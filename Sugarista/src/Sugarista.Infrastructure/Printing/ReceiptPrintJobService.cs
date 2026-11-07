using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.MenuItems.OptionGroups;
using Sugarista.Application.Printing;
using Sugarista.Application.Printing.ReceiptLayout;
using Sugarista.Domain.Entities;
using Sugarista.Infrastructure.Data;

namespace Sugarista.Infrastructure.Printing;

/// <summary>
/// Builds the receipt payload (all text pre-formatted, Persian digits, cafe
/// local time) and enqueues two print jobs per paid order:
/// one full customer receipt and one barista/kitchen ticket.
/// </summary>
public class ReceiptPrintJobService : IPrintJobService
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions SnapshotJsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly PersianCalendar PersianCalendar = new();

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ReceiptPrintJobService> _logger;

    public ReceiptPrintJobService(
        ApplicationDbContext dbContext,
        ILogger<ReceiptPrintJobService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task EnqueueOrderReceiptsAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            var hasActiveJobs = await _dbContext.PrintJobs
                .AnyAsync(job =>
                    job.OrderId == order.Id
                    && (job.Status == PrintJobStatus.Pending
                        || job.Status == PrintJobStatus.Dispatched
                        || job.Status == PrintJobStatus.Printed),
                    cancellationToken);

            if (hasActiveJobs)
            {
                _logger.LogInformation(
                    "Skipping receipt enqueue for order {OrderId}: active print jobs already exist.",
                    order.Id);
                return;
            }

            var settings = await _dbContext.SiteSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            var layout = ReceiptLayoutJsonParser.Parse(settings?.ReceiptLayoutJson);

            string? tableLabel = null;
            if (order.SalonTableId.HasValue)
            {
                var salonTable = await _dbContext.SalonTables
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == order.SalonTableId.Value, cancellationToken);
                tableLabel = salonTable?.Name;
            }

            string? deliveryLabel = null;
            if (order.DeliveryMethodId.HasValue)
            {
                var deliveryMethod = await _dbContext.DeliveryMethods
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == order.DeliveryMethodId.Value, cancellationToken);
                deliveryLabel = deliveryMethod?.PersianName;
            }

            var header = new ReceiptHeaderPayload(
                settings?.CafeName ?? "Sugarista Cafe",
                settings?.Address,
                settings?.ContactPhone);

            var basePayload = BuildReceiptPayload(order, settings, layout, tableLabel, deliveryLabel, header);

            var customerPayload = basePayload with { CopyType = PrintJobCopyType.Customer };
            var baristaPayload = basePayload with { CopyType = PrintJobCopyType.Barista };

            _dbContext.PrintJobs.Add(PrintJob.Create(
                order.Id,
                order.TrackingCode,
                PrintJobCopyType.Customer,
                JsonSerializer.Serialize(customerPayload, PayloadSerializerOptions)));

            _dbContext.PrintJobs.Add(PrintJob.Create(
                order.Id,
                order.TrackingCode,
                PrintJobCopyType.Barista,
                JsonSerializer.Serialize(baristaPayload, PayloadSerializerOptions)));

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Queued customer + barista receipts for order {OrderId} ({TrackingCode}).",
                order.Id,
                order.TrackingCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Receipt printing must never break checkout — log and move on.
            _logger.LogError(ex, "Failed to enqueue receipt print jobs for order {OrderId}.", order.Id);
        }
    }

    private static ReceiptPayload BuildReceiptPayload(
        Order order,
        SiteSettings? settings,
        ResolvedReceiptLayout layout,
        string? tableLabel,
        string? deliveryLabel,
        ReceiptHeaderPayload header)
    {
        var timeZone = ResolveTimeZone(settings?.TimeZoneId);
        var createdAtLabel = FormatCafeLocal(order.CreatedAt, timeZone);

        var showOptionPrices = layout.Customer.Items.ShowOptionPrices;
        var items = order.Lines
            .OrderBy(line => line.PersianName)
            .Select(line => new ReceiptItemPayload(
                line.PersianName,
                line.SizeLabel,
                line.Quantity,
                ToPersianNumber(line.Quantity),
                line.ModifierLabels?.ToList(),
                Money(line.UnitPrice),
                Money(line.LineTotal),
                string.IsNullOrWhiteSpace(line.PayerLabel) ? null : line.PayerLabel,
                line.BaristaNote,
                BuildOptionLines(line, showOptionPrices),
                BuildOptionTotalLabel(line),
                line.CustomDrinkSummary?.ToList()))
            .ToList();

        var channelLabel = order.Channel == OrderChannel.Pos
            ? (!order.IsSplitBill ? "سفارش صندوق (POS)" : "سفارش صندوق (POS)")
            : "سفارش آنلاین";

        var serviceLabel = order.ServiceType switch
        {
            PosServiceType.DineIn => "سرو در محل",
            PosServiceType.TakeAway => "بیرون‌بر",
            _ => deliveryLabel is null ? null : $"ارسال با {deliveryLabel}"
        };

        if (serviceLabel is null && deliveryLabel is not null)
        {
            serviceLabel = $"ارسال با {deliveryLabel}";
        }

        var paymentLabel = order.PaymentMethod switch
        {
            PosPaymentMethod.Cash => "نقدی (صندوق)",
            PosPaymentMethod.Card => "کارت (صندوق)",
            PosPaymentMethod.OnlineGateway => "پرداخت آنلاین",
            _ => order.Channel == OrderChannel.Pos ? "پرداخت صندوق" : "پرداخت آنلاین"
        };

        var reference = order.Payments
            .Where(payment => payment.ReferenceId is not null)
            .OrderByDescending(payment => payment.PaidAt)
            .FirstOrDefault()?.ReferenceId;

        var paidAtLabel = order.PaidAt.HasValue
            ? FormatCafeLocal(order.PaidAt.Value, timeZone)
            : createdAtLabel;

        var pickupAtLabel = order.DeliveryAt.HasValue
            ? FormatCafeLocal(order.DeliveryAt.Value, timeZone)
            : null;

        return new ReceiptPayload(
            CopyType: PrintJobCopyType.Customer,
            Header: header,
            Order: new ReceiptOrderPayload(
                Code: order.TrackingCode,
                ChannelLabel: channelLabel,
                ServiceLabel: serviceLabel,
                TableLabel: tableLabel,
                CustomerName: string.IsNullOrWhiteSpace(order.CustomerName) ? null : order.CustomerName,
                CustomerPhone: string.IsNullOrWhiteSpace(order.CustomerPhone) ? null : order.CustomerPhone,
                Note: order.Note,
                CreatedAtLabel: createdAtLabel,
                PaidAtLabel: paidAtLabel,
                PaymentLabel: paymentLabel,
                Reference: reference,
                PickupAtLabel: pickupAtLabel),
            Items: items,
            Totals: new ReceiptTotalsPayload(
                SubtotalLabel: Money(order.Lines.Sum(line => line.LineTotal)),
                DeliveryFeeLabel: order.DeliveryFee > 0 ? Money(order.DeliveryFee) : null,
                TaxLabel: order.TaxTotal > 0 ? Money(order.TaxTotal) : null,
                TotalLabel: Money(order.TotalAmount),
                AmountPaidLabel: order.AmountPaid > 0 ? Money(order.AmountPaid) : null),
            FooterText: settings?.FooterText,
            TimeZoneId: settings?.TimeZoneId ?? "Asia/Tehran",
            Layout: layout);
    }

    /// <summary>
    /// Selected per-item options of the line, from the immutable snapshot.
    /// <paramref name="showPrices"/> attaches the frozen price delta of each
    /// option (TASK-013), otherwise only the plain labels are returned.
    /// </summary>
    private static List<ReceiptOptionPayload>? BuildOptionLines(OrderLine line, bool showPrices)
    {
        if (string.IsNullOrWhiteSpace(line.OptionsJson))
        {
            return null;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<MenuItemOptionSnapshot>(
                line.OptionsJson,
                SnapshotJsonOptions);
            var components = snapshot?.Components;
            if (components is null || components.Count == 0)
            {
                return null;
            }

            return components
                .Select(component => new ReceiptOptionPayload(
                    component.GroupName,
                    component.OptionName,
                    showPrices && component.PriceDelta > 0 ? Money(component.PriceDelta) : null))
                .ToList();
        }
        catch (JsonException)
        {
            // Extremely defensive: a corrupt snapshot must never block printing.
            return line.OptionLabels?.Select(label => new ReceiptOptionPayload(null, label, null)).ToList();
        }
    }

    private static string? BuildOptionTotalLabel(OrderLine line)
    {
        if (string.IsNullOrWhiteSpace(line.OptionsJson))
        {
            return null;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<MenuItemOptionSnapshot>(
                line.OptionsJson,
                SnapshotJsonOptions);
            return snapshot is { OptionsTotal: > 0 } ? Money(snapshot.OptionsTotal) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception)
        {
            return TimeZoneInfo.Local;
        }
    }

    private static string FormatCafeLocal(DateTimeOffset moment, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(moment, timeZone);
        var localDateTime = local.DateTime;
        var date = $"{ToPersianNumber(PersianCalendar.GetYear(localDateTime))}/{ToPersianNumber(PersianCalendar.GetMonth(localDateTime), "00")}/{ToPersianNumber(PersianCalendar.GetDayOfMonth(localDateTime), "00")}";
        var time = $"{ToPersianNumber(localDateTime.Hour, "00")}:{ToPersianNumber(localDateTime.Minute, "00")}";
        return $"{date} — {time}";
    }

    /// <summary>Formats an amount with thousand separators and Persian digits (۱۲۵٬۰۰۰).</summary>
    public static string Money(decimal amount)
        => ToPersianNumber(string.Create(CultureInfo.InvariantCulture, $"{amount:N0}"))
            .Replace(",", "٬", StringComparison.Ordinal);

    public static string ToPersianNumber(string value)
    {
        const string latin = "0123456789";
        const string persian = "۰۱۲۳۴۵۶۷۸۹";
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            var index = latin.IndexOf(character);
            builder.Append(index >= 0 ? persian[index] : character);
        }

        return builder.ToString();
    }

    private static string ToPersianNumber(int value)
        => ToPersianNumber(value.ToString(CultureInfo.InvariantCulture));

    private static string ToPersianNumber(int value, string format)
        => ToPersianNumber(value.ToString(format, CultureInfo.InvariantCulture));
}

