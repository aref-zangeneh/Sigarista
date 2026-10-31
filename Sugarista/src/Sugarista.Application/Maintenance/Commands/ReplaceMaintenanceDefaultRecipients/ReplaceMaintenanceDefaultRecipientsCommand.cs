using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Maintenance.Commands.UpsertMaintenanceSchedule;
using Sugarista.Application.Maintenance.Queries.GetMaintenanceEquipments;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Maintenance.Commands.ReplaceMaintenanceDefaultRecipients;

/// <summary>
/// Replaces the global fallback reminder list (site admin's mobiles/emails).
/// These targets receive reminders for every schedule that has no recipients
/// of its own, so the «مدیر سایت» always hears about due services.
/// </summary>
public record ReplaceMaintenanceDefaultRecipientsCommand(
    IReadOnlyList<MaintenanceRecipientInput> Recipients) : IRequest<IReadOnlyList<MaintenanceRecipientDto>>;

public class ReplaceMaintenanceDefaultRecipientsCommandValidator
    : AbstractValidator<ReplaceMaintenanceDefaultRecipientsCommand>
{
    public ReplaceMaintenanceDefaultRecipientsCommandValidator()
    {
        RuleForEach(command => command.Recipients).ChildRules(recipient =>
        {
            recipient.RuleFor(item => item.Target)
                .NotEmpty().WithMessage("مقدار ایمیل/شماره موبایل الزامی است.")
                .MaximumLength(200);
            recipient.RuleFor(item => item.Channel)
                .NotEmpty().WithMessage("کانال یادآوری الزامی است. (Sms یا Email)");
            recipient.RuleFor(item => item.Label).MaximumLength(100);
        });
    }
}

public class ReplaceMaintenanceDefaultRecipientsCommandHandler
    : IRequestHandler<ReplaceMaintenanceDefaultRecipientsCommand, IReadOnlyList<MaintenanceRecipientDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public ReplaceMaintenanceDefaultRecipientsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MaintenanceRecipientDto>> Handle(
        ReplaceMaintenanceDefaultRecipientsCommand request,
        CancellationToken cancellationToken)
    {
        // Build and validate the new list before touching the old one, so a bad
        // row cannot wipe the currently saved fallback recipients.
        var recipients = new List<MaintenanceDefaultRecipient>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in request.Recipients)
        {
            if (!Enum.TryParse<MaintenanceChannel>(input.Channel, ignoreCase: true, out var channel)
                || channel is not (MaintenanceChannel.Sms or MaintenanceChannel.Email))
            {
                throw new InvalidOperationException(
                    $"کانال یادآوری «{input.Channel}» معتبر نیست. از Sms یا Email استفاده کنید.");
            }

            var recipient = new MaintenanceDefaultRecipient(channel, input.Target, input.Label);
            if (!seen.Add($"{recipient.Channel}:{recipient.Target}"))
            {
                continue;
            }

            recipients.Add(recipient);
        }

        // Delete first so re-saving an unchanged target cannot trip the unique
        // (Channel, Target) index while EF is still batching inserts/deletes.
        await _dbContext.MaintenanceDefaultRecipients
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.MaintenanceDefaultRecipients.AddRange(recipients);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return recipients
            .OrderBy(recipient => recipient.Channel)
            .ThenBy(recipient => recipient.Target, StringComparer.Ordinal)
            .Select(recipient => new MaintenanceRecipientDto(
                recipient.Id,
                recipient.Channel.ToString(),
                recipient.Target,
                recipient.Label))
            .ToList();
    }
}
