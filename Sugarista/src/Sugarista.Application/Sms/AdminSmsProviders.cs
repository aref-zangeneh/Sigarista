using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

public sealed record SmsSenderLineDto(Guid Id, string Number, string? Label, bool IsDefaultLine);

/// <summary>
/// Provider view for the admin panel. Credential values are NEVER included —
/// only presence flags and short masked previews (e.g. "ab••••••").
/// </summary>
public sealed record SmsProviderDto(
    Guid Id,
    string Name,
    string ProviderType,
    string? BaseUrl,
    bool HasApiKey,
    string? ApiKeyPreview,
    bool HasUsername,
    bool HasPassword,
    int TimeoutSeconds,
    string? ExtraSettingsJson,
    bool IsEnabled,
    bool IsDefault,
    DateTimeOffset? LastSuccessfulConnectionAt,
    string? LastConnectionError,
    DateTimeOffset? LastConnectionErrorAt,
    IReadOnlyList<SmsSenderLineDto> SenderLines,
    bool SupportsBulk,
    bool SupportsCredit,
    bool SupportsPattern,
    bool SupportsMessageStatus,
    int BulkBatchSize,
    bool SupportsDeliveryReport,
    string? DeliveryWebhookSecret);

public record GetSmsProvidersQuery : IRequest<IReadOnlyList<SmsProviderDto>>;

public class GetSmsProvidersQueryHandler(
    IApplicationDbContext dbContext,
    ISmsProviderGatewayFactory gatewayFactory,
    ISmsCredentialProtector protector) : IRequestHandler<GetSmsProvidersQuery, IReadOnlyList<SmsProviderDto>>
{
    public async Task<IReadOnlyList<SmsProviderDto>> Handle(
        GetSmsProvidersQuery request,
        CancellationToken cancellationToken)
    {
        var providers = await dbContext.SmsProviders
            .AsNoTracking()
            .Include(provider => provider.SenderLines)
            .OrderByDescending(provider => provider.IsDefault)
            .ThenBy(provider => provider.Name)
            .ToListAsync(cancellationToken);

        // Capability flags are static per adapter type — a throwaway credential
        // set is enough to ask the factory for a gateway.
        var capabilityCache = new Dictionary<SmsProviderType, ISmsProviderGateway>();
        ISmsProviderGateway GatewayFor(SmsProviderType type)
        {
            if (!capabilityCache.TryGetValue(type, out var gateway))
            {
                gateway = gatewayFactory.Create(new SmsProviderCredentials(
                    Guid.Empty, type.ToString(), type, null, null, null, null, 30, null, null));
                capabilityCache[type] = gateway;
            }

            return gateway;
        }

        return providers
            .Select(provider =>
            {
                var gateway = GatewayFor(provider.ProviderType);
                return new SmsProviderDto(
                    provider.Id,
                    provider.Name,
                    provider.ProviderType.ToString(),
                    provider.BaseUrl,
                    provider.EncryptedApiKey is not null,
                    Mask(protector.Decrypt(provider.EncryptedApiKey)),
                    provider.EncryptedUsername is not null,
                    provider.EncryptedPassword is not null,
                    provider.TimeoutSeconds,
                    provider.ExtraSettingsJson,
                    provider.IsEnabled,
                    provider.IsDefault,
                    provider.LastSuccessfulConnectionAt,
                    provider.LastConnectionError,
                    provider.LastConnectionErrorAt,
                    provider.SenderLines
                        .OrderByDescending(line => line.IsDefaultLine)
                        .ThenBy(line => line.Number)
                        .Select(line => new SmsSenderLineDto(line.Id, line.Number, line.Label, line.IsDefaultLine))
                        .ToList(),
                    gateway.SupportsBulkSend,
                    gateway.SupportsCreditCheck,
                    gateway.SupportsPatternSend,
                    gateway.SupportsMessageStatus,
                    gateway.BulkBatchSize,
                    gateway.SupportsDeliveryReport,
                    provider.DeliveryWebhookSecret);
            })
            .ToList();
    }

    private static string? Mask(string? secret) =>
        string.IsNullOrEmpty(secret) ? null : $"{secret[..Math.Min(2, secret.Length)]}••••••";
}

public sealed record SmsSenderLineInput(string Number, string? Label, bool IsDefault);

public record UpsertSmsProviderCommand(
    Guid Id,
    string Name,
    string ProviderType,
    string? BaseUrl,
    string? ApiKey,
    string? Username,
    string? Password,
    int TimeoutSeconds,
    string? ExtraSettingsJson,
    bool IsEnabled,
    bool IsDefault,
    IReadOnlyList<SmsSenderLineInput> SenderLines,
    string? DeliveryWebhookSecret = null) : IRequest<Guid>;

