using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyProxy.Admin.ControlPlane;
using MyProxy.Admin.OpenApi;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Tests.Admin;

public class OpenApiTests
{
    [Fact]
    public async Task OpenApi_document_includes_control_plane_paths()
    {
        using var app = await CreateHostAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("MyProxy Control Plane API", root.GetProperty("info").GetProperty("title").GetString());

        var paths = root.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/control/clients", out _));
        Assert.True(paths.TryGetProperty("/api/control/routes", out _));
        Assert.True(paths.TryGetProperty("/api/control/scopes", out _));
        Assert.True(paths.TryGetProperty("/api/control/audit-entries", out _));
    }

    [Fact]
    public async Task Swagger_ui_is_available()
    {
        using var app = await CreateHostAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<WebApplication> CreateHostAsync()
    {
        var databaseName = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddGatewayOpenApi();
        builder.Services.AddDbContextFactory<GatewayDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped(provider =>
            provider.GetRequiredService<IDbContextFactory<GatewayDbContext>>().CreateDbContext());
        builder.Services.AddSingleton<IApiKeyHasher, Sha256ApiKeyHasher>();
        builder.Services.AddSingleton<IApiKeyGenerator, CryptographicApiKeyGenerator>();
        builder.Services.AddSingleton(GatewayReloadClientTestFactory.Create());

        var app = builder.Build();
        app.UseRouting();
        app.MapGatewayOpenApi();

        var gatewayReloadClient = app.Services.GetRequiredService<GatewayReloadClient>();
        app.MapControlPlaneApi(gatewayReloadClient);

        await app.StartAsync();
        return app;
    }
}
