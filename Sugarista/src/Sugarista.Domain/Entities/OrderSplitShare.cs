using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class OrderSplitShare : BaseEntity
{
    private OrderSplitShare()
    {
    }

    public OrderSplitShare(
        Guid orderId,
        string payerKey,
        string payerLabel,
        decimal? sharePercentage,
        decimal? shareAmount,
        int sortOrder)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Split share order is required.");
        }

        if (string.IsNullOrWhiteSpace(payerKey))
        {
            throw new DomainException("Split share payer key is required.");
        }

        if (sharePercentage is < 0 or > 100)
        {
            throw new DomainException("Share percentage must be between 0 and 100.");
        }

        if (shareAmount is < 0)
        {
            throw new DomainException("Share amount must be greater than or equal to zero.");
        }

        OrderId = orderId;
        PayerKey = payerKey.Trim().ToLowerInvariant();
        PayerLabel = string.IsNullOrWhiteSpace(payerLabel) ? PayerKey : payerLabel.Trim();
        SharePercentage = sharePercentage;
        ShareAmount = shareAmount;
        SortOrder = sortOrder;
    }

    public Guid OrderId { get; private set; }

    public Order Order { get; private set; } = null!;

    public string PayerKey { get; private set; } = string.Empty;

    public string PayerLabel { get; private set; } = string.Empty;

    public decimal? SharePercentage { get; private set; }

    public decimal? ShareAmount { get; private set; }

    public int SortOrder { get; private set; }

    public void Update(decimal? sharePercentage, decimal? shareAmount, string payerLabel, int sortOrder)
    {
        if (sharePercentage is < 0 or > 100)
        {
            throw new DomainException("Share percentage must be between 0 and 100.");
        }

        if (shareAmount is < 0)
        {
            throw new DomainException("Share amount must be greater than or equal to zero.");
        }

        SharePercentage = sharePercentage;
        ShareAmount = shareAmount;
        PayerLabel = string.IsNullOrWhiteSpace(payerLabel) ? PayerKey : payerLabel.Trim();
        SortOrder = sortOrder;
    }
}
