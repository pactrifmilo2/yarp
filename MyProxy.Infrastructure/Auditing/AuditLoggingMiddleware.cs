using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Infrastructure.Auditing;

public sealed class AuditLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        GatewayDbContext dbContext,
        GatewayClientContext clientContext)
    {
        var stopwatch = Stopwatch.StartNew();

        await next(httpContext);

        stopwatch.Stop();

        var auditEntry = AuditEntry.Create(
            clientContext.Client?.Id,
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            httpContext.Request.Method,
            GetEndpoint(httpContext.Request),
            httpContext.Response.StatusCode,
            stopwatch.Elapsed);

        dbContext.AuditEntries.Add(auditEntry);
        await dbContext.SaveChangesAsync(httpContext.RequestAborted);
    }

    private static string GetEndpoint(HttpRequest request)
    {
        return request.PathBase.Add(request.Path).Value ?? "/";
    }
}
