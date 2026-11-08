using Sugarista.Application.Orders;
using Sugarista.Application.Orders.Commands.CreateOrderCheckout;

namespace Sugarista.Application.UnitTests;

/// <summary>
/// Tests for the checkout price re-validation guard
/// (<see cref="CreateOrderCheckoutCommandHandler.EnsurePricesHaveNotChanged"/>).
///
/// The theme sends the total and unit prices it displayed. When they no longer
/// match the server-resolved values, checkout must abort with a structured
/// 409 conflict (code "price_changed") so the customer explicitly confirms —
/// silent re-pricing is forbidden.
///
/// Regression context: a theme bug shipped 2026-11-04 sent
/// <c>expectedTotalAmount: null</c> for four days, which silently skipped the
/// total check. These tests lock in both the null-tolerant behavior (the field
/// is optional) and the mismatch behavior, so neither side can regress quietly.
/// </summary>
public class CheckoutPriceGuardTests
{
    private static RequestBuilder Builder() => new();

    private static OrderPricing.ResolvedLine Line(
        string persianName,
        int quantity,
        decimal unitPrice,
        string? sizeKey = "small",
        string? sizeLabel = null)
    {
        return new OrderPricing.ResolvedLine(
            Guid.NewGuid(),
            persianName,
            "English",
            sizeKey,
            sizeLabel,
            quantity,
            unitPrice,
            [],
            Guid.NewGuid(),
            0,
            []);
    }

    private static void InvokeGuard(
        CreateOrderCheckoutCommand request,
        IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, string? clientLineKey)> lines,
        decimal itemsTotal,
        decimal totalAmount)
    {
        CreateOrderCheckoutCommandHandler.EnsurePricesHaveNotChanged(request, lines, itemsTotal, totalAmount);
    }

    [Fact]
    public void Guard_WithoutAnyExpectedData_DoesNotThrow()
    {
        // The theme is allowed to omit the expected values (older versions did,
        // and a bug regression could send them as null again) — the guard skips
        // silently instead of breaking checkout flow.
        var request = Builder().Build();
        var lines = new[] { (Line("اسپرسو", quantity: 1, unitPrice: 120_000), (string?)"line-1") };

        var exception = Record.Exception(() => InvokeGuard(request, lines, itemsTotal: 120_000, totalAmount: 120_000));

        Assert.Null(exception);
    }

    [Fact]
    public void Guard_WithMatchingExpectedTotalAndLinePrices_DoesNotThrow()
    {
        var espresso = Line("اسپرسو", quantity: 2, unitPrice: 120_000);
        var request = Builder()
            .WithExpectedTotalAmount(240_000)
            .WithExpectedLinePrices(("line-1", espresso.MenuItemId, espresso.SizeKey, 120_000))
            .Build();

        var exception = Record.Exception(() => InvokeGuard(
            request,
            lines: [(espresso, (string?)"line-1")],
            itemsTotal: 240_000,
            totalAmount: 240_000));

        Assert.Null(exception);
    }

    [Fact]
    public void Guard_WithStaleExpectedTotal_ThrowsPriceChangedConflict()
    {
        // What the buggy theme produced in practice: the displayed cart total
        // (NaN) was serialized as null; here the guest deliberately acts on a
        // stale total — the server must refuse and surface the old/new totals.
        var espresso = Line("اسپرسو", quantity: 1, unitPrice: 120_000);
        var request = Builder()
            .WithExpectedTotalAmount(100_000)
            .WithExpectedLinePrices(("line-1", espresso.MenuItemId, espresso.SizeKey, 120_000))
            .Build();

        var exception = Assert.Throws<CheckoutConflictException>(() => InvokeGuard(
            request,
            lines: [(espresso, (string?)"line-1")],
            itemsTotal: 120_000,
            totalAmount: 120_000));

        Assert.Equal("price_changed", exception.Code);
        Assert.Equal(100_000m, exception.Details["oldItemsTotal"]);
        Assert.Equal(120_000m, exception.Details["newItemsTotal"]);
        var changes = Assert.IsType<Dictionary<string, object?>[]>(exception.Details["changes"]);
        Assert.Empty(changes);
    }

    [Fact]
    public void Guard_WithTotalWithinPennyTolerance_DoesNotThrow()
    {
        var espresso = Line("اسپرسو", quantity: 1, unitPrice: 120_000);
        var request = Builder().WithExpectedTotalAmount(120_000m - 0.01m).Build();

        var exception = Record.Exception(() => InvokeGuard(
            request,
            lines: [(espresso, (string?)"line-1")],
            itemsTotal: 120_000,
            totalAmount: 120_000));

        Assert.Null(exception);
    }

    [Fact]
    public void Guard_WithTotalBeyondPennyTolerance_ThrowsPriceChangedConflict()
    {
        var request = Builder().WithExpectedTotalAmount(120_000m - 0.02m).Build();
        var espresso = Line("اسپرسو", quantity: 1, unitPrice: 120_000);

        var exception = Assert.Throws<CheckoutConflictException>(() => InvokeGuard(
            request,
            lines: [(espresso, (string?)"line-1")],
            itemsTotal: 120_000,
            totalAmount: 120_000));

        Assert.Equal("price_changed", exception.Code);
    }

    [Fact]
    public void Guard_WithChangedUnitPrice_ReportsPerLineChange()
    {
        // The espresso price was raised on the server between browse and
        // checkout. Even when the stale total would coincidentally match, the
        // changed unit price must be reported line by line.
        var espresso = Line("اسپرسو", quantity: 2, unitPrice: 150_000, sizeKey: "small", sizeLabel: "مینیلو");
        var request = Builder()
            .WithExpectedTotalAmount(240_000)
            .WithExpectedLinePrices(
                ("line-1", espresso.MenuItemId, "small", 120_000))
            .Build();

        var exception = Assert.Throws<CheckoutConflictException>(() => InvokeGuard(
            request,
            lines: [(espresso, (string?)"line-1")],
            itemsTotal: 300_000,
            totalAmount: 300_000));

        Assert.Equal("price_changed", exception.Code);
        var changes = Assert.IsType<Dictionary<string, object?>[]>(exception.Details["changes"]);
        var change = Assert.Single(changes);
        Assert.Equal(espresso.MenuItemId, change["menuItemId"]);
        Assert.Equal("اسپرسو", change["persianName"]);
        Assert.Equal(120_000m, change["oldUnitPrice"]);
        Assert.Equal(150_000m, change["newUnitPrice"]);
        Assert.Equal(2, change["quantity"]);
    }

    [Fact]
    public void Guard_WithUnkeyedOrUnknownLineKeys_SkipsThoseLines()
    {
        // Lines without a client key (legacy clients) or snapshots keyed to a
        // different line cannot be compared — those are skipped without noise.
        var espresso = Line("اسپرسو", quantity: 1, unitPrice: 150_000);
        var request = Builder()
            .WithExpectedLinePrices(
                ("other-line", Guid.NewGuid(), "small", 999_000))
            .Build();

        var exception = Record.Exception(() => InvokeGuard(
            request,
            lines: new[]
            {
                (espresso, (string?)"line-1"),
                (Line("لاته", quantity: 1, unitPrice: 200_000), null!)
            },
            itemsTotal: 350_000,
            totalAmount: 350_000));

        Assert.Null(exception);
    }

    [Fact]
    public void Guard_WithAcceptPriceChanges_NeverThrowsEvenForStalePrices()
    {
        // "تأیید قیمت جدید و ادامه" — the customer saw the conflict and
        // explicitly accepted the new prices; the guard must not re-abort.
        var request = Builder()
            .WithExpectedTotalAmount(100_000)
            .WithExpectedLinePrices(("line-1", Guid.NewGuid(), "small", 1_000))
            .WithAcceptPriceChanges()
            .Build();
        var espresso = Line("اسپرسو", quantity: 1, unitPrice: 150_000);

        var exception = Record.Exception(() => InvokeGuard(
            request,
            lines: [(espresso, (string?)"line-1")],
            itemsTotal: 150_000,
            totalAmount: 150_000));

        Assert.Null(exception);
    }
}

