namespace Sugarista.Domain.Entities;

public enum AffiliateStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2
}

public enum AffiliateDiscountType
{
    Percentage = 0,
    FixedAmount = 1
}

public enum AffiliateOrderSource
{
    Online = 0,
    Pos = 1
}

public enum AffiliateCommissionStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Paid = 3
}

public enum AffiliatePayoutType
{
    Cash = 0,
    WalletCredit = 1
}

public enum AffiliatePayoutStatus
{
    Requested = 0,
    Processing = 1,
    Completed = 2,
    Rejected = 3
}
