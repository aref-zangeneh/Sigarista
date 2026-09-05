namespace Sugarista.Domain.Entities;

public enum OrderStatus
{
    Held,
    PendingPayment,
    PartiallyPaid,
    Paid,
    Failed,
    Cancelled
}
