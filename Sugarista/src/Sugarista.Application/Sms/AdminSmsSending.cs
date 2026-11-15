using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

public sealed record SmsSendOutcomeDto(
    bool Success,
    Guid? MessageId,
    string Status,
    string? ProviderMessageId,
    string? Error,
    string Mobile);

public record SendSmsCommand(
    string? Mobile,
    Guid? ProviderId,
    Guid? TemplateId,
    string? Body,
    string Kind,
    string? SenderLine,
    bool TestMode = false) : IRequest<SmsSendOutcomeDto>;

public class SendSmsCommandValidator : AbstractValidator<SendSmsCommand>
{
    public SendSmsCommandValidator()
    {
        RuleFor(command => command.Mobile).NotEmpty().MaximumLength(20)
            .Must(mobile => SmsTextUtilities.NormalizeMobile(mobile) is not null)
            .WithMessage("شماره موبایل نامعتبر است. فرمت‌های مجاز: 0912…، 98912…، +98912…");
        RuleFor(command => command.Kind).Must(kind =>
                Enum.TryParse<SmsMessageKind>(kind, ignoreCase: true, out _))
            .WithMessage("نوع پیامک نامعتبر است.");
        RuleFor(command => command.Body).MaximumLength(1400)
            .Must((command, body) => !string.IsNullOrWhiteSpace(body) || command.TemplateId is not null)
            .WithMessage("متن پیامک را وارد کنید یا یک قالب انتخاب کنید.");
    }
}

public class SendSmsCommandHandler(ISmsGateway gateway, ICurrentUserService currentUser)
    : IRequestHandler<SendSmsCommand, SmsSendOutcomeDto>
{
    public async Task<SmsSendOutcomeDto> Handle(SendSmsCommand request, CancellationToken cancellationToken)
    {
        var kind = Enum.Parse<SmsMessageKind>(request.Kind, ignoreCase: true);
        var outcome = await gateway.SendSingleAsync(
            new SmsSendRequest(
                request.Mobile,
                request.ProviderId,
                request.TemplateId,
                request.Body,
                kind,
                request.SenderLine,
                currentUser.UserId ?? "admin"),
            cancellationToken);

        return new SmsSendOutcomeDto(
            outcome.Success,
            outcome.MessageId,
            outcome.Status.ToString(),
            outcome.ProviderMessageId,
            outcome.Error,
            outcome.NormalizedMobile);
    }
}

public sealed record SmsBulkRecipientPick(
    IReadOnlyList<string> Recipients,
    IReadOnlyList<Guid> GroupIds);

public record PreviewSmsBulkCommand(
    IReadOnlyList<string> Recipients,
    IReadOnlyList<Guid> GroupIds,
    Guid? ProviderId,
    Guid? TemplateId,
    string? Body,
    string Kind) : IRequest<SmsBulkPreview>;

public class PreviewSmsBulkCommandValidator : AbstractValidator<PreviewSmsBulkCommand>
{
    public PreviewSmsBulkCommandValidator()
    {
        RuleFor(command => command.Kind).Must(kind =>
                Enum.TryParse<SmsMessageKind>(kind, ignoreCase: true, out _))
            .WithMessage("نوع پیامک نامعتبر است.");
        RuleFor(command => command)
            .Must(command => command.Recipients.Count > 0 || command.GroupIds.Count > 0)
            .WithMessage("حداقل یک شماره یا یک گروه گیرنده انتخاب کنید.");
    }
}

public class PreviewSmsBulkCommandHandler(ISmsGateway gateway) : IRequestHandler<PreviewSmsBulkCommand, SmsBulkPreview>
{
    public async Task<SmsBulkPreview> Handle(PreviewSmsBulkCommand request, CancellationToken cancellationToken)
    {
        var kind = Enum.Parse<SmsMessageKind>(request.Kind, ignoreCase: true);
        return await gateway.PreviewBulkAsync(
            new SmsBulkPreviewRequest(
                request.Recipients,
                request.GroupIds,
                request.ProviderId,
                request.TemplateId,
                request.Body,
                kind),
            cancellationToken);
    }
}

public record CreateSmsCampaignCommand(
    string Title,
    IReadOnlyList<string> Recipients,
    IReadOnlyList<Guid> GroupIds,
    Guid? ProviderId,
    Guid? TemplateId,
    string? Body,
    string Kind) : IRequest<SmsCampaignDto>;

public class CreateSmsCampaignCommandValidator : AbstractValidator<CreateSmsCampaignCommand>
{
    public CreateSmsCampaignCommandValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(160);
        RuleFor(command => command.Kind).Must(kind =>
                Enum.TryParse<SmsMessageKind>(kind, ignoreCase: true, out _))
            .WithMessage("نوع پیامک نامعتبر است.");
        RuleFor(command => command)
            .Must(command => command.Recipients.Count > 0 || command.GroupIds.Count > 0)
            .WithMessage("حداقل یک شماره یا یک گروه گیرنده انتخاب کنید.");
    }
}

