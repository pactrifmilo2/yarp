using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MyProxy.Infrastructure;
using MyProxy.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGatewayInfrastructure(builder.Configuration);

builder.Services.AddReverseProxy();

var app = builder.Build();

// Register the reverse proxy routes
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseMiddleware<ApiKeyAuthenticationMiddleware>();
});

app.Run();