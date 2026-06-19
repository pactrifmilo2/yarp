using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyProxy.Admin.ControlPlane;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Persistence;
using MyProxy.Infrastructure.Proxy;

namespace MyProxy.Tests.Admin;

public class ControlPlaneApiTests
{
    [Fact]
    public async Task Client_endpoints_create_update_list_and_delete_clients()
    {
        using var host = await CreateHostAsync();
        var client = host.GetTestClient();

        var createResponse = await client.PostAsJsonAsync("/api/control/clients", new
        {
            name = "Flight Ops",
            scopes = new[] { "read:flights", "write:flights" },
            expiresAt = DateTimeOffset.UtcNow.AddDays(30),
            rateLimit = new
            {
                requestLimit = 120,
                windowSeconds = 60,
            },
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadJsonAsync(createResponse);
        var id = created.RootElement.GetProperty("id").GetGuid();
        var apiKey = created.RootElement.GetProperty("apiKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(apiKey));
        Assert.StartsWith("mp_", apiKey, StringComparison.Ordinal);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/control/clients");
        var listedClient = Assert.Single(list.EnumerateArray());
        Assert.Equal("Flight Ops", listedClient.GetProperty("name").GetString());
        Assert.Equal(new[] { "read:flights", "write:flights" }, ReadStringArray(listedClient, "scopes"));
        Assert.Equal(120, listedClient.GetProperty("rateLimit").GetProperty("requestLimit").GetInt32());

        var updateResponse = await client.PutAsJsonAsync($"/api/control/clients/{id}", new
        {
            name = "Flight Operations",
            isActive = false,
            scopes = new[] { "read:flights" },
            expiresAt = (DateTimeOffset?)null,
            rateLimit = new
            {
                requestLimit = 30,
                windowSeconds = 10,
            },
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadJsonAsync(updateResponse);
        Assert.Equal("Flight Operations", updated.RootElement.GetProperty("name").GetString());
        Assert.False(updated.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal(new[] { "read:flights" }, ReadStringArray(updated.RootElement, "scopes"));
        Assert.Equal(30, updated.RootElement.GetProperty("rateLimit").GetProperty("requestLimit").GetInt32());

        var regenerateResponse = await client.PostAsync($"/api/control/clients/{id}/regenerate-api-key", null);
        Assert.Equal(HttpStatusCode.OK, regenerateResponse.StatusCode);
        var regenerated = await ReadJsonAsync(regenerateResponse);
        var newApiKey = regenerated.RootElement.GetProperty("apiKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(newApiKey));
        Assert.NotEqual(apiKey, newApiKey);

        var deleteResponse = await client.DeleteAsync($"/api/control/clients/{id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var emptyList = await client.GetFromJsonAsync<JsonElement>("/api/control/clients");
        Assert.Empty(emptyList.EnumerateArray());
    }

    [Fact]
    public async Task List_clients_does_not_return_api_keys()
    {
        using var host = await CreateHostAsync();
        var client = host.GetTestClient();

        await client.PostAsJsonAsync("/api/control/clients", new
        {
            name = "Flight Ops",
            scopes = new[] { "read:flights" },
            expiresAt = (DateTimeOffset?)null,
            rateLimit = (object?)null,
        });

        var list = await client.GetFromJsonAsync<JsonElement>("/api/control/clients");
        var listedClient = Assert.Single(list.EnumerateArray());
        Assert.False(listedClient.TryGetProperty("apiKey", out _));
    }

    [Fact]
    public async Task Route_endpoints_create_update_list_and_delete_routes()
    {
        using var host = await CreateHostAsync();
        var client = host.GetTestClient();

        var createResponse = await client.PostAsJsonAsync("/api/control/routes", new
        {
            routeId = "flights-route",
            clusterId = "flights-cluster",
            path = "/api/flights/{**catch-all}",
            destinationAddress = "https://flights.internal/",
            requiredScopes = new[] { "read:flights" },
            isEnabled = true,
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadJsonAsync(createResponse);
        var id = created.RootElement.GetProperty("id").GetGuid();

        var updateResponse = await client.PutAsJsonAsync($"/api/control/routes/{id}", new
        {
            routeId = "flights-v2",
            clusterId = "flights-cluster",
            path = "/api/v2/flights/{**catch-all}",
            destinationAddress = "https://flights-v2.internal/",
            requiredScopes = new[] { "read:flights", "write:flights" },
            isEnabled = false,
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/control/routes");
        var route = Assert.Single(list.EnumerateArray());
        Assert.Equal("flights-v2", route.GetProperty("routeId").GetString());
        Assert.False(route.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(new[] { "read:flights", "write:flights" }, ReadStringArray(route, "requiredScopes"));

        var deleteResponse = await client.DeleteAsync($"/api/control/routes/{id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var emptyList = await client.GetFromJsonAsync<JsonElement>("/api/control/routes");
        Assert.Empty(emptyList.EnumerateArray());
    }

    [Fact]
    public async Task Audit_entries_endpoint_returns_newest_entries_with_client_name()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = CreateOptions(databaseName);
        var gatewayClient = Client.Create("Flight Ops", "api-key-hash", new[] { "read:flights" });
        await using (var dbContext = new GatewayDbContext(options))
        {
            dbContext.Clients.Add(gatewayClient);
            dbContext.AuditEntries.Add(AuditEntry.Create(
                gatewayClient.Id,
                "10.0.0.5",
                "get",
                "/api/flights",
                200,
                TimeSpan.FromMilliseconds(24)));
            await dbContext.SaveChangesAsync();
        }

        using var host = await CreateHostAsync(databaseName);
        var client = host.GetTestClient();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/control/audit-entries");

        var entry = Assert.Single(list.EnumerateArray());
        Assert.Equal("Flight Ops", entry.GetProperty("clientName").GetString());
        Assert.Equal("GET", entry.GetProperty("method").GetString());
        Assert.Equal("/api/flights", entry.GetProperty("path").GetString());
        Assert.Equal(200, entry.GetProperty("statusCode").GetInt32());
    }

    private static async Task<IHost> CreateHostAsync(string? databaseName = null)
    {
        databaseName ??= Guid.NewGuid().ToString();

        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddDbContextFactory<GatewayDbContext>(options =>
                            options.UseInMemoryDatabase(databaseName));
                        services.AddScoped(provider =>
                            provider.GetRequiredService<IDbContextFactory<GatewayDbContext>>().CreateDbContext());
                        services.AddSingleton<IApiKeyHasher, Sha256ApiKeyHasher>();
                        services.AddSingleton<IApiKeyGenerator, CryptographicApiKeyGenerator>();
                        services.AddSingleton<DatabaseProxyConfigProvider>();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControlPlaneApi());
                    });
            })
            .StartAsync();
    }

    private static DbContextOptions<GatewayDbContext> CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var responseStream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(responseStream);
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetString())
            .OfType<string>()
            .ToArray();
    }
}
