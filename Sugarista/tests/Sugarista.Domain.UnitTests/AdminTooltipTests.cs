using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests.AdminTooltipTests;

public class AdminTooltipTests
{
    [Fact]
    public void Constructor_WithValidData_SetsProperties()
    {
        var tooltip = new AdminTooltip("pos.pay_cash", "پرداخت نقدی سفارش", isActive: false);

        Assert.Equal("pos.pay_cash", tooltip.Key);
        Assert.Equal("پرداخت نقدی سفارش", tooltip.Text);
        Assert.False(tooltip.IsActive);
    }

    [Fact]
    public void Update_TrimsWhitespace()
    {
        var tooltip = new AdminTooltip("pos.pay_cash", "متن");

        tooltip.Update("pos.pay_cash", "  متن تازه  ", true);

        Assert.Equal("متن تازه", tooltip.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyKey_Throws(string? key)
    {
        Assert.Throws<DomainException>(() => new AdminTooltip(key!, "متن"));
    }

    [Fact]
    public void Constructor_WithEmptyText_Throws()
    {
        Assert.Throws<DomainException>(() => new AdminTooltip("pos.pay_cash", " "));
    }
}
