using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.SubscriptionPackages;

public record AdminSubscriptionPackageDto(
    Guid Id,
    string Name,
    string? Description,
    int TotalUseCount,
    decimal Price,
    int ValidityDays,
    int DailyUseLimit,
    string? AllowedCategoryIds,
    int DisplayOrder,
    bool IsEnabled);

public record GetAdminSubscriptionPackagesQuery : IRequest<IReadOnlyCollection<AdminSubscriptionPackageDto>>;

public class GetAdminSubscriptionPackagesQueryHandler
    : IRequestHandler<GetAdminSubscriptionPackagesQuery, IReadOnlyCollection<AdminSubscriptionPackageDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminSubscriptionPackagesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminSubscriptionPackageDto>> Handle(
        GetAdminSubscriptionPackagesQuery request,
        CancellationToken cancellationToken)
    {
        var packages = await _dbContext.SubscriptionPackages
            .AsNoTracking()
            .OrderBy(package => package.DisplayOrder)
            .ThenBy(package => package.Price)
            .ToListAsync(cancellationToken);

        return packages
            .Select(package => new AdminSubscriptionPackageDto(
                package.Id,
                package.Name,
                package.Description,
                package.TotalUseCount,
                package.Price,
                package.ValidityDays,
                package.DailyUseLimit,
                package.AllowedCategoryIds,
                package.DisplayOrder,
                package.IsEnabled))
            .ToList();
    }
}

/// <summary>Creates a subscription package when <see cref="Id"/> is empty, otherwise updates it.</summary>
public record UpsertSubscriptionPackageCommand(
    Guid Id,
    string Name,
    string? Description,
    int TotalUseCount,
    decimal Price,
    int ValidityDays,
    int DailyUseLimit,
    string? AllowedCategoryIds,
    int DisplayOrder = 0,
    bool IsEnabled = true) : IRequest<AdminSubscriptionPackageDto>;

public class UpsertSubscriptionPackageCommandValidator : AbstractValidator<UpsertSubscriptionPackageCommand>
{
    public UpsertSubscriptionPackageCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.TotalUseCount).InclusiveBetween(1, 100_000);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ValidityDays).InclusiveBetween(1, 3650);
        RuleFor(command => command.DailyUseLimit).InclusiveBetween(0, 1000);
        RuleFor(command => command.AllowedCategoryIds).MaximumLength(2000);
        RuleFor(command => command.DisplayOrder).InclusiveBetween(-9999, 9999);
    }
}

public class UpsertSubscriptionPackageCommandHandler
    : IRequestHandler<UpsertSubscriptionPackageCommand, AdminSubscriptionPackageDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertSubscriptionPackageCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminSubscriptionPackageDto> Handle(
        UpsertSubscriptionPackageCommand request,
        CancellationToken cancellationToken)
    {
        SubscriptionPackage package;
        if (request.Id == Guid.Empty)
        {
            package = new SubscriptionPackage(
                request.Name,
                request.Description,
                request.TotalUseCount,
                request.Price,
                request.ValidityDays,
                request.DailyUseLimit,
                request.AllowedCategoryIds,
                request.DisplayOrder,
                request.IsEnabled);
            _dbContext.SubscriptionPackages.Add(package);
        }
        else
        {
            package = await _dbContext.SubscriptionPackages
                .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Subscription package was not found.");

            package.Update(
                request.Name,
                request.Description,
                request.TotalUseCount,
                request.Price,
                request.ValidityDays,
                request.DailyUseLimit,
                request.AllowedCategoryIds,
                request.DisplayOrder,
                request.IsEnabled);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminSubscriptionPackageDto(
            package.Id,
            package.Name,
            package.Description,
            package.TotalUseCount,
            package.Price,
            package.ValidityDays,
            package.DailyUseLimit,
            package.AllowedCategoryIds,
            package.DisplayOrder,
            package.IsEnabled);
    }
}

public record DeleteSubscriptionPackageCommand(Guid Id) : IRequest;

public class DeleteSubscriptionPackageCommandHandler : IRequestHandler<DeleteSubscriptionPackageCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteSubscriptionPackageCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteSubscriptionPackageCommand request, CancellationToken cancellationToken)
    {
        var package = await _dbContext.SubscriptionPackages
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription package was not found.");

        // Packages that were already sold are kept for history; they can only
        // be disabled, never hard-deleted.
        var hasSubscriptions = await _dbContext.CustomerSubscriptions
            .AnyAsync(subscription => subscription.PackageId == package.Id, cancellationToken);
        if (hasSubscriptions)
        {
            throw new Domain.Exceptions.DomainException(
                "این بسته فروخته شده است و قابل حذف نیست؛ به جای حذف، آن را غیرفعال کنید.");
        }

        _dbContext.SubscriptionPackages.Remove(package);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
