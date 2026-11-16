using FluentValidation;
using MediatR;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Tooltips.Dto;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Tooltips.Commands.UpsertTooltipOverride;

public record UpsertTooltipOverrideCommand(
    string Key,
    string Text,
    bool IsActive = true) : IRequest<AdminTooltipOverrideDto>;

public class UpsertTooltipOverrideCommandValidator : AbstractValidator<UpsertTooltipOverrideCommand>
{
    public UpsertTooltipOverrideCommandValidator()
    {
        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(150)
            .Matches("^[a-z0-9._-]+$")
            .WithMessage("کلید راهنما فقط می‌تواند شامل حروف لاتین کوچک، عدد، نقطه، خط تیره و آندرلاین باشد.");

        RuleFor(command => command.Text)
            .NotEmpty()
            .MaximumLength(1000);
    }
}

public class UpsertTooltipOverrideCommandHandler
    : IRequestHandler<UpsertTooltipOverrideCommand, AdminTooltipOverrideDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertTooltipOverrideCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminTooltipOverrideDto> Handle(
        UpsertTooltipOverrideCommand request,
        CancellationToken cancellationToken)
    {
        var key = request.Key.Trim();

        var tooltip = await _dbContext.AdminTooltips
            .FirstOrDefaultAsync(entry => entry.Key == key, cancellationToken);

        if (tooltip is null)
        {
            tooltip = new AdminTooltip(key, request.Text.Trim(), request.IsActive);
            _dbContext.AdminTooltips.Add(tooltip);
        }
        else
        {
            tooltip.Update(tooltip.Key, request.Text.Trim(), request.IsActive);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminTooltipOverrideDto(tooltip.Key, tooltip.Text, tooltip.IsActive);
    }
}