public class UpsertSmsProviderCommandValidator : AbstractValidator<UpsertSmsProviderCommand>
{
    public UpsertSmsProviderCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.ProviderType).Must(BeKnownProviderType)
            .WithMessage("نوع سرویس‌دهنده پشتیبانی نمی‌شود.");
        RuleFor(command => command.BaseUrl).MaximumLength(300)
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.IsWellFormedUriString(url, UriKind.Absolute))
            .WithMessage("آدرس API نامعتبر است.");
        RuleFor(command => command.TimeoutSeconds).InclusiveBetween(3, 120);
        RuleFor(command => command.DeliveryWebhookSecret).MaximumLength(128);
        RuleFor(command => command)
            .Must(HasValidCredentialsForUpsert)
            .WithMessage("برای ملی پیامک نام کاربری/رمز و برای بقیه کلید API لازم است.");
        RuleFor(command => command.ExtraSettingsJson)
            .Must(json => string.IsNullOrWhiteSpace(json) || IsJson(json)).WithMessage("تنظیمات اضافه باید JSON معتبر باشد.");
    }

    private static bool BeKnownProviderType(string value) =>
        Enum.TryParse<SmsProviderType>(value, ignoreCase: true, out _)
        && value.Equals(Enum.Parse<SmsProviderType>(value, true).ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool HasValidCredentialsForUpsert(UpsertSmsProviderCommand command)
    {
        // On update (Id != Empty), empty credential fields keep the existing stored secrets.
        if (command.Id != Guid.Empty)
        {
            return true;
        }

        if (!Enum.TryParse<SmsProviderType>(command.ProviderType, ignoreCase: true, out var type))
        {
            return true; // Handled by ProviderType rule.
        }

        return type switch
        {
            SmsProviderType.Logging => true,
            SmsProviderType.Melipayamak => !string.IsNullOrWhiteSpace(command.Username)
                                           && !string.IsNullOrWhiteSpace(command.Password),
            _ => !string.IsNullOrWhiteSpace(command.ApiKey),
        };
    }

    private static bool IsJson(string value)
    {
        try
        {
            _ = System.Text.Json.JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Creates (Id = empty) or updates a provider. Empty credential fields KEEP
/// the stored values on update — the panel never echoes the real secret back.
/// Every credential change is audited.
/// </summary>
public class UpsertSmsProviderCommandHandler(
    IApplicationDbContext dbContext,
    ISmsCredentialProtector protector,
    IAuditLog auditLog) : IRequestHandler<UpsertSmsProviderCommand, Guid>
{
    public async Task<Guid> Handle(UpsertSmsProviderCommand request, CancellationToken cancellationToken)
    {
        var providerType = Enum.Parse<SmsProviderType>(request.ProviderType, ignoreCase: true);

        SmsProvider provider;
        if (request.Id == Guid.Empty)
        {
            // Melipayamak authenticates with username/password — no API key.
            if (string.IsNullOrWhiteSpace(request.ApiKey) && providerType != SmsProviderType.Logging
                && providerType != SmsProviderType.Melipayamak)
            {
                throw new FluentValidation.ValidationException("کلید API برای سرویس‌دهندهٔ جدید الزامی است.");
            }

            provider = new SmsProvider(
                request.Name,
                providerType,
                request.BaseUrl,
                protector.Encrypt(request.ApiKey),
                protector.Encrypt(request.Username),
                protector.Encrypt(request.Password),
                request.TimeoutSeconds,
                request.ExtraSettingsJson,
                request.IsEnabled,
                request.IsDefault);
            // Auto-generate the delivery-webhook secret when none was supplied.
            provider.SetDeliveryWebhookSecret(
                string.IsNullOrWhiteSpace(request.DeliveryWebhookSecret)
                    ? Guid.NewGuid().ToString("N")
                    : request.DeliveryWebhookSecret);
            dbContext.SmsProviders.Add(provider);
        }
        else
        {
            provider = await dbContext.SmsProviders
                .Include(item => item.SenderLines)
                .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
                ?? throw new KeyNotFoundException("سرویس‌دهنده پیدا نشد.");

            var credentialChanged = !string.IsNullOrWhiteSpace(request.ApiKey)
                                    || !string.IsNullOrWhiteSpace(request.Username)
                                    || !string.IsNullOrWhiteSpace(request.Password);

            provider.Update(
                request.Name,
                providerType,
                request.BaseUrl,
                string.IsNullOrWhiteSpace(request.ApiKey) ? provider.EncryptedApiKey : protector.Encrypt(request.ApiKey),
                string.IsNullOrWhiteSpace(request.Username) ? provider.EncryptedUsername : protector.Encrypt(request.Username),
                string.IsNullOrWhiteSpace(request.Password) ? provider.EncryptedPassword : protector.Encrypt(request.Password),
                request.TimeoutSeconds,
                request.ExtraSettingsJson,
                request.IsEnabled,
                request.IsDefault);

            if (credentialChanged)
            {
                // Credentials are secrets — the audit entry names WHAT changed, never the value.
                auditLog.Write("sms.provider.credentials-changed", $"Provider '{provider.Name}' ({provider.Id}).");
            }

            // Empty webhook secret on update = keep the stored one.
            if (!string.IsNullOrWhiteSpace(request.DeliveryWebhookSecret))
            {
                provider.SetDeliveryWebhookSecret(request.DeliveryWebhookSecret);
            }
        }

        if (request.IsDefault)
        {
            var others = await dbContext.SmsProviders
                .Where(item => item.Id != provider.Id && item.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var other in others)
            {
                other.Update(
                    other.Name,
                    other.ProviderType,
                    other.BaseUrl,
                    other.EncryptedApiKey,
                    other.EncryptedUsername,
                    other.EncryptedPassword,
                    other.TimeoutSeconds,
                    other.ExtraSettingsJson,
                    other.IsEnabled,
                    isDefault: false);
            }
        }

        // Sender lines: replace-all semantics (the panel submits the full list).
        // Remove existing rows explicitly and mark new BaseEntity rows as Added so EF Core
        // does not treat client-generated Guid Ids as Modified on existing providers.
        if (request.Id != Guid.Empty && provider.SenderLines.Count > 0)
        {
            dbContext.SmsSenderLines.RemoveRange(provider.SenderLines);
        }
        provider.SenderLines.Clear();
        foreach (var line in request.SenderLines.Take(10))
        {
            if (!string.IsNullOrWhiteSpace(line.Number))
            {
                var entity = new SmsSenderLine(provider.Id, line.Number, line.Label, line.IsDefault);
                provider.SenderLines.Add(entity);
                if (request.Id != Guid.Empty)
                {
                    dbContext.SmsSenderLines.Add(entity);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        auditLog.Write("sms.provider.saved", $"Provider '{provider.Name}' ({provider.Id}), default={provider.IsDefault}.");
        return provider.Id;
    }
}

public record DeleteSmsProviderCommand(Guid Id) : IRequest;

public class DeleteSmsProviderCommandHandler(
    IApplicationDbContext dbContext,
    IAuditLog auditLog) : IRequestHandler<DeleteSmsProviderCommand>
{
    public async Task Handle(DeleteSmsProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = await dbContext.SmsProviders
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("سرویس‌دهنده پیدا نشد.");

        dbContext.SmsProviders.Remove(provider);
        await dbContext.SaveChangesAsync(cancellationToken);
        auditLog.Write("sms.provider.deleted", $"Provider '{provider.Name}' ({provider.Id}).");
    }
}

public record TestSmsProviderConnectionCommand(Guid Id) : IRequest<SmsConnectionTestResult>;

public class TestSmsProviderConnectionCommandHandler(
    IApplicationDbContext dbContext,
    ISmsProviderResolver resolver,
    ISmsProviderGatewayFactory gatewayFactory) : IRequestHandler<TestSmsProviderConnectionCommand, SmsConnectionTestResult>
{
    public async Task<SmsConnectionTestResult> Handle(
        TestSmsProviderConnectionCommand request,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.SmsProviders
            .Include(item => item.SenderLines)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("سرویس‌دهنده پیدا نشد.");

        var gateway = gatewayFactory.Create(resolver.Decrypt(provider));
        var result = await gateway.TestConnectionAsync(cancellationToken);

        if (result.Success)
        {
            provider.MarkConnectionSucceeded(DateTimeOffset.UtcNow);
        }
        else
        {
            provider.MarkConnectionFailed(result.Message, DateTimeOffset.UtcNow);
        }

        await resolver.RecordLogAsync(provider, "test-connection", result.Success, result.Message, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }
}

public record CheckSmsProviderCreditCommand(Guid Id) : IRequest<SmsCreditResult>;

public class CheckSmsProviderCreditCommandHandler(
    IApplicationDbContext dbContext,
    ISmsProviderResolver resolver,
    ISmsProviderGatewayFactory gatewayFactory) : IRequestHandler<CheckSmsProviderCreditCommand, SmsCreditResult>
{
    public async Task<SmsCreditResult> Handle(
        CheckSmsProviderCreditCommand request,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.SmsProviders
            .Include(item => item.SenderLines)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("سرویس‌دهنده پیدا نشد.");

        var gateway = gatewayFactory.Create(resolver.Decrypt(provider));
        if (!gateway.SupportsCreditCheck)
        {
            return new SmsCreditResult(false, null, "این Provider گزارش اعتبار ندارد.");
        }

        var result = await gateway.GetCreditAsync(cancellationToken);
        await resolver.RecordLogAsync(provider, "check-credit", result.Success, result.Error ?? result.Credit?.ToString(), cancellationToken);
        return result;
    }
}
