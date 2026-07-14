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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddGatewayObservability();

builder.Services.AddReverseProxy();

var app = builder.Build();

app.MapPrometheusScrapingEndpoint()
    .DisableHttpMetrics();

// Internal endpoint for the Admin to trigger a config reload after route changes.
app.MapPost("/api/reload", (DatabaseProxyConfigProvider proxyConfigProvider) =>
{
    proxyConfigProvider.Reload();
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