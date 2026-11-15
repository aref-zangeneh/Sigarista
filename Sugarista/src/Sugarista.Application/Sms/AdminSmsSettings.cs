using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

public record GetSmsSettingsQuery : IRequest<SmsGlobalSettings>;

public class GetSmsSettingsQueryHandler(ISmsSettingsStore settingsStore)
    : IRequestHandler<GetSmsSettingsQuery, SmsGlobalSettings>
{
    public Task<SmsGlobalSettings> Handle(GetSmsSettingsQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(settingsStore.Get());
}

public record UpdateSmsSettingsCommand(
    int MaxRecipientsPerSend,
    int BatchSize,
    int MaxRetries,
    int RetryDelaySeconds,
    int DailyMessageLimit,
    bool BulkEnabled,
    bool FailoverEnabled,
    Guid? FailoverProviderId,
    int HistoryRetentionDays) : IRequest<SmsGlobalSettings>;

public class UpdateSmsSettingsCommandValidator : AbstractValidator<UpdateSmsSettingsCommand>
{
    public UpdateSmsSettingsCommandValidator()
    {
        RuleFor(command => command.MaxRecipientsPerSend).InclusiveBetween(1, 100_000);
        RuleFor(command => command.BatchSize).InclusiveBetween(1, 500);
        RuleFor(command => command.MaxRetries).InclusiveBetween(0, 5);
        RuleFor(command => command.RetryDelaySeconds).InclusiveBetween(1, 600);
        RuleFor(command => command.DailyMessageLimit).InclusiveBetween(1, 100_000);
        RuleFor(command => command.HistoryRetentionDays).InclusiveBetween(7, 730);
    }
}

public class UpdateSmsSettingsCommandHandler(
    ISmsSettingsStore settingsStore,
    IApplicationDbContext dbContext,
    IAuditLog auditLog) : IRequestHandler<UpdateSmsSettingsCommand, SmsGlobalSettings>
{
    public async Task<SmsGlobalSettings> Handle(
        UpdateSmsSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FailoverEnabled && request.FailoverProviderId is { } failoverId)
        {
            var failoverProvider = await dbContext.SmsProviders
                .AsNoTracking()
                .FirstOrDefaultAsync(provider => provider.Id == failoverId, cancellationToken)
                ?? throw new InvalidOperationException("سرویس‌دهندهٔ جایگزین انتخاب‌شده وجود ندارد.");
            if (!failoverProvider.IsEnabled)
            {
                throw new InvalidOperationException("سرویس‌دهندهٔ جایگزین انتخاب‌شده غیرفعال است.");
            }
        }

        var settings = new SmsGlobalSettings(
            request.MaxRecipientsPerSend,
            request.BatchSize,
            request.MaxRetries,
            request.RetryDelaySeconds,
            request.DailyMessageLimit,
            request.BulkEnabled,
            request.FailoverEnabled,
            request.FailoverProviderId,
            request.HistoryRetentionDays);

        settingsStore.Save(settings);
        auditLog.Write(
            "sms.settings.updated",
            $"bulk={settings.BulkEnabled}, failover={settings.FailoverEnabled}, dailyLimit={settings.DailyMessageLimit}.");
        return settings;
    }
}

/// <summary>Prunes SmsMessage/ProviderLog rows older than the retention window. Called manually from the panel.</summary>
public record PruneSmsHistoryCommand : IRequest<int>;

public class PruneSmsHistoryCommandHandler(
    IApplicationDbContext dbContext,
    ISmsSettingsStore settingsStore,
    IAuditLog auditLog) : IRequestHandler<PruneSmsHistoryCommand, int>
{
    public async Task<int> Handle(PruneSmsHistoryCommand request, CancellationToken cancellationToken)
    {
        var settings = settingsStore.Get();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-settings.HistoryRetentionDays);

        var oldMessages = dbContext.SmsMessages.Where(message => message.RequestedAt < cutoff);
        var oldLogs = dbContext.SmsProviderLogs
            .Where(log => log.CreatedAt < cutoff);

        var removed = await oldMessages.CountAsync(cancellationToken) + await oldLogs.CountAsync(cancellationToken);

        dbContext.SmsMessages.RemoveRange(await oldMessages.Take(5000).ToListAsync(cancellationToken));
        dbContext.SmsProviderLogs.RemoveRange(await oldLogs.Take(5000).ToListAsync(cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);

        auditLog.Write("sms.history.pruned", $"Retention {settings.HistoryRetentionDays}d; removed rows (this pass, capped 5000/table).");
        return removed;
    }
}
