using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.WalletPackages.UpsertWalletPackage;

/// <summary>Creates a wallet charge package when <see cref="Id"/> is empty, otherwise updates it.</summary>
public record UpsertWalletPackageCommand(
    Guid Id,
    decimal Amount,
    decimal BonusAmount,
    int DisplayOrder = 0,
    bool IsEnabled = true) : IRequest<AdminWalletPackageDto>;

public class UpsertWalletPackageCommandValidator : AbstractValidator<UpsertWalletPackageCommand>
{
    public UpsertWalletPackageCommandValidator()
    {
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Amount).LessThan(100_000_000_000m);
        RuleFor(command => command.BonusAmount).GreaterThanOrEqualTo(0);
        RuleFor(command => command.BonusAmount).LessThan(100_000_000_000m);
        RuleFor(command => command.DisplayOrder).InclusiveBetween(-9999, 9999);
    }
}

public class UpsertWalletPackageCommandHandler : IRequestHandler<UpsertWalletPackageCommand, AdminWalletPackageDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertWalletPackageCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminWalletPackageDto> Handle(UpsertWalletPackageCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            var package = new WalletTopUpPackage(
                request.Amount,
                request.BonusAmount,
                request.DisplayOrder,
                request.IsEnabled);
            _dbContext.WalletTopUpPackages.Add(package);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new AdminWalletPackageDto(
                package.Id,
                package.Amount,
                package.BonusAmount,
                package.DisplayOrder,
                package.IsEnabled);
        }

        var existing = await _dbContext.WalletTopUpPackages
            .FirstOrDefaultAsync(packageEntry => packageEntry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Wallet package was not found.");

        existing.Update(request.Amount, request.BonusAmount, request.DisplayOrder, request.IsEnabled);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new AdminWalletPackageDto(
            existing.Id,
            existing.Amount,
            existing.BonusAmount,
            existing.DisplayOrder,
            existing.IsEnabled);
    }
}
