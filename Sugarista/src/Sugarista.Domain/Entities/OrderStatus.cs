namespace Sugarista.Domain.Entities;

public enum OrderStatus
{
    PendingPayment,
    PartiallyPaid,
    Paid,
    Failed,
    Cancelled
}
