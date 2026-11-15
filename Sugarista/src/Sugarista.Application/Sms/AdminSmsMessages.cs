using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

public sealed record SmsMessageDto(
    Guid Id,
    Guid? ProviderId,
    string? ProviderName,
    string Mobile,
    string DisplayMobile,
    string Body,
    string Kind,
    string SendType,
    Guid? TemplateId,
    Guid? CampaignId,
    int BatchNumber,
    string Status,
    string? ProviderMessageId,
    string? ProviderResponse,
    decimal? Cost,
    int Segments,
    int AttemptNumber,
    string? ErrorMessage,
    DateTimeOffset RequestedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? LastStatusAt,
    string? RequestedBy);

public record GetSmsMessagesQuery(
    string? Mobile,
    Guid? ProviderId,
    string? Status,
    Guid? CampaignId,
    string? Kind,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20) : IRequest<SmsPagedResult<SmsMessageDto>>;

public class GetSmsMessagesQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsMessagesQuery, SmsPagedResult<SmsMessageDto>>
{
    public async Task<SmsPagedResult<SmsMessageDto>> Handle(
        GetSmsMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = SmsPagination.Normalize(request.Page, request.PageSize);

        var query = dbContext.SmsMessages.AsNoTracking();
        query = ApplyFilters(query, request);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(message => message.RequestedAt)
            .ThenByDescending(message => message.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var providerIds = items.Where(message => message.ProviderId is not null)
            .Select(message => message.ProviderId!.Value)
            .Distinct()
            .ToList();
        var providerNames = providerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.SmsProviders
                .AsNoTracking()
                .Where(provider => providerIds.Contains(provider.Id))
                .ToDictionaryAsync(provider => provider.Id, provider => provider.Name, cancellationToken);

        return new SmsPagedResult<SmsMessageDto>(
            items
                .Select(message => new SmsMessageDto(
                    message.Id,
                    message.ProviderId,
                    message.ProviderId is { } id ? providerNames.GetValueOrDefault(id) : null,
                    message.Mobile,
                    SmsTextUtilities.ToDisplayMobile(message.Mobile),
                    message.Body,
                    message.Kind.ToString(),
                    message.SendType.ToString(),
                    message.TemplateId,
                    message.CampaignId,
                    message.BatchNumber,
                    message.Status.ToString(),
                    message.ProviderMessageId,
                    message.ProviderResponse,
                    message.Cost,
                    message.Segments,
                    message.AttemptNumber,
                    message.ErrorMessage,
                    message.RequestedAt,
                    message.SentAt,
                    message.LastStatusAt,
                    message.RequestedBy))
                .ToList(),
            total,
            page,
            pageSize);
    }

    private static IQueryable<SmsMessage> ApplyFilters(IQueryable<SmsMessage> query, GetSmsMessagesQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Mobile))
        {
            var normalized = SmsTextUtilities.NormalizeMobile(request.Mobile);
            if (normalized is not null)
            {
                query = query.Where(message => message.Mobile == normalized);
            }
            else
            {
                query = query.Where(message => false);
            }
        }

        if (request.ProviderId is { } providerId)
        {
            query = query.Where(message => message.ProviderId == providerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<SmsSendStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(message => message.Status == status);
        }

        if (request.CampaignId is { } campaignId)
        {
            query = query.Where(message => message.CampaignId == campaignId);
        }

        if (!string.IsNullOrWhiteSpace(request.Kind)
            && Enum.TryParse<SmsMessageKind>(request.Kind, ignoreCase: true, out var kind))
        {
            query = query.Where(message => message.Kind == kind);
        }

        if (request.From is { } from)
        {
            query = query.Where(message => message.RequestedAt >= from);
        }

        if (request.To is { } to)
        {
            query = query.Where(message => message.RequestedAt <= to);
        }

        return query;
    }
}

public record GetSmsMessageQuery(Guid Id) : IRequest<SmsMessageDto>;

