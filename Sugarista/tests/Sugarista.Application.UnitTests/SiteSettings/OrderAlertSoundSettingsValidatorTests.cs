using Sugarista.Application.SiteSettings.Commands.UpdateOrderAlertSoundSettings;
using Sugarista.Application.SiteSettings.Commands.UpdateSiteSettings;

namespace Sugarista.Application.UnitTests.SiteSettings;

public class OrderAlertSoundSettingsValidatorTests
{
    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(false, 0, true)]
    [InlineData(true, 3, true)]
    [InlineData(true, 10, true)]
    [InlineData(true, 30, true)]
    [InlineData(true, 300, true)]
    [InlineData(true, -1, false)]
    [InlineData(true, 1, false)]
    [InlineData(true, 2, false)]
    [InlineData(true, 301, false)]
    public void UpdateOrderAlertSoundSettingsValidator_ShouldEnforceValidIntervals(
        bool enabled,
        int intervalSeconds,
        bool expectedValid)
    {
        var validator = new UpdateOrderAlertSoundSettingsCommandValidator();
        var result = validator.Validate(new UpdateOrderAlertSoundSettingsCommand(enabled, intervalSeconds));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(300, true)]
    [InlineData(2, false)]
    [InlineData(500, false)]
    public void UpdateSiteSettingsValidator_ShouldValidateOrderAlertSoundInterval(
        int intervalSeconds,
        bool expectedValid)
    {
        var validator = new UpdateSiteSettingsCommandValidator();
        var command = new UpdateSiteSettingsCommand
        {
            CafeName = "Sugarista",
            BrowserTitle = "Sugarista",
            MetaDescription = "Cafe",
            TimeZoneId = "Asia/Tehran",
            ThemeMode = "auto",
            OrderAlertSoundEnabled = true,
            OrderAlertSoundIntervalSeconds = intervalSeconds,
        };

        var result = validator.Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
    }
}
