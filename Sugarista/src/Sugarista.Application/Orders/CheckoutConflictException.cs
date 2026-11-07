namespace Sugarista.Application.Orders;

/// <summary>
/// Raised when the server-side pre-submit re-validation fails: the slot filled
/// up, a product sold out, or a price changed while the customer was checking
/// out. The API turns this into HTTP 409 with a machine-readable
/// <c>code</c> plus structured details the theme uses to offer the next action
/// (remove an item, confirm the new price, pick the nearest slot).
/// </summary>
public sealed class CheckoutConflictException : Exception
{
    public CheckoutConflictException(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        Details = details ?? new Dictionary<string, object?>();
    }

    public string Code { get; }

    public IReadOnlyDictionary<string, object?> Details { get; }
}