public class GetSmsMessageQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsMessageQuery, SmsMessageDto>
{
    public async Task<SmsMessageDto> Handle(GetSmsMessageQuery request, CancellationToken cancellationToken)
    {
        var message = await dbContext.SmsMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("پیامک پیدا نشد.");

        var providerName = message.ProviderId is { } providerId
            ? await dbContext.SmsProviders
                .Where(provider => provider.Id == providerId)
                .Select(provider => provider.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new SmsMessageDto(
            message.Id,
            message.ProviderId,
            providerName,
            message.Mobile,
            SmsTextUtilities.ToDisplayMobile(message.Mobile),
            message.Body,
            message.Kind.ToString(),
            message.SendType.ToString(),
            message.TemplateId,
            message.CampaignId,
            message.BatchNumber,
            message.Status.ToString(),
            message.ProviderMessageId,
            message.ProviderResponse,
            message.Cost,
            message.Segments,
            message.AttemptNumber,
            message.ErrorMessage,
            message.RequestedAt,
            message.SentAt,
            message.LastStatusAt,
            message.RequestedBy);
    }
}

/// <summary>Re-queries the provider for a message's current state (when supported).</summary>
public record RefreshSmsMessageStatusCommand(Guid Id) : IRequest<SmsMessageDto>;

public class RefreshSmsMessageStatusCommandHandler(
    IApplicationDbContext dbContext,
    ISmsProviderResolver resolver,
    ISmsProviderGatewayFactory gatewayFactory) : IRequestHandler<RefreshSmsMessageStatusCommand, SmsMessageDto>
{
    public async Task<SmsMessageDto> Handle(
        RefreshSmsMessageStatusCommand request,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.SmsMessages
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("پیامک پیدا نشد.");

        if (string.IsNullOrWhiteSpace(message.ProviderMessageId) || message.ProviderId is not { } providerId)
        {
            throw new InvalidOperationException("برای این پیامک شناسهٔ Provider ثبت نشده است.");
        }

        var provider = await resolver.ResolveByIdAsync(providerId, cancellationToken)
                       ?? throw new InvalidOperationException("سرویس‌دهندهٔ این پیامک دیگر موجود نیست.");

        var gateway = gatewayFactory.Create(resolver.Decrypt(provider));
        if (!gateway.SupportsMessageStatus)
        {
            throw new InvalidOperationException("این Provider استعلام وضعیت را پشتیبانی نمی‌کند.");
        }

        var statusResult = await gateway.GetMessageStatusAsync(message.ProviderMessageId, cancellationToken);
        if (statusResult.Success)
        {
            var mapped = statusResult.Status switch
            {
                SmsOutcomeStatus.Delivered => SmsSendStatus.Delivered,
                SmsOutcomeStatus.Sent => SmsSendStatus.Sent,
                SmsOutcomeStatus.Accepted => SmsSendStatus.Accepted,
                SmsOutcomeStatus.Rejected => SmsSendStatus.Rejected,
                SmsOutcomeStatus.Failed => SmsSendStatus.Failed,
                _ => SmsSendStatus.Unknown
            };
            message.MarkDelivery(mapped);
        }

        await resolver.RecordLogAsync(provider, "refresh-status", statusResult.Success, statusResult.Error, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SmsMessageDto(
            message.Id,
            message.ProviderId,
            provider.Name,
            message.Mobile,
            SmsTextUtilities.ToDisplayMobile(message.Mobile),
            message.Body,
            message.Kind.ToString(),
            message.SendType.ToString(),
            message.TemplateId,
            message.CampaignId,
            message.BatchNumber,
            message.Status.ToString(),
            message.ProviderMessageId,
            message.ProviderResponse,
            message.Cost,
            message.Segments,
            message.AttemptNumber,
            message.ErrorMessage,
            message.RequestedAt,
            message.SentAt,
            message.LastStatusAt,
            message.RequestedBy);
    }
}
