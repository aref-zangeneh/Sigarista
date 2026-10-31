namespace Sugarista.Application.Maintenance.Commands.TestMaintenanceReminder;

/// <summary>Send an immediate test reminder for one schedule (no cycle rolling).</summary>
public record TestMaintenanceReminderCommand(Guid ScheduleId) : IRequest<MaintenanceReminderTestResult>;

public class TestMaintenanceReminderCommandValidator : AbstractValidator<TestMaintenanceReminderCommand>
{
    public TestMaintenanceReminderCommandValidator()
    {
        RuleFor(command => command.ScheduleId).NotEmpty();
    }
}

public class TestMaintenanceReminderCommandHandler
    : IRequestHandler<TestMaintenanceReminderCommand, MaintenanceReminderTestResult>
{
    private readonly MaintenanceReminderDispatcher _dispatcher;

    public TestMaintenanceReminderCommandHandler(MaintenanceReminderDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task<MaintenanceReminderTestResult> Handle(
        TestMaintenanceReminderCommand request,
        CancellationToken cancellationToken)
    {
        return await _dispatcher.SendTestAsync(request.ScheduleId, cancellationToken);
    }
}
