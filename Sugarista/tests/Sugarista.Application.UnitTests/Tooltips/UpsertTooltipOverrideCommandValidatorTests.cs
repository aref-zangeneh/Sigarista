using Sugarista.Application.Tooltips.Commands.UpsertTooltipOverride;

namespace Sugarista.Application.UnitTests.Tooltips;

public class UpsertTooltipOverrideCommandValidatorTests
{
    private readonly UpsertTooltipOverrideCommandValidator _validator = new();

    [Theory]
    [InlineData("pos.pay_cash", true)]
    [InlineData("pos.service_toggle", true)]
    [InlineData("tooltips-page.search", true)]
    [InlineData("", false)]
    [InlineData("Has Space", false)]
    [InlineData("has!bang", false)]
    [InlineData("فارسی", false)]
    public void Key_ShouldOnlyAllowLowercaseLatinDotDash(string key, bool shouldBeValid)
    {
        var result = _validator.Validate(new UpsertTooltipOverrideCommand(key, "متن راهنما"));

        Assert.Equal(shouldBeValid, result.IsValid);
    }

    [Fact]
    public void Text_ShouldBeRequired()
    {
        var result = _validator.Validate(new UpsertTooltipOverrideCommand("pos.pay_cash", "  "));

        Assert.False(result.IsValid);
    }
}
