using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Represents a cafe/admin bank card configured for Card-to-Card (کارت به کارت)
/// payments in POS, including card metadata, visual theme, and an optional uploaded card photo.
/// </summary>
public class AdminBankCard : BaseAuditableEntity
{
    private AdminBankCard()
    {
    }

    public AdminBankCard(
        string title,
        string bankName,
        string cardHolderName,
        string cardNumber,
        string? shebaNumber = null,
        string? cardImageUrl = null,
        string cardTheme = "ocean",
        bool isDefault = false,
        bool isActive = true,
        int displayOrder = 0)
    {
        Update(
            title,
            bankName,
            cardHolderName,
            cardNumber,
            shebaNumber,
            cardImageUrl,
            cardTheme,
            isDefault,
            isActive,
            displayOrder);
    }

    public string Title { get; private set; } = string.Empty;

    public string BankName { get; private set; } = string.Empty;

    public string CardHolderName { get; private set; } = string.Empty;

    /// <summary>Normalized 16-digit card number (digits only).</summary>
    public string CardNumber { get; private set; } = string.Empty;

    public string? ShebaNumber { get; private set; }

    public string? CardImageUrl { get; private set; }

    public string CardTheme { get; private set; } = "ocean";

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int DisplayOrder { get; private set; }

    public void Update(
        string title,
        string bankName,
        string cardHolderName,
        string cardNumber,
        string? shebaNumber,
        string? cardImageUrl,
        string cardTheme,
        bool isDefault,
        bool isActive,
        int displayOrder)
    {
        var normalizedDigits = NormalizeDigitsOnly(cardNumber);
        if (normalizedDigits.Length != 16)
        {
            throw new DomainException("شماره کارت بانکی باید دقیقاً ۱۶ رقم باشد.");
        }

        if (string.IsNullOrWhiteSpace(cardHolderName))
        {
            throw new DomainException("نام صاحب کارت الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(bankName))
        {
            throw new DomainException("نام بانک الزامی است.");
        }

        Title = string.IsNullOrWhiteSpace(title)
            ? $"{bankName.Trim()} — {cardHolderName.Trim()}"
            : Truncate(title.Trim(), 120);
        BankName = Truncate(bankName.Trim(), 100);
        CardHolderName = Truncate(cardHolderName.Trim(), 150);
        CardNumber = normalizedDigits;
        ShebaNumber = NormalizeOptional(shebaNumber, 34);
        CardImageUrl = NormalizeOptional(cardImageUrl, 500);
        CardTheme = string.IsNullOrWhiteSpace(cardTheme) ? "ocean" : Truncate(cardTheme.Trim().ToLowerInvariant(), 40);
        IsDefault = isDefault;
        IsActive = isActive;
        DisplayOrder = displayOrder < 0 ? 0 : displayOrder;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }

    private static string NormalizeDigitsOnly(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var chars = new List<char>(20);
        foreach (var ch in raw)
        {
            if (ch is >= '0' and <= '9') chars.Add(ch);
            else if (ch is >= '۰' and <= '۹') chars.Add((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩') chars.Add((char)('0' + (ch - '٠')));
        }
        return new string(chars.ToArray());
    }

    private static string Truncate(string value, int maxLen)
        => value.Length > maxLen ? value[..maxLen] : value;

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > maxLen ? trimmed[..maxLen] : trimmed;
    }
}
