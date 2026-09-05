using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class Account : BaseAuditableEntity
{
    private Account()
    {
    }

    public Account(string code, string name, string namePersian, AccountType type, bool isSystem = false)
    {
        SetCode(code);
        SetName(name, namePersian);
        Type = type;
        IsSystem = isSystem;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NamePersian { get; private set; } = string.Empty;

    public AccountType Type { get; private set; }

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public ICollection<JournalLine> JournalLines { get; private set; } = [];

    public void Update(string name, string namePersian, bool isActive)
    {
        if (IsSystem && !isActive)
        {
            throw new DomainException("System accounts cannot be deactivated.");
        }

        SetName(name, namePersian);
        IsActive = isActive;
    }

    private void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Account code is required.");
        }

        Code = code.Trim();
    }

    private void SetName(string name, string namePersian)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Account name is required.");
        }

        Name = name.Trim();
        NamePersian = string.IsNullOrWhiteSpace(namePersian) ? Name : namePersian.Trim();
    }
}
