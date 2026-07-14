using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MyProxy.Admin.ControlPlane;

namespace MyProxy.Tests.Admin;

internal static class GatewayReloadClientTestFactory
{
    public static GatewayReloadClient Create()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler())
        {
            BaseAddress = new Uri("http://localhost:9999"),
        };
        return new GatewayReloadClient(httpClient, NullLogger<GatewayReloadClient>.Instance);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
