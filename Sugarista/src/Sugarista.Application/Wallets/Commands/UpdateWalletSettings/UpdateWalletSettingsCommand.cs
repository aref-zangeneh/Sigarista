using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Wallets.Queries.GetAdminWalletSummary;

namespace Sugarista.Application.Wallets.Commands.UpdateWalletSettings;

public record UpdateWalletSettingsCommand(
    bool WalletEnabled,
    decimal WalletCashbackPercent,
    decimal WalletCashbackMinOrderAmount,
    decimal WalletCashbackMaxPerOrderAmount,
    int WalletCashbackExpiryDays,
    int WalletPaymentMaxPercent,
    string PickupRefundMode) : IRequest<AdminWalletSettingsDto>;

public class UpdateWalletSettingsCommandValidator : AbstractValidator<UpdateWalletSettingsCommand>
{
    private static readonly string[] AllowedRefundModes = ["Wallet", "Manual"];

    public UpdateWalletSettingsCommandValidator()
    {
        RuleFor(c => c.WalletCashbackPercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("درصد کش‌بک باید بین ۰ تا ۱۰۰ باشد.");

        RuleFor(c => c.WalletCashbackMinOrderAmount)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("حداقل مبلغ سفارش برای کش‌بک نمی‌تواند منفی باشد.");

        RuleFor(c => c.WalletCashbackMaxPerOrderAmount)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("سقف کش‌بک هر سفارش نمی‌تواند منفی باشد.");

        RuleFor(c => c.WalletCashbackExpiryDays)
            .InclusiveBetween(0, 3650)
            .WithMessage("مدت انقضای کش‌بک باید بین ۰ تا ۳۶۵۰ روز باشد.");

        RuleFor(c => c.WalletPaymentMaxPercent)
            .InclusiveBetween(1, 100)
            .WithMessage("سقف پرداخت از کیف پول باید بین ۱ تا ۱۰۰ درصد باشد.");

        RuleFor(c => c.PickupRefundMode)
            .Must(mode => string.IsNullOrWhiteSpace(mode) || AllowedRefundModes.Contains(mode.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("حالت بازگشت وجه باید Wallet یا Manual باشد.");
    }
}

public class UpdateWalletSettingsCommandHandler
    : IRequestHandler<UpdateWalletSettingsCommand, AdminWalletSettingsDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateWalletSettingsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminWalletSettingsDto> Handle(
        UpdateWalletSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new Sugarista.Domain.Entities.SiteSettings();
            _dbContext.SiteSettings.Add(settings);
        }

        var normalizedRefundMode = string.Equals(request.PickupRefundMode?.Trim(), "Manual", StringComparison.OrdinalIgnoreCase)
            ? "Manual"
            : "Wallet";

        settings.WalletEnabled = request.WalletEnabled;
        settings.WalletCashbackPercent = request.WalletCashbackPercent;
        settings.WalletCashbackMinOrderAmount = request.WalletCashbackMinOrderAmount;
        settings.WalletCashbackMaxPerOrderAmount = request.WalletCashbackMaxPerOrderAmount;
        settings.WalletCashbackExpiryDays = request.WalletCashbackExpiryDays;
        settings.WalletPaymentMaxPercent = request.WalletPaymentMaxPercent;
        settings.PickupRefundMode = normalizedRefundMode;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminWalletSettingsDto(
            settings.WalletEnabled,
            settings.WalletCashbackPercent,
            settings.WalletCashbackMinOrderAmount,
            settings.WalletCashbackMaxPerOrderAmount,
            settings.WalletCashbackExpiryDays,
            settings.WalletPaymentMaxPercent,
            settings.PickupRefundMode);
    }
}
