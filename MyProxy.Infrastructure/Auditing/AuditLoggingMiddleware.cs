using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Metrics;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Infrastructure.Auditing;

public sealed class AuditLoggingMiddleware(
    RequestDelegate next,
    IGatewayMetrics gatewayMetrics)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        GatewayDbContext dbContext,
        GatewayClientContext clientContext)
    {
        var stopwatch = Stopwatch.StartNew();

        await next(httpContext);

        stopwatch.Stop();
        var endpoint = GetEndpoint(httpContext.Request);
        var clientName = clientContext.Client?.Name ??
            (clientContext.UsedIpBypass ? "ip-bypass" : "anonymous");

        gatewayMetrics.RecordRequest(
            clientName,
            httpContext.Request.Method,
            endpoint,
            httpContext.Response.StatusCode,
            stopwatch.Elapsed);

        var auditEntry = AuditEntry.Create(
            clientContext.Client?.Id,
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            httpContext.Request.Method,
            endpoint,
            httpContext.Response.StatusCode,
            stopwatch.Elapsed,
            AuditQueryStringSanitizer.Sanitize(httpContext.Request.Query));

        dbContext.AuditEntries.Add(auditEntry);
        await dbContext.SaveChangesAsync(httpContext.RequestAborted);
    }

    private static string GetEndpoint(HttpRequest request)
    {
        return request.PathBase.Add(request.Path).Value ?? "/";
    }
}
