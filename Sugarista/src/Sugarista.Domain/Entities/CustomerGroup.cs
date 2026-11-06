using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>How membership of a customer group is approved.</summary>
public enum CustomerGroupApprovalMethod
{
    /// <summary>The admin adds the customer to the group manually.</summary>
    Manual = 0,

    /// <summary>
    /// Customers whose email address ends with the group's corporate domain
    /// are approved automatically (e.g. @company.com).
    /// </summary>
    CorporateEmailDomain = 1
}

/// <summary>
/// A customer segment (e.g. دانشجو، کارمندان شرکت X). Every group grants a
/// standing discount to its active members and has its own approval rule and
/// membership validity.
/// </summary>
public class CustomerGroup : BaseAuditableEntity
{
    public CustomerGroup(
        string name,
        string? description,
        decimal discountPercent,
        CustomerGroupApprovalMethod approvalMethod,
        string? emailDomain,
        int membershipValidityDays,
        int displayOrder = 0,
        bool isEnabled = true)
    {
        Update(name, description, discountPercent, approvalMethod, emailDomain, membershipValidityDays, displayOrder, isEnabled);
    }

    private CustomerGroup()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Standing discount (0-100) for active members.</summary>
    public decimal DiscountPercent { get; private set; }

    public CustomerGroupApprovalMethod ApprovalMethod { get; private set; }

    /// <summary>Corporate email domain (without @) for domain-based approval.</summary>
    public string? EmailDomain { get; private set; }

    /// <summary>Membership validity in days (0 = never expires).</summary>
    public int MembershipValidityDays { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsEnabled { get; private set; }

    public void Update(
        string name,
        string? description,
        decimal discountPercent,
        CustomerGroupApprovalMethod approvalMethod,
        string? emailDomain,
        int membershipValidityDays,
        int displayOrder,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Customer group name is required.");
        }

        if (discountPercent is < 0 or > 100)
        {
            throw new DomainException("Customer group discount must be between 0 and 100.");
        }

        if (membershipValidityDays < 0)
        {
            throw new DomainException("Customer group validity cannot be negative.");
        }

        if (approvalMethod == CustomerGroupApprovalMethod.CorporateEmailDomain &&
            string.IsNullOrWhiteSpace(emailDomain))
        {
            throw new DomainException("برای تایید با دامنه ایمیل، دامنهٔ سازمانی الزامی است.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DiscountPercent = discountPercent;
        ApprovalMethod = approvalMethod;
        EmailDomain = approvalMethod == CustomerGroupApprovalMethod.CorporateEmailDomain
            ? NormalizeDomain(emailDomain)
            : null;
        MembershipValidityDays = membershipValidityDays;
        DisplayOrder = displayOrder;
        IsEnabled = isEnabled;
    }

    public static string NormalizeDomain(string? domain) =>
        (domain ?? string.Empty).Trim().TrimStart('@').ToLowerInvariant();
}
