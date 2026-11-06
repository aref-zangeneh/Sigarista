using FluentValidation;
using MediatR;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MembershipTiers.Commands.UpsertMembershipTier;

/// <summary>Creates a membership level when <see cref="Id"/> is empty, otherwise updates it.</summary>
public record UpsertMembershipTierCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal MinTotalPaidSpent,
    decimal DiscountPercent,
    int DisplayOrder = 0,
    bool IsEnabled = true,
    int MinPaidOrders = 0,
    decimal? CashbackPercent = null) : IRequest<AdminMembershipTierDto>;

public record AdminMembershipTierDto(
    Guid Id,
    string Name,
    string? Description,
    decimal MinTotalPaidSpent,
    decimal DiscountPercent,
    int DisplayOrder,
    bool IsEnabled,
    int MinPaidOrders,
    decimal? CashbackPercent);

public class UpsertMembershipTierCommandValidator : AbstractValidator<UpsertMembershipTierCommand>
{
    public UpsertMembershipTierCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.MinTotalPaidSpent).GreaterThanOrEqualTo(0);
        RuleFor(command => command.DiscountPercent).InclusiveBetween(0, 100);
        RuleFor(command => command.MinPaidOrders).InclusiveBetween(0, 1_000_000);
        RuleFor(command => command.CashbackPercent)
            .InclusiveBetween(0, 100)
            .When(command => command.CashbackPercent.HasValue);
        RuleFor(command => command.DisplayOrder).InclusiveBetween(-9999, 9999);
    }
}

public static class AdminMembershipTierMapper
{
    public static AdminMembershipTierDto ToDto(MembershipTier tier)
    {
        return new AdminMembershipTierDto(
            tier.Id,
            tier.Name,
            tier.Description,
            tier.MinTotalPaidSpent,
            tier.DiscountPercent,
            tier.DisplayOrder,
            tier.IsEnabled,
            tier.MinPaidOrders,
            tier.CashbackPercent);
    }
}

public class UpsertMembershipTierCommandHandler : IRequestHandler<UpsertMembershipTierCommand, AdminMembershipTierDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertMembershipTierCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminMembershipTierDto> Handle(UpsertMembershipTierCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            var tier = new MembershipTier(
                request.Name,
                request.Description,
                request.MinTotalPaidSpent,
                request.DiscountPercent,
                request.DisplayOrder,
                request.IsEnabled,
                request.MinPaidOrders,
                request.CashbackPercent);
            _dbContext.MembershipTiers.Add(tier);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return AdminMembershipTierMapper.ToDto(tier);
        }

        var existing = await _dbContext.MembershipTiers
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Membership tier was not found.");

        existing.Update(
            request.Name,
            request.Description,
            request.MinTotalPaidSpent,
            request.DiscountPercent,
            request.DisplayOrder,
            request.IsEnabled,
            request.MinPaidOrders,
            request.CashbackPercent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AdminMembershipTierMapper.ToDto(existing);
    }
}
