using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Sugarista.Application.Sms;
using Sugarista.Infrastructure.Sms;

namespace Sugarista.Infrastructure.UnitTests.Sms;

public class SmsIrGatewayStatusTests
{
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }

    [Theory]
    [InlineData(1, SmsOutcomeStatus.Delivered)]
    [InlineData(2, SmsOutcomeStatus.Failed)]
    [InlineData(3, SmsOutcomeStatus.Accepted)]
    [InlineData(4, SmsOutcomeStatus.Accepted)]
    [InlineData(5, SmsOutcomeStatus.Sent)]
    [InlineData(6, SmsOutcomeStatus.Failed)]
    [InlineData(7, SmsOutcomeStatus.Rejected)]
    [InlineData(null, SmsOutcomeStatus.Unknown)]
    public void MapSmsIrDeliveryState_MapsAllCodesAccurately(int? code, SmsOutcomeStatus expected)
    {
        Assert.Equal(expected, SmsIrGateway.MapSmsIrDeliveryState(code));
    }

    [Fact]
    public async Task GetMessageStatusAsync_QueriesSmsIrReportEndpointAndMapsDelivered()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"status":1,"message":"موفق","data":{"messageId":987654,"deliveryState":1,"cost":1200}}""",
                Encoding.UTF8,
                "application/json")
        });

        using var client = new HttpClient(handler);
        var gateway = new SmsIrGateway(client, NullLogger<SmsIrGateway>.Instance, "test-api-key", "https://api.sms.ir/v1");

        Assert.True(gateway.SupportsMessageStatus);
        var result = await gateway.GetMessageStatusAsync("987654");

        Assert.True(result.Success);
        Assert.Equal(SmsOutcomeStatus.Delivered, result.Status);
        Assert.Equal(120m, result.Cost);
        Assert.Equal("https://api.sms.ir/v1/send/987654", handler.LastRequest?.RequestUri?.ToString());
        Assert.Equal("test-api-key", handler.LastRequest?.Headers.GetValues("x-api-key").Single());
    }
}
