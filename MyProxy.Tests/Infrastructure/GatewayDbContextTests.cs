using Microsoft.EntityFrameworkCore;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Tests.Infrastructure;

public class GatewayDbContextTests
{
    [Fact]
    public async Task Saves_gateway_data_foundation_entities()
    {
        var options = CreateOptions();
        await using var dbContext = new GatewayDbContext(options);

        var client = Client.Create("Flight Ops", "sha256-key", new[] { "read:flights" });
        client.AssignRateLimit(RateLimit.Create(100, TimeSpan.FromMinutes(1)));

        var route = RouteDefinition.Create(
            "flights-route",
            "flights-cluster",
            "/api/flights/{**catch-all}",
            "https://flights.internal/",
            new[] { "read:flights" });

        var auditEntry = AuditEntry.Create(
            client.Id,
            "127.0.0.1",
            "GET",
            "/api/flights",
            200,
            TimeSpan.FromMilliseconds(42),
            "?page=2");

        dbContext.Clients.Add(client);
        dbContext.Routes.Add(route);
        dbContext.AuditEntries.Add(auditEntry);
        await dbContext.SaveChangesAsync();

        var savedClient = await dbContext.Clients
            .Include(saved => saved.Scopes)
            .Include(saved => saved.RateLimit)
            .SingleAsync();
        var savedRoute = await dbContext.Routes
            .Include(saved => saved.RequiredScopes)
            .SingleAsync();
        var savedAuditEntry = await dbContext.AuditEntries.SingleAsync();

        Assert.Equal("read:flights", Assert.Single(savedClient.Scopes).Name);
        Assert.Equal(100, savedClient.RateLimit?.RequestLimit);
        Assert.Equal("read:flights", Assert.Single(savedRoute.RequiredScopes).Name);
        Assert.Equal(client.Id, savedAuditEntry.ClientId);
        Assert.Equal("?page=2", savedAuditEntry.QueryString);
    }

    [Fact]
    public void Model_uses_expected_postgresql_table_names()
    {
        using var dbContext = new GatewayDbContext(CreateOptions());

        Assert.Equal("clients", dbContext.Model.FindEntityType(typeof(Client))?.GetTableName());
        Assert.Equal("scopes", dbContext.Model.FindEntityType(typeof(Scope))?.GetTableName());
        Assert.Equal("rate_limits", dbContext.Model.FindEntityType(typeof(RateLimit))?.GetTableName());
        Assert.Equal("routes", dbContext.Model.FindEntityType(typeof(RouteDefinition))?.GetTableName());
        Assert.Equal("audit_entries", dbContext.Model.FindEntityType(typeof(AuditEntry))?.GetTableName());
    }

    private static DbContextOptions<GatewayDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }
}