/// <summary>
/// Thin fluent builder so each test states only the fields it cares about.
/// </summary>
public sealed class RequestBuilder
{
    private decimal? _expectedTotalAmount;
    private IReadOnlyCollection<CheckoutPriceSnapshot>? _expectedLinePrices;
    private bool _acceptPriceChanges;

    public RequestBuilder WithExpectedTotalAmount(decimal amount)
    {
        _expectedTotalAmount = amount;
        return this;
    }

    public RequestBuilder WithExpectedLinePrices(params (string? clientLineKey, Guid menuItemId, string? sizeKey, decimal unitPrice)[] snapshots)
    {
        _expectedLinePrices = snapshots
            .Select(snapshot => new CheckoutPriceSnapshot(snapshot.clientLineKey, snapshot.menuItemId, snapshot.sizeKey, snapshot.unitPrice))
            .ToArray();
        return this;
    }

    public RequestBuilder WithAcceptPriceChanges()
    {
        _acceptPriceChanges = true;
        return this;
    }

    public CreateOrderCheckoutCommand Build()
    {
        return new CreateOrderCheckoutCommand(
            CustomerName: "مشتری تست",
            CustomerPhone: "09120000000",
            Note: null,
            Items: [new CheckoutLineRequest(Guid.NewGuid(), "small", 1, [])],
            ExpectedTotalAmount: _expectedTotalAmount,
            ExpectedLinePrices: _expectedLinePrices,
            AcceptPriceChanges: _acceptPriceChanges);
    }
}
