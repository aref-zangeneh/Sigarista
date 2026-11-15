using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

public sealed record SmsContactGroupDto(
    Guid Id,
    string Name,
    string? Description,
    int MemberCount,
    DateTimeOffset CreatedAt,
    bool IsSmart = false,
    SmsSmartContactFilter? SmartFilter = null,
    string? SmartFilterDescription = null);

public record GetSmsContactGroupsQuery : IRequest<IReadOnlyList<SmsContactGroupDto>>;

public class GetSmsContactGroupsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsContactGroupsQuery, IReadOnlyList<SmsContactGroupDto>>
{
    public async Task<IReadOnlyList<SmsContactGroupDto>> Handle(
        GetSmsContactGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var groups = await dbContext.SmsContactGroups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);

        var memberCounts = await dbContext.SmsContactGroupMembers
            .AsNoTracking()
            .GroupBy(member => member.GroupId)
            .Select(group => new { GroupId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.GroupId, entry => entry.Count, cancellationToken);

        // Smart groups compute their size from the contact filter.
        var smartGroups = groups.Where(group => group.IsSmart).ToList();
        var smartCounts = new Dictionary<Guid, int>();
        foreach (var group in smartGroups)
        {
            var filter = SmsSmartContactFilter.FromJsonOrNull(group.SmartFilterJson) ?? SmsSmartContactFilter.Default;
            smartCounts[group.Id] = await SmartGroupMembers(dbContext, filter, CancellationToken.None)
                .CountAsync(cancellationToken);
        }

        return groups
            .Select(group =>
            {
                var filter = group.IsSmart
                    ? SmsSmartContactFilter.FromJsonOrNull(group.SmartFilterJson) ?? SmsSmartContactFilter.Default
                    : null;
                return new SmsContactGroupDto(
                    group.Id,
                    group.Name,
                    group.Description,
                    group.IsSmart ? smartCounts.GetValueOrDefault(group.Id) : memberCounts.GetValueOrDefault(group.Id),
                    group.CreatedAt,
                    group.IsSmart,
                    filter,
                    filter?.Describe());
            })
            .ToList();
    }

    public static IQueryable<SmsContact> SmartGroupMembers(
        IApplicationDbContext dbContext,
        SmsSmartContactFilter filter,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = filter.AddedWithinDays is > 0
            ? new DateTimeOffset(now.DateTime.Date.AddDays(-filter.AddedWithinDays.Value), TimeSpan.Zero)
            : (DateTimeOffset?)null;

        return dbContext.SmsContacts.AsNoTracking().Where(contact =>
            (filter.IsActive == null || contact.IsActive == filter.IsActive)
            && (filter.MarketingConsent == null || contact.MarketingConsent == filter.MarketingConsent)
            && (cutoff == null || contact.CreatedAt >= cutoff));
    }
}

public record UpsertSmsContactGroupCommand(
    Guid Id,
    string Name,
    string? Description,
    SmsSmartContactFilter? SmartFilter = null) : IRequest<Guid>;

public class UpsertSmsContactGroupCommandValidator : AbstractValidator<UpsertSmsContactGroupCommand>
{
    public UpsertSmsContactGroupCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Description).MaximumLength(400);
        RuleFor(command => command.SmartFilter)
            .Must(filter => filter is null || filter.AddedWithinDays is null or (>= 1 and <= 3650))
            .WithMessage("بازهٔ «اضافه‌شده در روز اخیر» باید بین ۱ تا ۳۶۵۰ روز باشد.");
    }
}

public class UpsertSmsContactGroupCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpsertSmsContactGroupCommand, Guid>
{
    public async Task<Guid> Handle(UpsertSmsContactGroupCommand request, CancellationToken cancellationToken)
    {
        var smartFilterJson = request.SmartFilter is { IsEmpty: false } filter ? filter.ToJson() : null;

        if (request.Id == Guid.Empty)
        {
            var duplicate = await dbContext.SmsContactGroups
                .AnyAsync(group => group.Name == request.Name.Trim(), cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("گروهی با همین نام وجود دارد.");
            }

            var group = new SmsContactGroup(request.Name, request.Description);
            group.SetSmartFilter(smartFilterJson);
            dbContext.SmsContactGroups.Add(group);
            await dbContext.SaveChangesAsync(cancellationToken);
            return group.Id;
        }

        var existing = await dbContext.SmsContactGroups
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("گروه پیدا نشد.");

        var nameDuplicate = await dbContext.SmsContactGroups
            .AnyAsync(group => group.Name == request.Name.Trim() && group.Id != existing.Id, cancellationToken);
        if (nameDuplicate)
        {
            throw new InvalidOperationException("گروهی با همین نام وجود دارد.");
        }

        existing.Update(request.Name, request.Description);
        existing.SetSmartFilter(smartFilterJson);
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }
}

public record DeleteSmsContactGroupCommand(Guid Id) : IRequest;

public class DeleteSmsContactGroupCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<DeleteSmsContactGroupCommand>
{
    public async Task Handle(DeleteSmsContactGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.SmsContactGroups
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("گروه پیدا نشد.");

        dbContext.SmsContactGroups.Remove(group);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SmsGroupMemberDto(Guid ContactId, string? FirstName, string? LastName, string DisplayMobile);

public record GetSmsContactGroupMembersQuery(
    Guid GroupId,
    int Page = 1,
    int PageSize = 50) : IRequest<SmsPagedResult<SmsGroupMemberDto>>;

public class GetSmsContactGroupMembersQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsContactGroupMembersQuery, SmsPagedResult<SmsGroupMemberDto>>
{
    public async Task<SmsPagedResult<SmsGroupMemberDto>> Handle(
        GetSmsContactGroupMembersQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = SmsPagination.Normalize(request.Page, request.PageSize);

        // Smart groups have no membership table — members come from the filter.
        var group = await dbContext.SmsContactGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.GroupId, cancellationToken)
            ?? throw new KeyNotFoundException("گروه پیدا نشد.");

        IQueryable<SmsContact> query;
        if (group.IsSmart)
        {
            var filter = SmsSmartContactFilter.FromJsonOrNull(group.SmartFilterJson) ?? SmsSmartContactFilter.Default;
            query = GetSmsContactGroupsQueryHandler.SmartGroupMembers(dbContext, filter, cancellationToken);
        }
        else
        {
            query = dbContext.SmsContactGroupMembers
                .AsNoTracking()
                .Where(member => member.GroupId == request.GroupId)
                .Join(dbContext.SmsContacts,
                    member => member.ContactId,
                    contact => contact.Id,
                    (member, contact) => contact);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(contact => contact.LastName)
            .ThenBy(contact => contact.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new SmsPagedResult<SmsGroupMemberDto>(
            items
                .Select(contact => new SmsGroupMemberDto(
                    contact.Id,
                    contact.FirstName,
                    contact.LastName,
                    SmsTextUtilities.ToDisplayMobile(contact.Mobile)))
                .ToList(),
            total,
            page,
            pageSize);
    }
}
