using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Discounts.Security;

public sealed record DiscountBlacklistEntryDto(
    Guid Id,
    string Kind,
    string Value,
    string? Reason,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record DiscountAttemptDto(
    Guid Id,
    string Identifier,
    string? Code,
    bool Succeeded,
    string? IpAddress,
    DateTimeOffset AttemptedAt);

public record GetDiscountBlacklistQuery : IRequest<IReadOnlyCollection<DiscountBlacklistEntryDto>>;

public class GetDiscountBlacklistQueryHandler
    : IRequestHandler<GetDiscountBlacklistQuery, IReadOnlyCollection<DiscountBlacklistEntryDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetDiscountBlacklistQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DiscountBlacklistEntryDto>> Handle(
        GetDiscountBlacklistQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.BlacklistEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.IsActive)
            .ThenByDescending(entry => entry.CreatedAt)
            .Select(entry => new DiscountBlacklistEntryDto(
                entry.Id,
                entry.Kind.ToString(),
                entry.Value,
                entry.Reason,
                entry.IsActive,
                entry.CreatedAt,
                entry.LastModifiedAt))
            .ToListAsync(cancellationToken);
    }
}

public record AddDiscountBlacklistEntryCommand(
    string Kind,
    string Value,
    string? Reason = null) : IRequest<Guid>;

public class AddDiscountBlacklistEntryCommandValidator : AbstractValidator<AddDiscountBlacklistEntryCommand>
{
    public AddDiscountBlacklistEntryCommandValidator()
    {
        RuleFor(command => command.Kind)
            .Must(kind => Enum.TryParse<BlacklistKind>(kind, true, out _))
            .WithMessage("نوع ورودی لیست سیاه باید Phone یا IpAddress باشد.");
        RuleFor(command => command.Value).NotEmpty().MaximumLength(64);
        RuleFor(command => command.Reason).MaximumLength(500);
    }
}

public class AddDiscountBlacklistEntryCommandHandler : IRequestHandler<AddDiscountBlacklistEntryCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public AddDiscountBlacklistEntryCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(
        AddDiscountBlacklistEntryCommand request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BlacklistKind>(request.Kind, true, out var kind))
        {
            throw new DomainException("نوع ورودی لیست سیاه معتبر نیست.");
        }

        var value = kind == BlacklistKind.Phone
            ? DiscountAbuseGuard.NormalizeIdentifier(request.Value)
            : request.Value.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(kind == BlacklistKind.Phone
                ? "شمارهٔ تلفن معتبر نیست."
                : "آدرس IP معتبر نیست.");
        }

        var existing = await _dbContext.BlacklistEntries
            .FirstOrDefaultAsync(entry => entry.Value == value, cancellationToken);

        if (existing is not null)
        {
            existing.Activate();
            existing.UpdateReason(request.Reason);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var created = new BlacklistEntry(kind, value, request.Reason);
        _dbContext.BlacklistEntries.Add(created);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return created.Id;
    }
}

public record SetDiscountBlacklistEntryActiveCommand(Guid Id, bool IsActive) : IRequest;

public class SetDiscountBlacklistEntryActiveCommandHandler : IRequestHandler<SetDiscountBlacklistEntryActiveCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public SetDiscountBlacklistEntryActiveCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        SetDiscountBlacklistEntryActiveCommand request,
        CancellationToken cancellationToken)
    {
        var entry = await _dbContext.BlacklistEntries
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Blacklist entry was not found.");

        if (request.IsActive)
        {
            entry.Activate();
        }
        else
        {
            entry.Deactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record DeleteDiscountBlacklistEntryCommand(Guid Id) : IRequest;

public class DeleteDiscountBlacklistEntryCommandHandler : IRequestHandler<DeleteDiscountBlacklistEntryCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteDiscountBlacklistEntryCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        DeleteDiscountBlacklistEntryCommand request,
        CancellationToken cancellationToken)
    {
        var entry = await _dbContext.BlacklistEntries
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Blacklist entry was not found.");

        _dbContext.BlacklistEntries.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record GetDiscountAttemptsQuery(string? Identifier = null, int Take = 100)
    : IRequest<IReadOnlyCollection<DiscountAttemptDto>>;

public class GetDiscountAttemptsQueryHandler
    : IRequestHandler<GetDiscountAttemptsQuery, IReadOnlyCollection<DiscountAttemptDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetDiscountAttemptsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DiscountAttemptDto>> Handle(
        GetDiscountAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.DiscountAttempts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Identifier))
        {
            var identifier = DiscountAbuseGuard.NormalizeIdentifier(request.Identifier);
            query = query.Where(attempt => attempt.Identifier == identifier);
        }

        return await query
            .OrderByDescending(attempt => attempt.AttemptedAt)
            .Take(Math.Clamp(request.Take, 1, 500))
            .Select(attempt => new DiscountAttemptDto(
                attempt.Id,
                attempt.Identifier,
                attempt.Code,
                attempt.Succeeded,
                attempt.IpAddress,
                attempt.AttemptedAt))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Clears recorded failures for a phone/user so the lockout is lifted.</summary>
public record ClearDiscountAttemptsCommand(string? Identifier = null) : IRequest<int>;

public class ClearDiscountAttemptsCommandHandler : IRequestHandler<ClearDiscountAttemptsCommand, int>
{
    private readonly IApplicationDbContext _dbContext;

    public ClearDiscountAttemptsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> Handle(
        ClearDiscountAttemptsCommand request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.DiscountAttempts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Identifier))
        {
            var identifier = DiscountAbuseGuard.NormalizeIdentifier(request.Identifier);
            query = query.Where(attempt => attempt.Identifier == identifier);
        }

        var attempts = await query.ToListAsync(cancellationToken);
        _dbContext.DiscountAttempts.RemoveRange(attempts);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return attempts.Count;
    }
}
