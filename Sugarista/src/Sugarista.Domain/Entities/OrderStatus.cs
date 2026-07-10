namespace Sugarista.Domain.Entities;

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Failed,
    Cancelled,
}
