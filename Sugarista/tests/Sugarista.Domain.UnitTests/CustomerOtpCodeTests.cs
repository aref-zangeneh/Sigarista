using Sugarista.Domain.Entities;
using Xunit;

namespace Sugarista.Domain.UnitTests;

public class CustomerOtpCodeTests
{
    [Fact]
    public void VerifyAndConsume_WithMatchingCode_ConsumesOnceAndRejectsReplay()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new CustomerOtpCode("09121234567", "482916", now, expirySeconds: 120, maxAttempts: 5);

        Assert.NotEqual("482916", otp.CodeHash);
        Assert.True(otp.CanVerifyAt(now.AddSeconds(30)));

        Assert.True(otp.VerifyAndConsume("482916", now.AddSeconds(30)));
        Assert.True(otp.IsConsumed);
        Assert.False(otp.CanVerifyAt(now.AddSeconds(31)));
        Assert.False(otp.VerifyAndConsume("482916", now.AddSeconds(31)));
    }

    [Fact]
    public void VerifyAndConsume_WrongCodesIncrementAttemptsUntilExhausted()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new CustomerOtpCode("09121234567", "654321", now, expirySeconds: 120, maxAttempts: 3);

        Assert.False(otp.VerifyAndConsume("111111", now.AddSeconds(5)));
        Assert.Equal(1, otp.AttemptCount);
        Assert.False(otp.IsExhausted);

        Assert.False(otp.VerifyAndConsume("222222", now.AddSeconds(10)));
        Assert.False(otp.VerifyAndConsume("333333", now.AddSeconds(15)));
        Assert.True(otp.IsExhausted);
        Assert.False(otp.CanVerifyAt(now.AddSeconds(20)));
        Assert.False(otp.VerifyAndConsume("654321", now.AddSeconds(20)));
    }

    [Fact]
    public void VerifyAndConsume_AfterExpiry_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new CustomerOtpCode("09121234567", "123456", now, expirySeconds: 120);

        Assert.True(otp.IsExpiredAt(now.AddSeconds(121)));
        Assert.False(otp.VerifyAndConsume("123456", now.AddSeconds(121)));
    }

    [Fact]
    public void GenerateNumericCode_ReturnsExactDigitCount()
    {
        var code = CustomerOtpCode.GenerateNumericCode(6);
        Assert.Equal(6, code.Length);
        Assert.All(code, ch => Assert.True(char.IsAsciiDigit(ch)));
    }
}