public class CreateSmsCampaignCommandHandler(
    ISmsGateway gateway,
    ICurrentUserService currentUser) : IRequestHandler<CreateSmsCampaignCommand, SmsCampaignDto>
{
    public async Task<SmsCampaignDto> Handle(CreateSmsCampaignCommand request, CancellationToken cancellationToken)
    {
        var kind = Enum.Parse<SmsMessageKind>(request.Kind, ignoreCase: true);
        var campaign = await gateway.CreateCampaignAsync(
            new SmsBulkPreviewRequest(
                request.Recipients,
                request.GroupIds,
                request.ProviderId,
                request.TemplateId,
                request.Body,
                kind),
            request.Title,
            currentUser.UserId ?? "admin",
            cancellationToken);

        return new SmsCampaignDto(
            campaign.Id,
            campaign.Title,
            campaign.Status.ToString(),
            campaign.TotalRecipients,
            campaign.SentCount,
            campaign.FailedCount,
            campaign.Kind.ToString(),
            campaign.ProviderId,
            campaign.CreatedAt,
            campaign.StartedAt,
            campaign.CompletedAt,
            campaign.CancelRequested);
    }
}

public sealed record SmsCampaignDto(
    Guid Id,
    string Title,
    string Status,
    int TotalRecipients,
    int SentCount,
    int FailedCount,
    string Kind,
    Guid? ProviderId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    bool CancelRequested);

public record GetSmsCampaignsQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<SmsPagedResult<SmsCampaignDto>>;

public class GetSmsCampaignsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsCampaignsQuery, SmsPagedResult<SmsCampaignDto>>
{
    public async Task<SmsPagedResult<SmsCampaignDto>> Handle(
        GetSmsCampaignsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = SmsPagination.Normalize(request.Page, request.PageSize);
        var query = dbContext.SmsCampaigns.AsNoTracking();

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(campaign => campaign.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(campaign => new SmsCampaignDto(
                campaign.Id,
                campaign.Title,
                campaign.Status.ToString(),
                campaign.TotalRecipients,
                campaign.SentCount,
                campaign.FailedCount,
                campaign.Kind.ToString(),
                campaign.ProviderId,
                campaign.CreatedAt,
                campaign.StartedAt,
                campaign.CompletedAt,
                campaign.CancelRequested))
            .ToListAsync(cancellationToken);

        return new SmsPagedResult<SmsCampaignDto>(items, total, page, pageSize);
    }
}

public sealed record SmsCampaignDetailDto(
    Guid Id,
    string Title,
    string Status,
    string Body,
    int TotalRecipients,
    int SentCount,
    int FailedCount,
    int PendingCount,
    string Kind,
    Guid? ProviderId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    bool CancelRequested,
    IReadOnlyList<SmsCampaignBatchDto> Batches);

public sealed record SmsCampaignBatchDto(int BatchNumber, int Total, int Sent, int Failed, int Pending);

public record GetSmsCampaignQuery(Guid Id) : IRequest<SmsCampaignDetailDto>;

public class GetSmsCampaignQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsCampaignQuery, SmsCampaignDetailDto>
{
    public async Task<SmsCampaignDetailDto> Handle(GetSmsCampaignQuery request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SmsCampaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("ارسال گروهی پیدا نشد.");

        var batchStats = await dbContext.SmsMessages
            .AsNoTracking()
            .Where(message => message.CampaignId == campaign.Id)
            .GroupBy(message => message.BatchNumber)
            .Select(group => new
            {
                BatchNumber = group.Key,
                Total = group.Count(),
                Sent = group.Count(message => message.Status == SmsSendStatus.Accepted
                                              || message.Status == SmsSendStatus.Sent),
                Failed = group.Count(message => message.Status == SmsSendStatus.Failed
                                                || message.Status == SmsSendStatus.Rejected)
            })
            .ToListAsync(cancellationToken);

        var pending = await dbContext.SmsMessages
            .AsNoTracking()
            .CountAsync(message => message.CampaignId == campaign.Id
                                   && (message.Status == SmsSendStatus.Queued
                                       || message.Status == SmsSendStatus.Pending
                                       || message.Status == SmsSendStatus.Processing), cancellationToken);

        return new SmsCampaignDetailDto(
            campaign.Id,
            campaign.Title,
            campaign.Status.ToString(),
            campaign.Body,
            campaign.TotalRecipients,
            campaign.SentCount,
            campaign.FailedCount,
            pending,
            campaign.Kind.ToString(),
            campaign.ProviderId,
            campaign.CreatedAt,
            campaign.StartedAt,
            campaign.CompletedAt,
            campaign.CancelRequested,
            batchStats
                .OrderBy(entry => entry.BatchNumber)
                .Select(entry => new SmsCampaignBatchDto(
                    entry.BatchNumber,
                    entry.Total,
                    entry.Sent,
                    entry.Failed,
                    entry.Total - entry.Sent - entry.Failed))
                .ToList());
    }
}

public record CancelSmsCampaignCommand(Guid Id) : IRequest;

public class CancelSmsCampaignCommandHandler(
    IApplicationDbContext dbContext,
    IAuditLog auditLog) : IRequestHandler<CancelSmsCampaignCommand>
{
    public async Task Handle(CancelSmsCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SmsCampaigns
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("ارسال گروهی پیدا نشد.");

        if (campaign.Status is not (SmsCampaignStatus.Pending or SmsCampaignStatus.Processing))
        {
            throw new InvalidOperationException("فقط ارسال‌های در صف یا در حال پردازش قابل لغو هستند.");
        }

        campaign.RequestCancel();
        await dbContext.SaveChangesAsync(cancellationToken);
        auditLog.Write("sms.campaign.cancel-requested", $"Campaign '{campaign.Title}' ({campaign.Id}).");
    }
}
