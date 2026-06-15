using Microsoft.EntityFrameworkCore;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Persistence;
using MyProxy.Infrastructure.Proxy;

namespace MyProxy.Tests.Infrastructure;

public class DatabaseProxyConfigProviderTests
{
    [Fact]
    public async Task GetConfig_maps_enabled_database_routes_to_yarp_routes_and_clusters()
    {
        var options = CreateOptions();
        await using var dbContext = new GatewayDbContext(options);
        dbContext.Routes.Add(RouteDefinition.Create(
            "flights-route",
            "flights-cluster",
            "/api/flights/{**catch-all}",
            "https://flights.internal/",
            new[] { "read:flights" }));
        await dbContext.SaveChangesAsync();

        var provider = new DatabaseProxyConfigProvider(new TestGatewayDbContextFactory(options));

        var config = provider.GetConfig();

        var route = Assert.Single(config.Routes);
        Assert.Equal("flights-route", route.RouteId);
        Assert.Equal("flights-cluster", route.ClusterId);
        Assert.Equal("/api/flights/{**catch-all}", route.Match.Path);
        Assert.Equal("read:flights", route.Metadata?["RequiredScopes"]);

        var cluster = Assert.Single(config.Clusters);
        Assert.Equal("flights-cluster", cluster.ClusterId);
        Assert.NotNull(cluster.Destinations);

        var destination = Assert.Single(cluster.Destinations);
        Assert.Equal("destination1", destination.Key);
        Assert.Equal("https://flights.internal/", destination.Value.Address);
    }

    [Fact]
    public async Task GetConfig_groups_destinations_by_cluster()
    {
        var options = CreateOptions();
        await using var dbContext = new GatewayDbContext(options);
        dbContext.Routes.AddRange(
            RouteDefinition.Create(
                "flights-route",
                "operations-cluster",
                "/api/flights/{**catch-all}",
                "https://flights.internal/",
                Array.Empty<string>()),
            RouteDefinition.Create(
                "notams-route",
                "operations-cluster",
                "/api/notams/{**catch-all}",
                "https://notams.internal/",
                Array.Empty<string>()));
        await dbContext.SaveChangesAsync();

        var provider = new DatabaseProxyConfigProvider(new TestGatewayDbContextFactory(options));

        var cluster = Assert.Single(provider.GetConfig().Clusters);

        Assert.Equal("operations-cluster", cluster.ClusterId);
        Assert.NotNull(cluster.Destinations);
        Assert.Equal(
            new[] { "https://flights.internal/", "https://notams.internal/" },
            cluster.Destinations.Values.Select(destination => destination.Address));
    }

    [Fact]
    public async Task Reload_swaps_config_snapshot_and_signals_previous_change_token()
    {
        var options = CreateOptions();
        await using var dbContext = new GatewayDbContext(options);
        dbContext.Routes.Add(RouteDefinition.Create(
            "flights-route",
            "flights-cluster",
            "/api/flights/{**catch-all}",
            "https://flights.internal/",
            Array.Empty<string>()));
        await dbContext.SaveChangesAsync();

        var provider = new DatabaseProxyConfigProvider(new TestGatewayDbContextFactory(options));
        var originalConfig = provider.GetConfig();
        var changed = false;
        using var registration = originalConfig.ChangeToken.RegisterChangeCallback(_ => changed = true, null);

        dbContext.Routes.Add(RouteDefinition.Create(
            "notams-route",
            "notams-cluster",
            "/api/notams/{**catch-all}",
            "https://notams.internal/",
            Array.Empty<string>()));
        await dbContext.SaveChangesAsync();

        provider.Reload();

        Assert.True(changed);
        Assert.NotSame(originalConfig, provider.GetConfig());
        Assert.Equal(2, provider.GetConfig().Routes.Count);
    }

    private static DbContextOptions<GatewayDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private sealed class TestGatewayDbContextFactory(DbContextOptions<GatewayDbContext> options)
        : IDbContextFactory<GatewayDbContext>
    {
        public GatewayDbContext CreateDbContext()
        {
            return new GatewayDbContext(options);
        }
    }
}
