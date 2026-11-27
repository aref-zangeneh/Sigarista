using Sugarista.Domain.Entities;

namespace Sugarista.Domain.Constants;

public static class AccountingAccountCodes
{
    public const string Cash = "1010";
    public const string CardReceivable = "1020";
    public const string Inventory = "1100";
    public const string AccountsReceivable = "1200";
    public const string AccountsPayable = "2010";
    public const string AffiliatePayable = "2020";
    public const string InputVat = "2110";
    public const string PartnerCapital = "3010";
    public const string PartnerDrawings = "3020";
    public const string RetainedEarnings = "3200";
    public const string SalesRevenue = "4010";
    public const string CostOfGoodsSold = "5010";
    public const string InternalConsumptionExpense = "5020";
    public const string ComplimentaryHospitalityExpense = "5030";
    public const string OperatingExpense = "5100";
    public const string AffiliateCommissionExpense = "5110";

    public static readonly IReadOnlyList<(string Code, string Name, string NamePersian, AccountType Type)> SeedAccounts =
    [
        (Cash, "Cash on Hand", "صندوق", AccountType.Asset),
        (CardReceivable, "Card Receivable", "حساب کارت‌خوان", AccountType.Asset),
        (Inventory, "Inventory", "موجودی کالا", AccountType.Asset),
        (AccountsReceivable, "Accounts Receivable", "حساب‌های دریافتنی", AccountType.Asset),
        (AccountsPayable, "Accounts Payable", "حساب‌های پرداختنی", AccountType.Liability),
        (AffiliatePayable, "Affiliate Commissions Payable", "بدهی پورسانت همکاران فروش", AccountType.Liability),
        (InputVat, "Input VAT", "مالیات بر ارزش افزوده ورودی", AccountType.Asset),
        (PartnerCapital, "Partner Capital", "سرمایه شرکا", AccountType.Equity),
        (PartnerDrawings, "Partner Drawings", "برداشت شرکا", AccountType.Equity),
        (RetainedEarnings, "Retained Earnings", "سود انباشته", AccountType.Equity),
        (SalesRevenue, "Sales Revenue", "درآمد فروش", AccountType.Revenue),
        (CostOfGoodsSold, "Cost of Goods Sold", "بهای تمام‌شده", AccountType.Expense),
        (InternalConsumptionExpense, "Internal Consumption", "مصرف داخلی پرسنل", AccountType.Expense),
        (ComplimentaryHospitalityExpense, "Complimentary & Hospitality", "پذیرایی و سفارش‌های مهمان (رایگان)", AccountType.Expense),
        (OperatingExpense, "Operating Expenses", "هزینه‌های جاری", AccountType.Expense),
        (AffiliateCommissionExpense, "Affiliate Commission Expense", "هزینه پورسانت همکاری در فروش", AccountType.Expense),
    ];
}
