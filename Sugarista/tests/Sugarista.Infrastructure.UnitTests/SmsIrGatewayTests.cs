using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Sugarista.Infrastructure.Sms;
using Xunit;

namespace Sugarista.Infrastructure.UnitTests;

public class SmsIrGatewayTests
{
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    [Fact]
    public async Task GetCreditAsync_WhenEmptyUnauthorizedResponse_ReturnsFriendlyErrorInsteadOfJsonException()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            }));
        var gateway = new SmsIrGateway(client, NullLogger<SmsIrGateway>.Instance, "bad-key", null);

        var result = await gateway.GetCreditAsync();

        Assert.False(result.Success);
        Assert.Null(result.Credit);
        Assert.Contains("401", result.Error);
    }

    [Fact]
    public async Task GetCreditAsync_WhenNonJsonGatewayError_ReturnsFriendlyErrorInsteadOfJsonException()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("<html><body>502 Bad Gateway</body></html>", Encoding.UTF8, "text/html")
            }));
        var gateway = new SmsIrGateway(client, NullLogger<SmsIrGateway>.Instance, "valid-key", null);

        var result = await gateway.GetCreditAsync();

        Assert.False(result.Success);
        Assert.Contains("502", result.Error);
    }

    [Fact]
    public async Task GetCreditAsync_WhenSmsIrV1ScalarData_ParsesCreditCorrectly()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":1,"message":"موفق","data":4520.5}""", Encoding.UTF8, "application/json")
            }));
        var gateway = new SmsIrGateway(client, NullLogger<SmsIrGateway>.Instance, "valid-key", null);

        var result = await gateway.GetCreditAsync();

        Assert.True(result.Success);
        Assert.Equal(4520.5m, result.Credit);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task GetCreditAsync_WhenObjectDataCredit_ParsesCreditCorrectly()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":1,"message":"موفق","data":{"credit":9800}}""", Encoding.UTF8, "application/json")
            }));
        var gateway = new SmsIrGateway(client, NullLogger<SmsIrGateway>.Instance, "valid-key", null);

        var result = await gateway.GetCreditAsync();

        Assert.True(result.Success);
        Assert.Equal(9800m, result.Credit);
    }
}
