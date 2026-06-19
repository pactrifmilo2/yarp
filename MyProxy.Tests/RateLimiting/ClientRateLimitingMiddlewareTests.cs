using Microsoft.AspNetCore.Http;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Metrics;
using MyProxy.Infrastructure.RateLimiting;

namespace MyProxy.Tests.RateLimiting;

public class ClientRateLimitingMiddlewareTests
{
    [Fact]
    public async Task Invoke_returns_429_when_client_exceeds_rate_limit()
    {
        var client = Client.Create("Flight Ops", "api-key-hash", new[] { "read:flights" });
        client.AssignRateLimit(RateLimit.Create(requestLimit: 2, window: TimeSpan.FromMinutes(1)));
        var nextCalls = 0;
        var metrics = new TestGatewayMetrics();
        var middleware = new ClientRateLimitingMiddleware(context =>
        {
            nextCalls++;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, metrics);

        var firstResponse = await InvokeAsync(middleware, client);
        var secondResponse = await InvokeAsync(middleware, client);
        var thirdResponse = await InvokeAsync(middleware, client);

        Assert.Equal(StatusCodes.Status200OK, firstResponse.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, secondResponse.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, thirdResponse.StatusCode);
        Assert.Equal(2, nextCalls);
        Assert.Equal(new[] { "Flight Ops" }, metrics.RateLimitedClients);
    }

    [Fact]
    public async Task Invoke_partitions_limits_by_client()
    {
        var firstClient = Client.Create("Flight Ops", "flight-key-hash", new[] { "read:flights" });
        firstClient.AssignRateLimit(RateLimit.Create(requestLimit: 1, window: TimeSpan.FromMinutes(1)));
        var secondClient = Client.Create("Weather Ops", "weather-key-hash", new[] { "read:weather" });
        secondClient.AssignRateLimit(RateLimit.Create(requestLimit: 1, window: TimeSpan.FromMinutes(1)));
        var nextCalls = 0;
        var metrics = new TestGatewayMetrics();
        var middleware = new ClientRateLimitingMiddleware(context =>
        {
            nextCalls++;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, metrics);

        var firstClientResponse = await InvokeAsync(middleware, firstClient);
        var secondClientResponse = await InvokeAsync(middleware, secondClient);
        var firstClientExceededResponse = await InvokeAsync(middleware, firstClient);

        Assert.Equal(StatusCodes.Status200OK, firstClientResponse.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, secondClientResponse.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, firstClientExceededResponse.StatusCode);
        Assert.Equal(2, nextCalls);
        Assert.Equal(new[] { "Flight Ops" }, metrics.RateLimitedClients);
    }

    private static async Task<HttpResponse> InvokeAsync(
        ClientRateLimitingMiddleware middleware,
        Client client)
    {
        var httpContext = new DefaultHttpContext();
        var clientContext = new GatewayClientContext
        {
            Client = client,
        };

        await middleware.InvokeAsync(httpContext, clientContext);

        return httpContext.Response;
    }

    private sealed class TestGatewayMetrics : IGatewayMetrics
    {
        public List<string> RateLimitedClients { get; } = [];

        public void RecordRequest(
            string clientName,
            string method,
            string path,
            int statusCode,
            TimeSpan latency)
        {
        }

        public void RecordRateLimited(string clientName)
        {
            RateLimitedClients.Add(clientName);
        }
    }
}
