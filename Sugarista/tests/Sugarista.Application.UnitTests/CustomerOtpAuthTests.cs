using Sugarista.Application.Customers.Auth.Commands.RequestCustomerOtp;
using Sugarista.Application.Customers.Auth.Commands.VerifyCustomerOtp;
using Xunit;

namespace Sugarista.Application.UnitTests;

public class CustomerOtpAuthTests
{
    [Theory]
    [InlineData("09121234567", true)]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷", true)]
    [InlineData("+989121234567", true)]
    [InlineData("9121234567", true)]
    [InlineData("12345", false)]
    [InlineData("", false)]
    [InlineData("02122334455", false)]
    public void RequestOtpValidator_ValidatesIranianMobileFormats(string phone, bool expectedValid)
    {
        var validator = new RequestCustomerOtpCommandValidator();
        var result = validator.Validate(new RequestCustomerOtpCommand(phone));
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("09121234567", "482916", true)]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷", "۴۸۲۹۱۶", true)]
    [InlineData("09121234567", "123", false)]
    [InlineData("09121234567", "abcdef", false)]
    [InlineData("09121234567", "", false)]
    public void VerifyOtpValidator_ValidatesPhoneAndNumericCode(string phone, string code, bool expectedValid)
    {
        var validator = new VerifyCustomerOtpCommandValidator();
        var result = validator.Validate(new VerifyCustomerOtpCommand(phone, code));
        Assert.Equal(expectedValid, result.IsValid);
    }
}
