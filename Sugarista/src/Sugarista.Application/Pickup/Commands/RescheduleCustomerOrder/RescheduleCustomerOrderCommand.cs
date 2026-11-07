using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Pickup.Commands.RescheduleCustomerOrder;

/// <summary>
/// Customer-initiated reschedule. The new slot obeys the same rules as a new
/// checkout (opening hours, lead time, capacity); the old slot is released
/// automatically because capacity is derived from DeliveryAt.
/// </summary>
public record RescheduleCustomerOrderCommand(
    Guid OrderId,
    string TrackingCode,
    DateTimeOffset DeliveryAt) : IRequest<CustomerOrderActionResultDto>;

public class RescheduleCustomerOrderCommandValidator
    : AbstractValidator<RescheduleCustomerOrderCommand>
{
    public RescheduleCustomerOrderCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.TrackingCode).NotEmpty().MaximumLength(20);
        RuleFor(command => command.DeliveryAt).NotEmpty();
    }
}

public class RescheduleCustomerOrderCommandHandler
    : IRequestHandler<RescheduleCustomerOrderCommand, CustomerOrderActionResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _clock;
    private readonly PickupSlotService _pickupSlotService;
    private readonly IPickupSlotReservationService _reservationService;
    private readonly ISmsService _smsService;
    private readonly ILogger<RescheduleCustomerOrderCommandHandler> _logger;

    public RescheduleCustomerOrderCommandHandler(
        IApplicationDbContext dbContext,
        IDateTime clock,
        PickupSlotService pickupSlotService,
        IPickupSlotReservationService reservationService,
        ISmsService smsService,
        ILogger<RescheduleCustomerOrderCommandHandler> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _pickupSlotService = pickupSlotService;
        _reservationService = reservationService;
        _smsService = smsService;
        _logger = logger;
    }

    public async Task<CustomerOrderActionResultDto> Handle(
        RescheduleCustomerOrderCommand request,
        CancellationToken cancellationToken)
    {
        var trackingCode = request.TrackingCode.Trim();
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(
                entity => entity.Id == request.OrderId && entity.TrackingCode == trackingCode,
                cancellationToken)
            ?? throw new KeyNotFoundException("سفارشی با این کد پیگیری پیدا نشد.");

        var siteSettings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        var pickupSettings = PickupSettingsSnapshot.From(siteSettings);
        var now = _clock.UtcNow;

        if (!pickupSettings.Enabled)
        {
            throw new DomainException("زمان‌بندی تحویل حضوری در حال حاضر فعال نیست.");
        }

        var blockedReason = PickupScheduleCalculator.BuildChangeBlockedReason(order, pickupSettings, now);
        if (blockedReason is not null)
        {
            throw new DomainException(blockedReason);
        }

        var preCheck = await _pickupSlotService.ValidateAsync(request.DeliveryAt, cancellationToken);
        if (!preCheck.IsValid)
        {
            throw new DomainException(preCheck.Message ?? "بازهٔ انتخاب‌شده معتبر نیست.");
        }

        var timeZone = PickupScheduleCalculator.ResolveTimeZone(pickupSettings.TimeZoneId);
        var openHours = await _pickupSlotService.GetOpenHoursAsync(cancellationToken);

        IPickupSlotReservation? reservation = null;
        try
        {
            reservation = await _reservationService.ReserveAsync(request.DeliveryAt, cancellationToken);

            var lockedCheck = await _pickupSlotService.ValidateCapacityOnlyAsync(
                request.DeliveryAt,
                pickupSettings,
                openHours,
                timeZone,
                cancellationToken);

            if (!lockedCheck.IsValid)
            {
                throw new DomainException(lockedCheck.Message ?? "این بازه هم‌اکنون تکمیل شده است.");
            }

            order.ApplyDeliveryTime(request.DeliveryAt);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await reservation.CommitAsync(cancellationToken);
        }
        finally
        {
            if (reservation is not null)
            {
                await reservation.DisposeAsync();
            }
        }

        var local = PickupScheduleCalculator.GetCafeLocalDateTime(request.DeliveryAt, timeZone);
        var label = FormatPersianDateTime(local);
        try
        {
            await _smsService.SendPickupScheduleChangedAsync(
                order.CustomerPhone,
                order.TrackingCode,
                order.CustomerName,
                label,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to send pickup reschedule SMS for order {OrderId}",
                order.Id);
        }

        return new CustomerOrderActionResultDto(
            order.TrackingCode,
            order.Status.ToString(),
            order.FulfillmentStatus.ToString(),
            order.DeliveryAt,
            $"زمان تحویل به {label} تغییر کرد و ظرفیت بازهٔ قبلی آزاد شد.");
    }

    private static string FormatPersianDateTime(DateTime local)
    {
        var calendar = new PersianCalendar();
        return $"{calendar.GetYear(local):0000}/{calendar.GetMonth(local):00}/{calendar.GetDayOfMonth(local):00} ساعت {local:HH:mm}";
    }
}
