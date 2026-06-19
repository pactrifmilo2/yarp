using System.Net;
using System.Reflection;
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
using MyProxy.Infrastructure.Monitoring;
using MyProxy.Infrastructure.Persistence;
using MyProxy.Infrastructure.Proxy;

namespace MyProxy.Tests.Admin;

public class MonitoringApiTests
{
    [Fact]
    public async Task Monitoring_summary_endpoint_returns_gateway_aggregates()
    {
        var now = new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);
        var databaseName = Guid.NewGuid().ToString();
        using var host = await CreateHostAsync(databaseName, now);
        var flightClient = Client.Create("Flight Ops", "flight-key-hash", new[] { "read:flights" });

        await using (var dbContext = new GatewayDbContext(CreateOptions(databaseName)))
        {
            dbContext.Clients.Add(flightClient);
            dbContext.AuditEntries.AddRange(
                CreateAuditEntry(flightClient.Id, 200, 100, now.AddMinutes(-1)),
                CreateAuditEntry(flightClient.Id, 500, 400, now));
            await dbContext.SaveChangesAsync();
        }

        var response = await host.GetTestClient().GetAsync("/api/control/monitoring/summary?windowMinutes=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var summary = await JsonDocument.ParseAsync(responseStream);
        var root = summary.RootElement;
        Assert.Equal(2, root.GetProperty("windowMinutes").GetInt32());
        Assert.Equal(2, root.GetProperty("totalRequests").GetInt32());
        Assert.Equal(1, root.GetProperty("errorCount").GetInt32());
        Assert.Equal(50, root.GetProperty("errorRatePercent").GetDouble());
        Assert.Equal("Flight Ops", root.GetProperty("topClients")[0].GetProperty("clientName").GetString());
        Assert.Equal(2, root.GetProperty("statusCodeBreakdown").GetArrayLength());
    }

    private static async Task<IHost> CreateHostAsync(string databaseName, DateTimeOffset now)
    {
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
                        services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                        services.AddScoped<MonitoringSummaryQuery>();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControlPlaneApi());
                    });
            })
            .StartAsync();
    }

    private static AuditEntry CreateAuditEntry(
        Guid? clientId,
        int statusCode,
        double latencyMilliseconds,
        DateTimeOffset timestamp)
    {
        var auditEntry = AuditEntry.Create(
            clientId,
            "127.0.0.1",
            "GET",
            "/api/flights",
            statusCode,
            TimeSpan.FromMilliseconds(latencyMilliseconds));
        typeof(AuditEntry)
            .GetProperty(nameof(AuditEntry.Timestamp), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(auditEntry, timestamp);

        return auditEntry;
    }

    private static DbContextOptions<GatewayDbContext> CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
