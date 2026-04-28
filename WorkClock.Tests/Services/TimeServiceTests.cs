using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using WorkClock.Api.Exceptions;
using WorkClock.Api.Services;
using Xunit;

namespace WorkClock.Tests.Services;

public class TimeServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static IHttpClientFactory MakeFactory(HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(response);

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://timeapi.io/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("TimeApi")).Returns(client);
        return factory.Object;
    }

    private static IHttpClientFactory MakeThrowingFactory(Exception ex)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ThrowsAsync(ex);

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://timeapi.io/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("TimeApi")).Returns(client);
        return factory.Object;
    }

    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

    // ── Happy path ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNowAsync_ValidResponse_ReturnsZurichLocalTime()
    {
        // +02:00 offset → stores 21:00 as Zurich local time
        var factory = MakeFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonBody(new { date_time = "2026-04-26T21:00:00.000000+02:00" })
        });
        var sut = new TimeService(factory, NullLogger<TimeService>.Instance);

        var result = await sut.GetNowAsync();

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(new DateTime(2026, 4, 26, 21, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public async Task GetNowAsync_ZeroOffset_ReturnsLocalTimeDirectly()
    {
        var factory = MakeFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonBody(new { date_time = "2026-04-26T15:30:00.000000+00:00" })
        });
        var sut = new TimeService(factory, NullLogger<TimeService>.Instance);

        var result = await sut.GetNowAsync();

        Assert.Equal(new DateTime(2026, 4, 26, 15, 30, 0, DateTimeKind.Utc), result);
    }

    // ── Network / HTTP errors ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetNowAsync_NetworkFailure_ThrowsTimeServiceException()
    {
        var factory = MakeThrowingFactory(new HttpRequestException("Network unreachable"));
        var sut = new TimeService(factory, NullLogger<TimeService>.Instance);

        await Assert.ThrowsAsync<TimeServiceException>(() => sut.GetNowAsync());
    }

    [Fact]
    public async Task GetNowAsync_Timeout_ThrowsTimeServiceException()
    {
        var factory = MakeThrowingFactory(new TaskCanceledException("Request timed out"));
        var sut = new TimeService(factory, NullLogger<TimeService>.Instance);

        await Assert.ThrowsAsync<TimeServiceException>(() => sut.GetNowAsync());
    }

    [Fact]
    public async Task GetNowAsync_Non200StatusCode_ThrowsTimeServiceException()
    {
        var factory = MakeFactory(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = new TimeService(factory, NullLogger<TimeService>.Instance);

        await Assert.ThrowsAsync<TimeServiceException>(() => sut.GetNowAsync());
    }

    // ── Malformed responses ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetNowAsync_InvalidJson_ThrowsTimeServiceException()
    {
        var factory = MakeFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("this is not json")
        });
        var sut = new TimeService(factory, NullLogger<TimeService>.Instance);

        await Assert.ThrowsAsync<TimeServiceException>(() => sut.GetNowAsync());
    }

    [Fact]
    public async Task GetNowAsync_MissingDateTimeField_ThrowsTimeServiceException()
    {
        var factory = MakeFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonBody(new { timezone = "Europe/Zurich" }) // no date_time field
        });
        var sut = new TimeService(factory, NullLogger<TimeService>.Instance);

        await Assert.ThrowsAsync<TimeServiceException>(() => sut.GetNowAsync());
    }

    [Fact]
    public async Task GetNowAsync_EmptyDateTimeField_ThrowsTimeServiceException()
    {
        var factory = MakeFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonBody(new { date_time = "" })
        });
        var sut = new TimeService(factory, NullLogger<TimeService>.Instance);

        await Assert.ThrowsAsync<TimeServiceException>(() => sut.GetNowAsync());
    }

    [Fact]
    public async Task GetNowAsync_UnparseableDateTimeValue_ThrowsTimeServiceException()
    {
        var factory = MakeFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonBody(new { date_time = "not-a-date" })
        });
        var sut = new TimeService(factory, NullLogger<TimeService>.Instance);

        await Assert.ThrowsAsync<TimeServiceException>(() => sut.GetNowAsync());
    }
}
