using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Queries.GetAdminCustomerWallet;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Wallets.Commands.ApplyWalletTopUpPackage;

/// <summary>
/// Applies a pre-defined wallet charge package to a customer's wallet:
/// credits the paid amount as TopUp and (when BonusAmount > 0) credits
/// the gift bonus as Cashback in one consistent server-side action.
/// </summary>
public record ApplyWalletTopUpPackageCommand(
    Guid CustomerId,
    Guid PackageId,
    string? Note = null) : IRequest<AdminCustomerWalletDto>;

public class ApplyWalletTopUpPackageCommandValidator : AbstractValidator<ApplyWalletTopUpPackageCommand>
{
    public ApplyWalletTopUpPackageCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.PackageId).NotEmpty();
        RuleFor(c => c.Note).MaximumLength(300);
    }
}

public class ApplyWalletTopUpPackageCommandHandler
    : IRequestHandler<ApplyWalletTopUpPackageCommand, AdminCustomerWalletDto>
{
    private readonly IApplicationDbContext _dbContext;

    public ApplyWalletTopUpPackageCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminCustomerWalletDto> Handle(
        ApplyWalletTopUpPackageCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException("مشتری یافت نشد.");

        var package = await _dbContext.WalletTopUpPackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, cancellationToken)
            ?? throw new KeyNotFoundException("بسته شارژ یافت نشد.");

        if (!package.IsEnabled)
        {
            throw new DomainException("این بسته شارژ غیرفعال است.");
        }

        var wallet = await WalletService.GetOrCreateAsync(_dbContext, customer.Id, cancellationToken);
        var suffix = string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" — {request.Note.Trim()}";

        await WalletService.TopUpAsync(
            _dbContext,
            wallet,
            package.Amount,
            $"شارژ بسته کیف پول ({package.Amount:N0} تومان){suffix}",
            cancellationToken);

        if (package.BonusAmount > 0m)
        {
            wallet.Credit(package.BonusAmount);
            _dbContext.WalletTransactions.Add(new WalletTransaction(
                wallet.Id,
                WalletTransactionType.Cashback,
                package.BonusAmount,
                wallet.Balance,
                $"هدیه بسته شارژ ({package.BonusAmount:N0} تومان){suffix}"));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var reloaded = await _dbContext.CustomerWallets
            .AsNoTracking()
            .FirstAsync(w => w.CustomerId == customer.Id, cancellationToken);

        var records = await _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == reloaded.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new AdminWalletMovementDto(
                t.Id,
                t.Type,
                t.Amount,
                t.BalanceAfter,
                t.Description,
                t.OrderId,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminCustomerWalletDto(
            customer.Id,
            customer.PersianName,
            customer.PhoneNumber,
            reloaded.Balance,
            records);
    }
}
