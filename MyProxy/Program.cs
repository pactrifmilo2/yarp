using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MyProxy;
using MyProxy.Infrastructure;
using MyProxy.Infrastructure.Auditing;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Proxy;
using MyProxy.Infrastructure.RateLimiting;
using OpenTelemetry.Exporter;

const string GatewayCorsPolicy = "GatewayCors";

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddGatewayObservability();

builder.Services.AddCors(options =>
{
    options.AddPolicy(GatewayCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddReverseProxy();

var app = builder.Build();

app.UseCors(GatewayCorsPolicy);

app.MapGet("/health", () => TypedResults.Ok(new
{
    service = "MyProxy",
    status = "healthy",
}));

app.MapPrometheusScrapingEndpoint()
    .DisableHttpMetrics();

// Internal endpoint for the Admin to trigger a config reload after route changes.
app.MapPost("/api/reload", IResult (
    HttpContext httpContext,
    DatabaseProxyConfigProvider proxyConfigProvider,
    IApiKeyBypassPolicy apiKeyBypassPolicy) =>
{
    var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
    if (remoteIpAddress is null || !IPAddress.IsLoopback(remoteIpAddress))
    {
        return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
    }

    proxyConfigProvider.Reload();
    apiKeyBypassPolicy.Reload();
    return TypedResults.Ok(new { message = "Proxy config reloaded." });
});

// Register the reverse proxy routes
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseMiddleware<AuditLoggingMiddleware>();
    proxyPipeline.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    proxyPipeline.UseMiddleware<ClientRateLimitingMiddleware>();
});

app.Run();

public partial class Program;
