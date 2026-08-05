using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
using Xunit;

namespace GM.Gateway.Sample.Tests;

// Boots the actual sample gateway (its appsettings routes + policies) and forwards to a stub backend,
// so the sample's real configuration is exercised end to end without running a separate service.
public class GatewaySampleTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient CreateClient() =>
        factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton<IForwarderHttpClientFactory, StubForwarderHttpClientFactory>()))
        .CreateClient();

    [Fact]
    public async Task OrdersRoute_Allows5_Then429()
    {
        var client = CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
            statuses.Add((await client.GetAsync("/orders/123")).StatusCode);

        Assert.Equal(5, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task OpenRoute_IsProxied_AndNeverLimited()
    {
        var client = CreateClient();

        for (var i = 0; i < 8; i++)
        {
            var response = await client.GetAsync("/open/anything");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("backend-ok", await response.Content.ReadAsStringAsync());
        }
    }
}
