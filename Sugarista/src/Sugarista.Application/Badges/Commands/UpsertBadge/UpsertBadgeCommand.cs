using FluentValidation;
using MediatR;
using Sugarista.Application.Badges.Dto;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Badges.Commands.UpsertBadge;

/// <summary>
/// Creates a badge when <see cref="Id"/> is empty, otherwise updates it.
/// Earning conditions: lifetime paid orders count and/or total paid spending.
/// </summary>
public record UpsertBadgeCommand(
    Guid Id,
    string Name,
    string? Description,
    string? ImageUrl,
    int? MinPaidOrders,
    decimal? MinTotalSpent,
    bool IsEnabled = true,
    int DisplayOrder = 0) : IRequest<AdminBadgeDto>;

public class UpsertBadgeCommandValidator : AbstractValidator<UpsertBadgeCommand>
{
    public UpsertBadgeCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.MinPaidOrders).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MinTotalSpent).GreaterThanOrEqualTo(0);
        RuleFor(command => command.DisplayOrder).InclusiveBetween(-9999, 9999);
        RuleFor(command => command)
            .Must(command => command.MinPaidOrders.HasValue || command.MinTotalSpent.HasValue)
            .WithMessage("اگر میخواهید تعداد سفارش و مبلغ سفارش هر دو خالی باشند، بج صادر نمیشود.");
    }
}

public class UpsertBadgeCommandHandler : IRequestHandler<UpsertBadgeCommand, AdminBadgeDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertBadgeCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminBadgeDto> Handle(UpsertBadgeCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            var badge = new Badge(
                request.Name,
                request.Description,
                request.ImageUrl,
                request.MinPaidOrders,
                request.MinTotalSpent,
                request.IsEnabled,
                request.DisplayOrder);
            _dbContext.Badges.Add(badge);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return AdminBadgeMapper.ToDto(badge);
        }

        var existing = await _dbContext.Badges
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Badge was not found.");

        existing.Update(
            request.Name,
            request.Description,
            request.ImageUrl,
            request.MinPaidOrders,
            request.MinTotalSpent,
            request.IsEnabled,
            request.DisplayOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AdminBadgeMapper.ToDto(existing);
    }
}
