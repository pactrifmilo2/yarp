using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MyProxy.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGatewayInfrastructure(builder.Configuration);

builder.Services.AddReverseProxy();

var app = builder.Build();

// Register the reverse proxy routes
app.MapReverseProxy();

app.Run();