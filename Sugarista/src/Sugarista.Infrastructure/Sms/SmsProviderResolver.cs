using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// Resolves providers from the database, decrypts their credentials, records
/// the provider interaction log and answers failover questions from the
/// global SMS policy.
/// </summary>
public sealed class SmsProviderResolver(
    IApplicationDbContext dbContext,
    ISmsCredentialProtector protector,
    AppSetupStore appSetupStore,
    ILogger<SmsProviderResolver> logger) : ISmsProviderResolver
{
    public async Task<SmsProvider?> ResolveDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SmsProviders
            .Include(provider => provider.SenderLines)
            .Where(provider => provider.IsEnabled && provider.IsDefault)
            .OrderByDescending(provider => provider.LastSuccessfulConnectionAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await dbContext.SmsProviders
                .Include(provider => provider.SenderLines)
                .Where(provider => provider.IsEnabled)
                .OrderBy(provider => provider.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SmsProvider?> ResolveByIdAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SmsProviders
            .Include(provider => provider.SenderLines)
            .FirstOrDefaultAsync(provider => provider.Id == providerId, cancellationToken);
    }

    public async Task<SmsProvider?> ResolveFailoverAsync(
        Guid primaryProviderId,
        CancellationToken cancellationToken = default)
    {
        var settings = appSetupStore.GetSmsSettings();
        if (!settings.FailoverEnabled || settings.FailoverProviderId is null)
        {
            return null;
        }

        if (settings.FailoverProviderId == primaryProviderId)
        {
            return null;
        }

        var failover = await ResolveByIdAsync(settings.FailoverProviderId.Value, cancellationToken);
        return failover is { IsEnabled: true } ? failover : null;
    }

    public SmsProviderCredentials Decrypt(SmsProvider provider)
    {
        var defaultLine = provider.SenderLines
            .FirstOrDefault(line => line.IsDefaultLine)?.Number
            ?? provider.SenderLines.FirstOrDefault()?.Number;

        return new SmsProviderCredentials(
            provider.Id,
            provider.Name,
            provider.ProviderType,
            provider.BaseUrl,
            protector.Decrypt(provider.EncryptedApiKey),
            protector.Decrypt(provider.EncryptedUsername),
            protector.Decrypt(provider.EncryptedPassword),
            provider.TimeoutSeconds,
            provider.ExtraSettingsJson,
            defaultLine);
    }

    public async Task RecordLogAsync(
        SmsProvider? provider,
        string action,
        bool success,
        string? detail,
        CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.SmsProviderLogs.Add(new SmsProviderLog(provider?.Id, action, success, detail));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // The provider log must never break the actual SMS flow.
            logger.LogWarning(exception, "Could not persist the SMS provider log ({Action}).", action);
        }
    }
}
