using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

public sealed record SmsContactDto(
    Guid Id,
    string? FirstName,
    string? LastName,
    string DisplayMobile,
    string? Email,
    string? Notes,
    bool IsActive,
    bool MarketingConsent,
    IReadOnlyList<Guid> GroupIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastModifiedAt);

public record GetSmsContactsQuery(
    string? Search,
    Guid? GroupId,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20) : IRequest<SmsPagedResult<SmsContactDto>>;

public class GetSmsContactsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsContactsQuery, SmsPagedResult<SmsContactDto>>
{
    public async Task<SmsPagedResult<SmsContactDto>> Handle(
        GetSmsContactsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = SmsPagination.Normalize(request.Page, request.PageSize);

        var query = dbContext.SmsContacts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var normalized = SmsTextUtilities.NormalizeMobile(search);
            var hasNormalized = normalized is not null;
            query = query.Where(contact =>
                (contact.FirstName != null && contact.FirstName.Contains(search))
                || (contact.LastName != null && contact.LastName.Contains(search))
                || (hasNormalized && contact.Mobile == normalized)
                || contact.Mobile.Contains(search));
        }

        if (request.GroupId is { } groupId)
        {
            query = query
                .Where(contact => dbContext.SmsContactGroupMembers
                    .Any(member => member.ContactId == contact.Id && member.GroupId == groupId));
        }

        if (request.IsActive is { } isActive)
        {
            query = query.Where(contact => contact.IsActive == isActive);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(contact => contact.LastName)
            .ThenBy(contact => contact.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var contactIds = items.Select(contact => contact.Id).ToList();
        var groupMap = await dbContext.SmsContactGroupMembers
            .AsNoTracking()
            .Where(member => contactIds.Contains(member.ContactId))
            .GroupBy(member => member.ContactId)
            .ToDictionaryAsync(
                group => group.Key,
                group => group.Select(member => member.GroupId).ToList(),
                cancellationToken);

        return new SmsPagedResult<SmsContactDto>(
            items
                .Select(contact => new SmsContactDto(
                    contact.Id,
                    contact.FirstName,
                    contact.LastName,
                    SmsTextUtilities.ToDisplayMobile(contact.Mobile),
                    contact.Email,
                    contact.Notes,
                    contact.IsActive,
                    contact.MarketingConsent,
                    groupMap.GetValueOrDefault(contact.Id, []),
                    contact.CreatedAt,
                    contact.LastModifiedAt))
                .ToList(),
            total,
            page,
            pageSize);
    }
}

public record UpsertSmsContactCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    string Mobile,
    string? Email,
    string? Notes,
    bool IsActive,
    bool MarketingConsent,
    IReadOnlyList<Guid> GroupIds) : IRequest<Guid>;

public class UpsertSmsContactCommandValidator : AbstractValidator<UpsertSmsContactCommand>
{
    public UpsertSmsContactCommandValidator()
    {
        RuleFor(command => command.Mobile)
            .NotEmpty()
            .Must(mobile => SmsTextUtilities.NormalizeMobile(mobile) is not null)
            .WithMessage("شماره موبایل نامعتبر است.");
        RuleFor(command => command.FirstName).MaximumLength(80);
        RuleFor(command => command.LastName).MaximumLength(80);
        RuleFor(command => command.Email).MaximumLength(160).EmailAddress()
            .When(command => !string.IsNullOrWhiteSpace(command.Email))
            .WithMessage("ایمیل نامعتبر است.");
        RuleFor(command => command.Notes).MaximumLength(500);
    }
}

