using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Auth;

namespace MyProxy.Infrastructure.RateLimiting;

public sealed class ClientRateLimitingMiddleware(RequestDelegate next) : IDisposable
{
    private static readonly object ClientItemKey = new();

    private readonly PartitionedRateLimiter<HttpContext> _limiter =
        PartitionedRateLimiter.Create<HttpContext, string>(CreatePartition);

    public async Task InvokeAsync(
        HttpContext httpContext,
        GatewayClientContext clientContext)
    {
        httpContext.Items[ClientItemKey] = clientContext.Client;

        using var lease = await _limiter.AcquireAsync(
            httpContext,
            permitCount: 1,
            httpContext.RequestAborted);

        if (!lease.IsAcquired)
        {
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        await next(httpContext);
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }

    private static RateLimitPartition<string> CreatePartition(HttpContext httpContext)
    {
        if (!httpContext.Items.TryGetValue(ClientItemKey, out var value) ||
            value is not Client client ||
            client.RateLimit is null)
        {
            return RateLimitPartition.GetNoLimiter("anonymous");
        }

        var rateLimit = client.RateLimit;
        var partitionKey = $"{client.Id:N}:{rateLimit.RequestLimit}:{rateLimit.Window.Ticks}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimit.RequestLimit,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = rateLimit.Window,
            });
    }
}
