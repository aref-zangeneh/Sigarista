using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.CustomerSubscriptions;

public record AdminCustomerSubscriptionDto(
    Guid Id,
    Guid PackageId,
    string PackageName,
    int TotalUseCount,
    int UsedCount,
    int RemainingUses,
    decimal Price,
    decimal RemainingValue,
    int DailyUseLimit,
    int UsesToday,
    string? AllowedCategoryIds,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt,
    CustomerSubscriptionStatus Status);

public record GetAdminCustomerSubscriptionsQuery(Guid CustomerId)
    : IRequest<IReadOnlyCollection<AdminCustomerSubscriptionDto>>;

public class GetAdminCustomerSubscriptionsQueryHandler
    : IRequestHandler<GetAdminCustomerSubscriptionsQuery, IReadOnlyCollection<AdminCustomerSubscriptionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminCustomerSubscriptionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminCustomerSubscriptionDto>> Handle(
        GetAdminCustomerSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var subscriptions = await _dbContext.CustomerSubscriptions
            .Where(subscription => subscription.CustomerId == request.CustomerId)
            .OrderByDescending(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);

        // Report the truthful status: an active-but-past-expiry subscription
        // is shown as expired even before the sweep touches it.
        return subscriptions
            .Select(subscription => new AdminCustomerSubscriptionDto(
                subscription.Id,
                subscription.PackageId,
                subscription.PackageName,
                subscription.TotalUseCount,
                subscription.UsedCount,
                subscription.RemainingUses,
                subscription.Price,
                subscription.RemainingValue,
                subscription.DailyUseLimit,
                subscription.LastUseDate == DateOnly.FromDateTime(now.UtcDateTime)
                    ? subscription.UsesToday
                    : 0,
                subscription.AllowedCategoryIds,
                subscription.StartsAt,
                subscription.ExpiresAt,
                subscription.Status == CustomerSubscriptionStatus.Active && subscription.ExpiresAt <= now
                    ? CustomerSubscriptionStatus.Expired
                    : subscription.Status))
            .ToList();
    }
}

/// <summary>Admin sells/activates a package for a customer.</summary>
public record ActivateCustomerSubscriptionCommand(
    Guid CustomerId,
    Guid PackageId) : IRequest<AdminCustomerSubscriptionDto>;

public class ActivateCustomerSubscriptionCommandValidator : AbstractValidator<ActivateCustomerSubscriptionCommand>
{
    public ActivateCustomerSubscriptionCommandValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.PackageId).NotEmpty();
    }
}

public class ActivateCustomerSubscriptionCommandHandler
    : IRequestHandler<ActivateCustomerSubscriptionCommand, AdminCustomerSubscriptionDto>
{
    private readonly IApplicationDbContext _dbContext;

    public ActivateCustomerSubscriptionCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminCustomerSubscriptionDto> Handle(
        ActivateCustomerSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        var customerExists = await _dbContext.Customers
            .AnyAsync(customer => customer.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            throw new KeyNotFoundException("Customer was not found.");
        }

        var package = await _dbContext.SubscriptionPackages
            .FirstOrDefaultAsync(entry => entry.Id == request.PackageId, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription package was not found.");

        if (!package.IsEnabled)
        {
            throw new DomainException("این بستهٔ اشتراک غیرفعال است.");
        }

        // Snapshot the package values onto the sold contract.
        var subscription = new CustomerSubscription(
            request.CustomerId,
            package.Id,
            package.Name,
            package.TotalUseCount,
            package.Price,
            package.ValidityDays,
            package.DailyUseLimit,
            package.AllowedCategoryIds);

        _dbContext.CustomerSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminCustomerSubscriptionDto(
            subscription.Id,
            subscription.PackageId,
            subscription.PackageName,
            subscription.TotalUseCount,
            subscription.UsedCount,
            subscription.RemainingUses,
            subscription.Price,
            subscription.RemainingValue,
            subscription.DailyUseLimit,
            subscription.UsesToday,
            subscription.AllowedCategoryIds,
            subscription.StartsAt,
            subscription.ExpiresAt,
            subscription.Status);
    }
}

/// <summary>
/// Cancels a subscription. When <see cref="TransferToWallet"/> is true the
/// remaining value is credited back to the customer's wallet as a logged
/// Refund transaction (balance can never go negative because it is a credit).
/// </summary>
public record CancelCustomerSubscriptionCommand(
    Guid SubscriptionId,
    bool TransferToWallet = true) : IRequest<AdminCustomerSubscriptionDto>;

public class CancelCustomerSubscriptionCommandHandler
    : IRequestHandler<CancelCustomerSubscriptionCommand, AdminCustomerSubscriptionDto>
{
    private readonly IApplicationDbContext _dbContext;

    public CancelCustomerSubscriptionCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminCustomerSubscriptionDto> Handle(
        CancelCustomerSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        var subscription = await _dbContext.CustomerSubscriptions
            .FirstOrDefaultAsync(entry => entry.Id == request.SubscriptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription was not found.");

        if (subscription.Status == CustomerSubscriptionStatus.Cancelled)
        {
            throw new DomainException("این اشتراک قبلاً لغو شده است.");
        }

        var refundAmount = request.TransferToWallet ? subscription.RemainingValue : 0m;
        subscription.Cancel();

        if (refundAmount > 0)
        {
            var wallet = await WalletService.GetOrCreateAsync(_dbContext, subscription.CustomerId, cancellationToken);
            wallet.Credit(refundAmount);
            _dbContext.WalletTransactions.Add(new WalletTransaction(
                wallet.Id,
                WalletTransactionType.Refund,
                refundAmount,
                wallet.Balance,
                $"بازپرداخت لغو اشتراک «{subscription.PackageName}» ({subscription.RemainingUses} استفادهٔ باقی‌مانده)"));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminCustomerSubscriptionDto(
            subscription.Id,
            subscription.PackageId,
            subscription.PackageName,
            subscription.TotalUseCount,
            subscription.UsedCount,
            subscription.RemainingUses,
            subscription.Price,
            subscription.RemainingValue,
            subscription.DailyUseLimit,
            subscription.UsesToday,
            subscription.AllowedCategoryIds,
            subscription.StartsAt,
            subscription.ExpiresAt,
            subscription.Status);
    }
}

/// <summary>Assigns a customer to a group (manual approval).</summary>
public record AssignCustomerGroupCommand(
    Guid CustomerId,
    Guid GroupId) : IRequest;

public class AssignCustomerGroupCommandHandler : IRequestHandler<AssignCustomerGroupCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public AssignCustomerGroupCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(AssignCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _dbContext.CustomerGroups
            .FirstOrDefaultAsync(entry => entry.Id == request.GroupId, cancellationToken)
            ?? throw new KeyNotFoundException("Customer group was not found.");

        var customerExists = await _dbContext.Customers
            .AnyAsync(customer => customer.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            throw new KeyNotFoundException("Customer was not found.");
        }

        await CustomerGroupService.AssignAsync(_dbContext, group, request.CustomerId, cancellationToken);
    }
}

/// <summary>Removes a customer from a group.</summary>
public record RemoveCustomerGroupCommand(
    Guid CustomerId,
    Guid GroupId) : IRequest;

public class RemoveCustomerGroupCommandHandler : IRequestHandler<RemoveCustomerGroupCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public RemoveCustomerGroupCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(RemoveCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var removed = await CustomerGroupService.RemoveAsync(
            _dbContext, request.CustomerId, request.GroupId, cancellationToken);

        if (!removed)
        {
            throw new KeyNotFoundException("Customer group membership was not found.");
        }
    }
}