public class UpsertSmsContactCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpsertSmsContactCommand, Guid>
{
    public async Task<Guid> Handle(UpsertSmsContactCommand request, CancellationToken cancellationToken)
    {
        var mobile = SmsTextUtilities.NormalizeMobile(request.Mobile)!;

        SmsContact contact;
        if (request.Id == Guid.Empty)
        {
            var duplicate = await dbContext.SmsContacts
                .AnyAsync(item => item.Mobile == mobile, cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("این شماره قبلاً در دفترچه ثبت شده است.");
            }

            contact = new SmsContact(
                request.FirstName, request.LastName, mobile, request.Email, request.Notes,
                request.IsActive, request.MarketingConsent);
            dbContext.SmsContacts.Add(contact);
        }
        else
        {
            contact = await dbContext.SmsContacts
                .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
                ?? throw new KeyNotFoundException("مخاطب پیدا نشد.");

            var duplicate = await dbContext.SmsContacts
                .AnyAsync(item => item.Mobile == mobile && item.Id != contact.Id, cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("شمارهٔ دیگری با همین موبایل ثبت شده است.");
            }

            contact.Update(
                request.FirstName, request.LastName, mobile, request.Email, request.Notes,
                request.IsActive, request.MarketingConsent);

            var existingGroups = await dbContext.SmsContactGroupMembers
                .Where(member => member.ContactId == contact.Id)
                .ToListAsync(cancellationToken);
            dbContext.SmsContactGroupMembers.RemoveRange(existingGroups);
        }

        foreach (var groupId in request.GroupIds.Distinct())
        {
            var groupExists = await dbContext.SmsContactGroups
                .AnyAsync(group => group.Id == groupId, cancellationToken);
            if (groupExists)
            {
                dbContext.SmsContactGroupMembers.Add(new SmsContactGroupMember(groupId, contact.Id));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return contact.Id;
    }
}

public record DeleteSmsContactCommand(Guid Id) : IRequest;

public class DeleteSmsContactCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<DeleteSmsContactCommand>
{
    public async Task Handle(DeleteSmsContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await dbContext.SmsContacts
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("مخاطب پیدا نشد.");

        dbContext.SmsContacts.Remove(contact);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SmsContactImportReport(
    int Added,
    int Updated,
    int Duplicates,
    int Invalid,
    IReadOnlyList<string> InvalidRows);

/// <summary>
/// CSV import: each line `mobile[,firstName[,lastName[,email[,notes]]]]`.
/// Persian digits and +98 formats accepted; duplicates update rather than
/// re-add (unique index on normalized mobile).
/// </summary>
public record ImportSmsContactsCommand(string CsvContent, bool MarketingConsentDefault = false)
    : IRequest<SmsContactImportReport>;

public class ImportSmsContactsCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ImportSmsContactsCommand, SmsContactImportReport>
{
    public async Task<SmsContactImportReport> Handle(
        ImportSmsContactsCommand request,
        CancellationToken cancellationToken)
    {
        var added = 0;
        var updated = 0;
        var duplicates = 0;
        var invalidRows = new List<string>();
        var lines = request.CsvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var seen = new HashSet<string>();
        foreach (var rawLine in lines.SkipWhile(line => line.TrimStart().StartsWith("mobile", StringComparison.OrdinalIgnoreCase)))
        {
            var parts = rawLine.Trim().Split(',');
            var mobile = SmsTextUtilities.NormalizeMobile(parts.Length > 0 ? parts[0] : null);
            if (mobile is null)
            {
                invalidRows.Add(rawLine.Length > 60 ? rawLine[..60] : rawLine);
                continue;
            }

            if (!seen.Add(mobile))
            {
                duplicates++;
                continue;
            }

            var existing = await dbContext.SmsContacts
                .FirstOrDefaultAsync(contact => contact.Mobile == mobile, cancellationToken);
            if (existing is null)
            {
                dbContext.SmsContacts.Add(new SmsContact(
                    parts.Length > 1 ? parts[1] : null,
                    parts.Length > 2 ? parts[2] : null,
                    mobile,
                    parts.Length > 3 ? parts[3] : null,
                    parts.Length > 4 ? parts[4] : null,
                    isActive: true,
                    request.MarketingConsentDefault));
                added++;
            }
            else
            {
                existing.Update(
                    parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : existing.FirstName,
                    parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : existing.LastName,
                    mobile,
                    parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : existing.Email,
                    existing.Notes,
                    existing.IsActive,
                    existing.MarketingConsent || request.MarketingConsentDefault);
                updated++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new SmsContactImportReport(added, updated, duplicates, invalidRows.Count, invalidRows.Take(50).ToList());
    }
}

public sealed record SmsExportRow(
    string Mobile,
    string? FirstName,
    string? LastName,
    string? Email,
    bool IsActive,
    bool MarketingConsent);

public record ExportSmsContactsQuery : IRequest<string>;

public class ExportSmsContactsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ExportSmsContactsQuery, string>
{
    public async Task<string> Handle(ExportSmsContactsQuery request, CancellationToken cancellationToken)
    {
        var contacts = await dbContext.SmsContacts
            .AsNoTracking()
            .OrderBy(contact => contact.LastName)
            .ThenBy(contact => contact.FirstName)
            .ToListAsync(cancellationToken);

        var builder = new StringBuilder("mobile,firstName,lastName,email,isActive,marketingConsent\n");
        foreach (var contact in contacts)
        {
            builder.Append(SmsTextUtilities.ToDisplayMobile(contact.Mobile)).Append(',');
            builder.Append(contact.FirstName?.Replace(',', ' ')).Append(',');
            builder.Append(contact.LastName?.Replace(',', ' ')).Append(',');
            builder.Append(contact.Email).Append(',');
            builder.Append(contact.IsActive ? "1" : "0").Append(',');
            builder.Append(contact.MarketingConsent ? "1" : "0").Append('\n');
        }

        return builder.ToString();
    }
}

public record AddContactsToGroupsCommand(
    IReadOnlyList<Guid> ContactIds,
    IReadOnlyList<Guid> GroupIds) : IRequest<int>;

public class AddContactsToGroupsCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<AddContactsToGroupsCommand, int>
{
    public async Task<int> Handle(AddContactsToGroupsCommand request, CancellationToken cancellationToken)
    {
        var existingPairs = await dbContext.SmsContactGroupMembers
            .Where(member => request.GroupIds.Contains(member.GroupId)
                             && request.ContactIds.Contains(member.ContactId))
            .Select(member => new { member.GroupId, member.ContactId })
            .ToListAsync(cancellationToken);
        var existingSet = existingPairs
            .Select(pair => (pair.GroupId, pair.ContactId))
            .ToHashSet();

        var validGroups = await dbContext.SmsContactGroups
            .Where(group => request.GroupIds.Contains(group.Id))
            .Select(group => group.Id)
            .ToListAsync(cancellationToken);

        var added = 0;
        foreach (var groupId in validGroups)
        {
            foreach (var contactId in request.ContactIds.Distinct())
            {
                if (existingSet.Add((groupId, contactId)))
                {
                    dbContext.SmsContactGroupMembers.Add(new SmsContactGroupMember(groupId, contactId));
                    added++;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return added;
    }
}

public record RemoveContactsFromGroupsCommand(
    IReadOnlyList<Guid> ContactIds,
    IReadOnlyList<Guid> GroupIds) : IRequest<int>;

public class RemoveContactsFromGroupsCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<RemoveContactsFromGroupsCommand, int>
{
    public async Task<int> Handle(RemoveContactsFromGroupsCommand request, CancellationToken cancellationToken)
    {
        var memberships = await dbContext.SmsContactGroupMembers
            .Where(member => request.GroupIds.Contains(member.GroupId)
                             && request.ContactIds.Contains(member.ContactId))
            .ToListAsync(cancellationToken);

        dbContext.SmsContactGroupMembers.RemoveRange(memberships);
        await dbContext.SaveChangesAsync(cancellationToken);
        return memberships.Count;
    }
}

// ---- Opt-outs -----------------------------------------------------------

public sealed record SmsOptOutDto(Guid Id, string DisplayMobile, string? Reason, DateTimeOffset CreatedAt);

public record GetSmsOptOutsQuery(int Page = 1, int PageSize = 50) : IRequest<SmsPagedResult<SmsOptOutDto>>;

public class GetSmsOptOutsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsOptOutsQuery, SmsPagedResult<SmsOptOutDto>>
{
    public async Task<SmsPagedResult<SmsOptOutDto>> Handle(
        GetSmsOptOutsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = SmsPagination.Normalize(request.Page, request.PageSize);
        var query = dbContext.SmsOptOuts.AsNoTracking();

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(optOut => optOut.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new SmsPagedResult<SmsOptOutDto>(
            items
                .Select(optOut => new SmsOptOutDto(
                    optOut.Id,
                    SmsTextUtilities.ToDisplayMobile(optOut.Mobile),
                    optOut.Reason,
                    optOut.CreatedAt))
                .ToList(),
            total,
            page,
            pageSize);
    }
}

public record AddSmsOptOutCommand(string Mobile, string? Reason) : IRequest<Guid>;

public class AddSmsOptOutCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<AddSmsOptOutCommand, Guid>
{
    public async Task<Guid> Handle(AddSmsOptOutCommand request, CancellationToken cancellationToken)
    {
        var mobile = SmsTextUtilities.NormalizeMobile(request.Mobile)
                     ?? throw new InvalidOperationException("شماره موبایل نامعتبر است.");

        var existing = await dbContext.SmsOptOuts
            .FirstOrDefaultAsync(optOut => optOut.Mobile == mobile, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var optOut = new SmsOptOut(mobile, request.Reason);
        dbContext.SmsOptOuts.Add(optOut);
        await dbContext.SaveChangesAsync(cancellationToken);
        return optOut.Id;
    }
}

public record RemoveSmsOptOutCommand(Guid Id) : IRequest;

public class RemoveSmsOptOutCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<RemoveSmsOptOutCommand>
{
    public async Task Handle(RemoveSmsOptOutCommand request, CancellationToken cancellationToken)
    {
        var optOut = await dbContext.SmsOptOuts
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("این شماره در لیست انصراف نیست.");

        dbContext.SmsOptOuts.Remove(optOut);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
