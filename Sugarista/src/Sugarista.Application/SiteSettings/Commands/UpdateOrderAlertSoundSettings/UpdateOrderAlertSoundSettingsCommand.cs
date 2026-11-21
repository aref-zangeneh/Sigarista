using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.SiteSettings.Commands.UpdateOrderAlertSoundSettings;

public record OrderAlertSoundSettingsDto(bool Enabled, int IntervalSeconds);

public record UpdateOrderAlertSoundSettingsCommand(
    bool Enabled,
    int IntervalSeconds) : IRequest<OrderAlertSoundSettingsDto>;

public class UpdateOrderAlertSoundSettingsCommandValidator
    : AbstractValidator<UpdateOrderAlertSoundSettingsCommand>
{
    public UpdateOrderAlertSoundSettingsCommandValidator()
    {
        RuleFor(command => command.IntervalSeconds)
            .Must(seconds => seconds == 0 || (seconds >= 3 && seconds <= 300))
            .WithMessage("فاصلهٔ تکرار صدای اعلان سفارش باید ۰ (بدون تکرار) یا بین ۳ تا ۳۰۰ ثانیه باشد.");
    }
}

public class UpdateOrderAlertSoundSettingsCommandHandler
    : IRequestHandler<UpdateOrderAlertSoundSettingsCommand, OrderAlertSoundSettingsDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateOrderAlertSoundSettingsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OrderAlertSoundSettingsDto> Handle(
        UpdateOrderAlertSoundSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Site settings were not found.");

        settings.OrderAlertSoundEnabled = request.Enabled;
        settings.OrderAlertSoundIntervalSeconds = request.IntervalSeconds;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OrderAlertSoundSettingsDto(
            settings.OrderAlertSoundEnabled,
            settings.OrderAlertSoundIntervalSeconds);
    }
}
