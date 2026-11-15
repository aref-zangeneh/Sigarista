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
    DateTimeOffset CreatedAt);

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

        return groups
            .Select(group => new SmsContactGroupDto(
                group.Id,
                group.Name,
                group.Description,
                memberCounts.GetValueOrDefault(group.Id),
                group.CreatedAt))
            .ToList();
    }
}

public record UpsertSmsContactGroupCommand(
    Guid Id,
    string Name,
    string? Description) : IRequest<Guid>;

public class UpsertSmsContactGroupCommandValidator : AbstractValidator<UpsertSmsContactGroupCommand>
{
    public UpsertSmsContactGroupCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Description).MaximumLength(400);
    }
}

public class UpsertSmsContactGroupCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpsertSmsContactGroupCommand, Guid>
{
    public async Task<Guid> Handle(UpsertSmsContactGroupCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            var duplicate = await dbContext.SmsContactGroups
                .AnyAsync(group => group.Name == request.Name.Trim(), cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("گروهی با همین نام وجود دارد.");
            }

            var group = new SmsContactGroup(request.Name, request.Description);
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
        var query = dbContext.SmsContactGroupMembers
            .AsNoTracking()
            .Where(member => member.GroupId == request.GroupId)
            .Join(dbContext.SmsContacts,
                member => member.ContactId,
                contact => contact.Id,
                (member, contact) => contact);

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