public record ReceiptHeaderPayload(
    string CafeName,
    string? Address,
    string? ContactPhone);

public record ReceiptItemPayload(
    string Name,
    string? SizeLabel,
    int Quantity,
    string QuantityLabel,
    List<string>? ModifierLabels,
    string UnitPriceLabel,
    string TotalLabel,
    string? PayerLabel,
    string? BaristaNote,
    /// <summary>Per-item options chosen at order time (TASK-013 snapshot).</summary>
    List<ReceiptOptionPayload>? Options,
    /// <summary>Sum of the option price deltas of the line (Toman, Persian digits).</summary>
    string? OptionTotalLabel,
    /// <summary>Custom drink builder summary lines (e.g. "۲ پیمانه سرکه مالت").</summary>
    List<string>? CustomDrinkSummary);

public record ReceiptOptionPayload(
    string? GroupName,
    string OptionName,
    /// <summary>Frozen price delta when the layout asks for per-option prices.</summary>
    string? PriceDeltaLabel);

public record ReceiptOrderPayload(
    string Code,
    string ChannelLabel,
    string? ServiceLabel,
    string? TableLabel,
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    string CreatedAtLabel,
    string PaidAtLabel,
    string PaymentLabel,
    string? Reference,
    /// <summary>Scheduled pickup/delivery moment in cafe local time (pickup scheduling).</summary>
    string? PickupAtLabel);

public record ReceiptTotalsPayload(
    string SubtotalLabel,
    string? DeliveryFeeLabel,
    string? TaxLabel,
    string TotalLabel,
    string? AmountPaidLabel);

public record ReceiptPayload(
    PrintJobCopyType CopyType,
    ReceiptHeaderPayload Header,
    ReceiptOrderPayload Order,
    List<ReceiptItemPayload> Items,
    ReceiptTotalsPayload Totals,
    string? FooterText,
    string TimeZoneId,
    /// <summary>Receipt layout resolved at enqueue time — the agent renders from it.</summary>
    ResolvedReceiptLayout Layout);
