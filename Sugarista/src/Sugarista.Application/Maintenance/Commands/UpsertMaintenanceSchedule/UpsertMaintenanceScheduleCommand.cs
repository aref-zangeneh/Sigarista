using FluentValidation;

namespace Sugarista.Application.Maintenance.Commands.UpsertMaintenanceSchedule;

public record MaintenanceRecipientInput(string Channel, string Target, string? Label);

public record UpsertMaintenanceScheduleCommand(
    Guid EquipmentId,
    Guid? Id,
    string Title,
    int IntervalDays,
    DateOnly NextDueDate,
    bool IsActive,
    IReadOnlyList<MaintenanceRecipientInput> Recipients) : IRequest<Guid>;

public class UpsertMaintenanceScheduleCommandValidator : AbstractValidator<UpsertMaintenanceScheduleCommand>
{
    public UpsertMaintenanceScheduleCommandValidator()
    {
        RuleFor(command => command.EquipmentId).NotEmpty();
        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("عنوان زمان‌بندی الزامی است.")
            .MaximumLength(150);
        RuleFor(command => command.IntervalDays)
            .InclusiveBetween(1, 3650).WithMessage("فاصله سرویس باید بین ۱ تا ۳۶۵۰ روز باشد.");
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
