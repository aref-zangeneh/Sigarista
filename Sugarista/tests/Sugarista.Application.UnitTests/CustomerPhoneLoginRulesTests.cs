using System.Text.RegularExpressions;
using Sugarista.Application.Customers.Auth;
using Sugarista.Application.Customers.Auth.Commands.LoginCustomer;
using Xunit;
using Xunit.Sdk;

namespace Sugarista.Application.UnitTests;

public class CustomerPhoneLoginRulesTests
{
    // ── normalization: one phone must always map to one record ──────────────

    [Theory]
    [InlineData("09121234567", "09121234567")]            // typed by the customer
    [InlineData(" 0912 345 6789 ", "09123456789")]        // spaces in between
    [InlineData("+989121234567", "09121234567")]          // with international prefix
    [InlineData("989121234567", "09121234567")]           // bare country code (12 digits)
    [InlineData("00989121234567", "09121234567")]         // double zero prefix
    [InlineData("9121234567", "09121234567")]             // dropped leading zero
    [InlineData("\t09121234567\n", "09121234567")]        // surrounding whitespace
    public void NormalizePhone_FoldsEveryPhoneStyleToOneCanonicalRecord(string raw, string expected)
    {
        Assert.Equal(expected, CustomerLoginRules.NormalizePhone(raw));
    }

    [Fact]
    public void NormalizePhone_KeepsLocalLandlineUntouched()
    {
        // 10-digit starting with anything other than 9 (e.g. 021 dialects typed
        // without the zero) must NOT be folded into an 09-prefixed mobile.
        Assert.Equal("5101234567", CustomerLoginRules.NormalizePhone("5101234567"));
    }

    [Theory]
    [InlineData("09121234567", true)]
    [InlineData("+989121234567", true)]
    [InlineData("0912123456", true)]   // 10 digits without zero.
    [InlineData("abc", false)]         // not digits
    [InlineData("09121", false)]       // too short
    [InlineData("١٢٣٤٥", false)]       // looks numeric, isn't ASCII digits
    public void IsValidPhone_AcceptsOnlySaneDigitPhones(string raw, bool expected)
    {
        var phone = CustomerLoginRules.NormalizePhone(raw);
        Assert.Equal(expected, CustomerLoginRules.IsValidPhone(phone));
    }

    [Fact]
    public void ClampDurationDays_KeepsSettingsInsideSafeBounds()
    {
        Assert.Equal(60, CustomerLoginRules.ClampDurationDays(0));      // unset → two months
        Assert.Equal(60, CustomerLoginRules.ClampDurationDays(-5));
        Assert.Equal(30, CustomerLoginRules.ClampDurationDays(30));
        Assert.Equal(60, CustomerLoginRules.ClampDurationDays(60));
        Assert.Equal(730, CustomerLoginRules.ClampDurationDays(1095));  // > 2 years cap
    }

    [Theory]
    [InlineData("09121234567", true)]
    [InlineData("0912 3456789", true)]
    [InlineData("not-a-phone", false)]
    [InlineData("0912123", false)]
    public void LoginCustomerCommandValidator_GuardsThePhoneNumber(string raw, bool expected)
    {
        var validator = new LoginCustomerCommandValidator();
        var result = validator.Validate(new LoginCustomerCommand(raw));
        Assert.Equal(expected, result.IsValid);
    }
}
