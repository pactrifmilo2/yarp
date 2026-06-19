using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MyProxy;
using MyProxy.Infrastructure;
using MyProxy.Infrastructure.Auditing;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.RateLimiting;
using OpenTelemetry.Exporter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddGatewayObservability();

builder.Services.AddReverseProxy();

var app = builder.Build();

app.MapPrometheusScrapingEndpoint()
    .DisableHttpMetrics();

// Register the reverse proxy routes
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseMiddleware<AuditLoggingMiddleware>();
    proxyPipeline.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    proxyPipeline.UseMiddleware<ClientRateLimitingMiddleware>();
});

app.Run();

public partial class Program;