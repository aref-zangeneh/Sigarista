using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Represents a single physical shelf/fridge lot (batch) of a ready-to-sell <see cref="MenuItem"/>
/// (such as cakes, desserts, and pastries) with its own arrival time and optional expiration date.
/// Sales automatically consume lots in FEFO/FIFO order (earliest-expiring first, then earliest received).
/// </summary>
public class MenuItemStockLot : BaseAuditableEntity
{
    private MenuItemStockLot()
    {
    }

    public MenuItemStockLot(
        Guid menuItemId,
        decimal quantity,
        DateTimeOffset receivedAt,
        DateTimeOffset? expiresAt = null,
        string? batchLabel = null)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("شناسه آیتم منو الزامی است.");
        }

        if (quantity < 0)
        {
            throw new DomainException("تعداد پارت موجودی نمی‌تواند منفی باشد.");
        }

        MenuItemId = menuItemId;
        InitialQuantity = quantity;
        RemainingQuantity = quantity;
        ReceivedAt = receivedAt.ToUniversalTime();
        ExpiresAt = expiresAt?.ToUniversalTime();
        BatchLabel = NormalizeLabel(batchLabel);
        IsDiscarded = false;
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public decimal InitialQuantity { get; private set; }

    public decimal RemainingQuantity { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public string? BatchLabel { get; private set; }

    public bool IsDiscarded { get; private set; }

    public DateTimeOffset? DiscardedAt { get; private set; }

    public string? DiscardReason { get; private set; }

    public bool IsActive => !IsDiscarded && RemainingQuantity > 0;

    public void UpdateDetails(
        decimal remainingQuantity,
        DateTimeOffset receivedAt,
        DateTimeOffset? expiresAt,
        string? batchLabel)
    {
        if (remainingQuantity < 0)
        {
            throw new DomainException("تعداد باقیمانده پارت نمی‌تواند منفی باشد.");
        }

        RemainingQuantity = remainingQuantity;
        if (remainingQuantity > InitialQuantity)
        {
            InitialQuantity = remainingQuantity;
        }

        ReceivedAt = receivedAt.ToUniversalTime();
        ExpiresAt = expiresAt?.ToUniversalTime();
        BatchLabel = NormalizeLabel(batchLabel);

        if (remainingQuantity > 0 && IsDiscarded)
        {
            IsDiscarded = false;
            DiscardedAt = null;
            DiscardReason = null;
        }
    }

    /// <summary>
    /// Consumes up to <paramref name="requested"/> units from this lot and returns
    /// the actual quantity consumed. Never lets <see cref="RemainingQuantity"/> drop below zero.
    /// </summary>
    public decimal Consume(decimal requested)
    {
        if (requested <= 0 || IsDiscarded || RemainingQuantity <= 0)
        {
            return 0m;
        }

        var taken = Math.Min(RemainingQuantity, requested);
        RemainingQuantity -= taken;
        return taken;
    }

    public void Restore(decimal quantity)
    {
        if (quantity <= 0)
        {
            return;
        }

        RemainingQuantity += quantity;
        if (RemainingQuantity > InitialQuantity)
        {
            InitialQuantity = RemainingQuantity;
        }
    }

    public void Discard(string? reason = null)
    {
        RemainingQuantity = 0m;
        IsDiscarded = true;
        DiscardedAt = DateTimeOffset.UtcNow;
        DiscardReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(200, reason.Trim().Length)];
    }

    private static string? NormalizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 120 ? trimmed[..120] : trimmed;
    }
}
