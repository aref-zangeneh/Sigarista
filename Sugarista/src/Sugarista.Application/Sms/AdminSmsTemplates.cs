using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

public sealed record SmsTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    string Body,
    string Kind,
    bool IsActive,
    bool IsDefault,
    IReadOnlyList<SmsTemplateMappingDto> ProviderMappings,
    IReadOnlyList<string> Placeholders,
    int Segments,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastModifiedAt);

public sealed record SmsTemplateMappingDto(string ProviderType, string ProviderTemplateId);

public record GetSmsTemplatesQuery : IRequest<IReadOnlyList<SmsTemplateDto>>;

public class GetSmsTemplatesQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsTemplatesQuery, IReadOnlyList<SmsTemplateDto>>
{
    public async Task<IReadOnlyList<SmsTemplateDto>> Handle(
        GetSmsTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var templates = await dbContext.SmsTemplates
            .AsNoTracking()
            .Include(template => template.ProviderMappings)
            .OrderByDescending(template => template.IsDefault)
            .ThenBy(template => template.Name)
            .ToListAsync(cancellationToken);

        return templates
            .Select(ToDto)
            .ToList();
    }

    internal static SmsTemplateDto ToDto(SmsTemplate template)
    {
        var (segments, _, _) = SmsTextUtilities.ComputeSegments(
            SmsTextUtilities.RenderTemplate(template.Body, SmsTextUtilities.SampleValues));
        return new SmsTemplateDto(
            template.Id,
            template.Name,
            template.Description,
            template.Body,
            template.Kind.ToString(),
            template.IsActive,
            template.IsDefault,
            template.ProviderMappings
                .Select(mapping => new SmsTemplateMappingDto(mapping.ProviderType.ToString(), mapping.ProviderTemplateId))
                .ToList(),
            SmsTextUtilities.ExtractPlaceholders(template.Body),
            segments,
            template.CreatedAt,
            template.LastModifiedAt);
    }
}

public sealed record SmsTemplateMappingInput(string ProviderType, string ProviderTemplateId);

public record UpsertSmsTemplateCommand(
    Guid Id,
    string Name,
    string? Description,
    string Body,
    string Kind,
    bool IsActive,
    bool IsDefault,
    IReadOnlyList<SmsTemplateMappingInput> ProviderMappings) : IRequest<Guid>;

public class UpsertSmsTemplateCommandValidator : AbstractValidator<UpsertSmsTemplateCommand>
{
    public UpsertSmsTemplateCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Body).NotEmpty().MaximumLength(1400);
        RuleFor(command => command.Kind).Must(kind =>
                Enum.TryParse<SmsMessageKind>(kind, ignoreCase: true, out _))
            .WithMessage("نوع پیامک نامعتبر است.");
        RuleFor(command => command.Body)
            .Must(body => SmsTextUtilities.ExtractPlaceholders(body)
                .All(name => SmsTextUtilities.PlaceholderNames.Contains(name)))
            .WithMessage($"متغیرهای مجاز: {string.Join("، ", SmsTextUtilities.PlaceholderNames)}");
    }
}

public class UpsertSmsTemplateCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpsertSmsTemplateCommand, Guid>
{
    public async Task<Guid> Handle(UpsertSmsTemplateCommand request, CancellationToken cancellationToken)
    {
        var kind = Enum.Parse<SmsMessageKind>(request.Kind, ignoreCase: true);

        SmsTemplate template;
        if (request.Id == Guid.Empty)
        {
            template = new SmsTemplate(
                request.Name, request.Description, request.Body, kind, request.IsActive, request.IsDefault);
            dbContext.SmsTemplates.Add(template);
        }
        else
        {
            template = await dbContext.SmsTemplates
                .Include(item => item.ProviderMappings)
                .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
                ?? throw new KeyNotFoundException("قالب پیدا نشد.");

            template.Update(request.Name, request.Description, request.Body, kind, request.IsActive, request.IsDefault);
            dbContext.SmsTemplateProviderMappings.RemoveRange(template.ProviderMappings);
        }

        foreach (var mapping in request.ProviderMappings.Take(10))
        {
            if (Enum.TryParse<SmsProviderType>(mapping.ProviderType, ignoreCase: true, out var providerType)
                && !string.IsNullOrWhiteSpace(mapping.ProviderTemplateId))
            {
                template.ProviderMappings.Add(
                    new SmsTemplateProviderMapping(template.Id, providerType, mapping.ProviderTemplateId));
            }
        }

        // At most one default per kind.
        if (request.IsDefault)
        {
            var otherDefaults = await dbContext.SmsTemplates
                .Where(item => item.Kind == kind && item.IsDefault && item.Id != template.Id)
                .ToListAsync(cancellationToken);
            foreach (var other in otherDefaults)
            {
                other.Update(other.Name, other.Description, other.Body, other.Kind, other.IsActive, isDefault: false);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return template.Id;
    }
}

public record DeleteSmsTemplateCommand(Guid Id) : IRequest;

public class DeleteSmsTemplateCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<DeleteSmsTemplateCommand>
{
    public async Task Handle(DeleteSmsTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await dbContext.SmsTemplates
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("قالب پیدا نشد.");

        dbContext.SmsTemplates.Remove(template);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record DuplicateSmsTemplateCommand(Guid Id) : IRequest<Guid>;

public class DuplicateSmsTemplateCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<DuplicateSmsTemplateCommand, Guid>
{
    public async Task<Guid> Handle(DuplicateSmsTemplateCommand request, CancellationToken cancellationToken)
    {
        var source = await dbContext.SmsTemplates
            .Include(template => template.ProviderMappings)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("قالب پیدا نشد.");

        var copy = new SmsTemplate(
            $"{source.Name} (کپی)",
            source.Description,
            source.Body,
            source.Kind,
            source.IsActive,
            isDefault: false);
        foreach (var mapping in source.ProviderMappings)
        {
            copy.ProviderMappings.Add(
                new SmsTemplateProviderMapping(copy.Id, mapping.ProviderType, mapping.ProviderTemplateId));
        }

        dbContext.SmsTemplates.Add(copy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return copy.Id;
    }
}

/// <summary>Preview a template body (or raw text) with sample values + segment math.</summary>
public record PreviewSmsTemplateQuery(string Body) : IRequest<SmsTemplatePreviewDto>;

public sealed record SmsTemplatePreviewDto(
    string RenderedBody,
    IReadOnlyList<string> Placeholders,
    int Characters,
    int Segments,
    bool IsUnicode);

public class PreviewSmsTemplateQueryHandler : IRequestHandler<PreviewSmsTemplateQuery, SmsTemplatePreviewDto>
{
    public Task<SmsTemplatePreviewDto> Handle(PreviewSmsTemplateQuery request, CancellationToken cancellationToken)
    {
        var placeholders = SmsTextUtilities.ExtractPlaceholders(request.Body);
        var rendered = SmsTextUtilities.RenderTemplate(request.Body, SmsTextUtilities.SampleValues);
        var (segments, characters, isUnicode) = SmsTextUtilities.ComputeSegments(rendered);
        return Task.FromResult(new SmsTemplatePreviewDto(rendered, placeholders, characters, segments, isUnicode));
    }
}
