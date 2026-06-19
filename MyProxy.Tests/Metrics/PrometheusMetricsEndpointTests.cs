extern alias GatewayHost;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyProxy.Infrastructure.Metrics;
using GatewayProgram = GatewayHost::Program;

namespace MyProxy.Tests.Metrics;

public class PrometheusMetricsEndpointTests
{
    [Fact]
    public async Task Metrics_endpoint_exposes_gateway_metrics()
    {
        await using var factory = new WebApplicationFactory<GatewayProgram>();
        var metrics = factory.Services.GetRequiredService<IGatewayMetrics>();
        metrics.RecordRequest(
            clientName: "Flight Ops",
            method: "GET",
            path: "/api/flights",
            statusCode: 200,
            latency: TimeSpan.FromMilliseconds(42));

        var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("gateway_requests", body, StringComparison.Ordinal);
        Assert.Contains("gateway_request_duration", body, StringComparison.Ordinal);
    }
}
