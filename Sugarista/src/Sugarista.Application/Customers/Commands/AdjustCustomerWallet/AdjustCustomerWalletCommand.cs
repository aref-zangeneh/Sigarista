using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Queries.GetAdminCustomerWallet;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Customers.Commands.AdjustCustomerWallet;

/// <summary>
/// Admin wallet management: charges money in (positive, issued against the
/// cash the customer handed over) or corrects the balance (negative). Every
/// action writes one wallet transaction — history is never edited.
/// </summary>
public record AdjustCustomerWalletCommand(
    Guid CustomerId,
    decimal Amount,
    string? Description) : IRequest<AdminCustomerWalletDto?>;

public class AdjustCustomerWalletCommandValidator : AbstractValidator<AdjustCustomerWalletCommand>
{
    public AdjustCustomerWalletCommandValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.Amount).NotEqual(0).WithMessage("مبلغ اصلاح نمی‌تواند صفر باشد.");
        RuleFor(command => command.Amount).GreaterThan(-1_000_000_000m);
        RuleFor(command => command.Amount).LessThan(1_000_000_000m);
    }
}

public class AdjustCustomerWalletCommandHandler : IRequestHandler<AdjustCustomerWalletCommand, AdminCustomerWalletDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public AdjustCustomerWalletCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminCustomerWalletDto?> Handle(AdjustCustomerWalletCommand request, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(entry => entry.Id == request.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException("Customer was not found.");

        var wallet = await WalletService.GetOrCreateAsync(_dbContext, customer.Id, cancellationToken);

        // Refuse impossible corrections up front — the balance must never go
        // below zero even for a mistaken admin adjustment.
        if (request.Amount < 0)
        {
            var balance = await _dbContext.CustomerWallets
                .AsNoTracking()
                .Where(walletEntry => walletEntry.CustomerId == customer.Id)
                .Select(walletEntry => (decimal?)walletEntry.Balance)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;
            if (balance < -request.Amount)
            {
                throw new DomainException("کسر بیشتر از موجودی کیف پول ممکن نیست.");
            }
        }

        if (request.Amount > 0)
        {
            await WalletService.TopUpAsync(
                _dbContext,
                wallet,
                request.Amount,
                request.Description ?? $"شارژ کیف پولِ {customer.PersianName}",
                cancellationToken);
        }
        else
        {
            await WalletService.AdjustAsync(
                _dbContext,
                wallet,
                request.Amount,
                request.Description ?? $"اصلاح دستی کیف پولِ {customer.PersianName}",
                cancellationToken);
        }

        // Fresh load so the response matches what is actually stored.
        var reloaded = await _dbContext.CustomerWallets
            .AsNoTracking()
            .FirstAsync(walletEntry => walletEntry.CustomerId == customer.Id, cancellationToken);
        var records = await _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(record => record.WalletId == reloaded.Id)
            .OrderByDescending(record => record.CreatedAt)
            .Take(50)
            .Select(record => new AdminWalletMovementDto(
                record.Id,
                record.Type,
                record.Amount,
                record.BalanceAfter,
                record.Description,
                record.OrderId,
                record.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminCustomerWalletDto(
            customer.Id,
            customer.PersianName,
            customer.PhoneNumber,
            reloaded.Balance,
            records);
    }
}
