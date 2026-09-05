namespace Sugarista.Domain.Entities;

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

public enum JournalSourceType
{
    Manual,
    OrderPayment,
    InventoryPurchase,
    PartnerCapital,
    PartnerWithdrawal,
    ProfitDistribution,
    OperatingExpense
}

public enum PartnerCapitalTransactionType
{
    Contribution,
    Withdrawal
}

public enum ProfitDistributionStatus
{
    Draft,
    Finalized
}
