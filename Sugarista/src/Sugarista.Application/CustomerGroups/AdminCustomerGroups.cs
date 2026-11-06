using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.CustomerGroups;

public record AdminCustomerGroupDto(
    Guid Id,
    string Name,
    string? Description,
    decimal DiscountPercent,
    CustomerGroupApprovalMethod ApprovalMethod,
    string? EmailDomain,
    int MembershipValidityDays,
    int DisplayOrder,
    bool IsEnabled,
    int ActiveMemberCount);

public record AdminCustomerGroupMembershipDto(
    Guid GroupId,
    string GroupName,
    decimal DiscountPercent,
    CustomerGroupMembershipSource Source,
    DateTimeOffset StartedAt,
    DateTimeOffset? ExpiresAt,
    bool IsActive);

public record GetCustomerGroupMembershipsQuery(Guid CustomerId)
    : IRequest<IReadOnlyCollection<AdminCustomerGroupMembershipDto>>;

public class GetCustomerGroupMembershipsQueryHandler
    : IRequestHandler<GetCustomerGroupMembershipsQuery, IReadOnlyCollection<AdminCustomerGroupMembershipDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCustomerGroupMembershipsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminCustomerGroupMembershipDto>> Handle(
        GetCustomerGroupMembershipsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await _dbContext.CustomerGroupMemberships
            .AsNoTracking()
            .Include(membership => membership.Group)
            .Where(membership => membership.CustomerId == request.CustomerId)
            .OrderBy(membership => membership.Group.DisplayOrder)
            .ToListAsync(cancellationToken);

        return rows
            .Select(membership => new AdminCustomerGroupMembershipDto(
                membership.GroupId,
                membership.Group.Name,
                membership.Group.DiscountPercent,
                membership.Source,
                membership.StartedAt,
                membership.ExpiresAt,
                membership.ExpiresAt == null || membership.ExpiresAt > now))
            .ToList();
    }
}

public record GetAdminCustomerGroupsQuery : IRequest<IReadOnlyCollection<AdminCustomerGroupDto>>;

public class GetAdminCustomerGroupsQueryHandler
    : IRequestHandler<GetAdminCustomerGroupsQuery, IReadOnlyCollection<AdminCustomerGroupDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminCustomerGroupsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminCustomerGroupDto>> Handle(
        GetAdminCustomerGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var groups = await _dbContext.CustomerGroups
            .AsNoTracking()
            .OrderBy(group => group.DisplayOrder)
            .ThenBy(group => group.Name)
            .ToListAsync(cancellationToken);

        var memberCounts = await _dbContext.CustomerGroupMemberships
            .AsNoTracking()
            .Where(membership => membership.ExpiresAt == null || membership.ExpiresAt > now)
            .GroupBy(membership => membership.GroupId)
            .Select(group => new { GroupId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.GroupId, entry => entry.Count, cancellationToken);

        return groups
            .Select(group => new AdminCustomerGroupDto(
                group.Id,
                group.Name,
                group.Description,
                group.DiscountPercent,
                group.ApprovalMethod,
                group.EmailDomain,
                group.MembershipValidityDays,
                group.DisplayOrder,
                group.IsEnabled,
                memberCounts.GetValueOrDefault(group.Id)))
            .ToList();
    }
}

/// <summary>Creates a customer group when <see cref="Id"/> is empty, otherwise updates it.</summary>
public record UpsertCustomerGroupCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal DiscountPercent,
    CustomerGroupApprovalMethod ApprovalMethod,
    string? EmailDomain,
    int MembershipValidityDays,
    int DisplayOrder = 0,
    bool IsEnabled = true) : IRequest<AdminCustomerGroupDto>;

public class UpsertCustomerGroupCommandValidator : AbstractValidator<UpsertCustomerGroupCommand>
{
    public UpsertCustomerGroupCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.DiscountPercent).InclusiveBetween(0, 100);
        RuleFor(command => command.MembershipValidityDays).InclusiveBetween(0, 3650);
        RuleFor(command => command.EmailDomain).MaximumLength(150);
        RuleFor(command => command.DisplayOrder).InclusiveBetween(-9999, 9999);
        RuleFor(command => command.EmailDomain)
            .NotEmpty()
            .When(command => command.ApprovalMethod == CustomerGroupApprovalMethod.CorporateEmailDomain)
            .WithMessage("برای تایید با دامنه ایمیل، دامنهٔ سازمانی الزامی است.");
    }
}

public class UpsertCustomerGroupCommandHandler
    : IRequestHandler<UpsertCustomerGroupCommand, AdminCustomerGroupDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertCustomerGroupCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminCustomerGroupDto> Handle(
        UpsertCustomerGroupCommand request,
        CancellationToken cancellationToken)
    {
        CustomerGroup group;
        if (request.Id == Guid.Empty)
        {
            group = new CustomerGroup(
                request.Name,
                request.Description,
                request.DiscountPercent,
                request.ApprovalMethod,
                request.EmailDomain,
                request.MembershipValidityDays,
                request.DisplayOrder,
                request.IsEnabled);
            _dbContext.CustomerGroups.Add(group);
        }
        else
        {
            group = await _dbContext.CustomerGroups
                .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Customer group was not found.");

            group.Update(
                request.Name,
                request.Description,
                request.DiscountPercent,
                request.ApprovalMethod,
                request.EmailDomain,
                request.MembershipValidityDays,
                request.DisplayOrder,
                request.IsEnabled);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Domain-based groups auto-approve matching corporate emails as soon
        // as the group is saved.
        if (group.ApprovalMethod == CustomerGroupApprovalMethod.CorporateEmailDomain)
        {
            await CustomerGroupService.SweepEmailDomainMembershipsAsync(
                _dbContext, cancellationToken);
        }

        var activeMembers = await _dbContext.CustomerGroupMemberships
            .AsNoTracking()
            .CountAsync(
                membership => membership.GroupId == group.Id &&
                              (membership.ExpiresAt == null || membership.ExpiresAt > DateTimeOffset.UtcNow),
                cancellationToken);

        return new AdminCustomerGroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.DiscountPercent,
            group.ApprovalMethod,
            group.EmailDomain,
            group.MembershipValidityDays,
            group.DisplayOrder,
            group.IsEnabled,
            activeMembers);
    }
}

public record DeleteCustomerGroupCommand(Guid Id) : IRequest;

public class DeleteCustomerGroupCommandHandler : IRequestHandler<DeleteCustomerGroupCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteCustomerGroupCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _dbContext.CustomerGroups
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Customer group was not found.");

        _dbContext.CustomerGroups.Remove(group);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
