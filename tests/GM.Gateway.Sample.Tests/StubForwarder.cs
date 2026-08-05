using System.Net;
using Yarp.ReverseProxy.Forwarder;

namespace GM.Gateway.Sample.Tests;

// Answers every forwarded request with 200 "backend-ok", so the tests exercise the gateway's routing
// and rate limiting without running the separate backend service.
internal sealed class StubForwarderHttpClientFactory : IForwarderHttpClientFactory
{
    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new(new StubBackendHandler());
}

internal sealed class StubBackendHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("backend-ok") });
}
