namespace Sugarista.Application.Customers.Auth;

/// <summary>
/// Pure rules for the phone-only customer login — kept free of I/O so they can
/// be unit-tested directly. Iranian phone numbers are matched in a canonical
/// digits-only form: `+98`, `98`, `0098` prefixes fold to a leading zero so
/// the same phone always resolves to ONE customer record.
/// </summary>
public static class CustomerLoginRules
{
    /// <summary>Canonical form: digits only, leading `+98/98/0098` folded to `0`.</summary>
    public static string NormalizePhone(string? phone)
    {
        var raw = (phone ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return raw;
        }

        if (raw.StartsWith("0098", StringComparison.Ordinal) && raw.Length > 4)
        {
            raw = "0" + raw[4..];
        }
        else if (raw.StartsWith("+98", StringComparison.Ordinal) && raw.Length > 3)
        {
            raw = "0" + raw[3..];
        }
        else if (raw.StartsWith("98", StringComparison.Ordinal) && raw.Length == 12)
        {
            raw = "0" + raw[2..];
        }
        else if (raw.Length == 10 && raw.StartsWith('9'))
        {
            // "912…" — a 10-digit national mobile missing its leading zero.
            raw = "0" + raw;
        }

        return new string(raw.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// A login-able phone: 8-15 digits (national mobile is 11, we stay open
    /// enough for foreign customers without accepting garbage).
    /// </summary>
    public static bool IsValidPhone(string phone)
    {
        if (phone.Length < 8 || phone.Length > 15)
        {
            return false;
        }

        return phone.All(char.IsAsciiDigit);
    }

    /// <summary>Settings are clamped to sane bounds regardless of what is stored.</summary>
    public static int ClampDurationDays(int settingsDays)
        => settingsDays switch
        {
            < 1 => 60,
            > 730 => 730,
            _ => settingsDays
        };
}
