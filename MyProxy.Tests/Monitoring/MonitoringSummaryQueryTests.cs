using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Monitoring;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Tests.Monitoring;

public class MonitoringSummaryQueryTests
{
    [Fact]
    public async Task GetSummaryAsync_returns_windowed_gateway_aggregates()
    {
        var now = new DateTimeOffset(2026, 6, 19, 12, 0, 30, TimeSpan.Zero);
        var options = CreateOptions();
        var flightClient = Client.Create("Flight Ops", "flight-key-hash", new[] { "read:flights" });
        var weatherClient = Client.Create("Weather Ops", "weather-key-hash", new[] { "read:weather" });

        await using (var dbContext = new GatewayDbContext(options))
        {
            dbContext.Clients.AddRange(flightClient, weatherClient);
            dbContext.AuditEntries.AddRange(
                CreateAuditEntry(flightClient.Id, "/api/flights", 200, 100, now.AddMinutes(-2)),
                CreateAuditEntry(flightClient.Id, "/api/flights", 500, 300, now.AddMinutes(-2).AddSeconds(10)),
                CreateAuditEntry(weatherClient.Id, "/api/weather", 404, 900, now.AddMinutes(-1)),
                CreateAuditEntry(flightClient.Id, "/api/old", 200, 50, now.AddMinutes(-10)));
            await dbContext.SaveChangesAsync();
        }

        await using var queryDbContext = new GatewayDbContext(options);
        var query = new MonitoringSummaryQuery(queryDbContext, new FixedTimeProvider(now));

        var summary = await query.GetSummaryAsync(windowMinutes: 3);

        Assert.Equal(3, summary.TotalRequests);
        Assert.Equal(2, summary.ErrorCount);
        Assert.Equal(66.67, summary.ErrorRatePercent, precision: 2);
        Assert.Equal(433.33, summary.AverageLatencyMs, precision: 2);
        Assert.Equal(900, summary.P95LatencyMs);
        Assert.Equal(3, summary.RequestsPerMinute.Count);
        Assert.Equal(2, summary.RequestsPerMinute[0].Count);
        Assert.Equal(1, summary.RequestsPerMinute[1].Count);
        Assert.Equal(0, summary.RequestsPerMinute[2].Count);
        Assert.Equal("Flight Ops", summary.TopClients[0].ClientName);
        Assert.Equal(2, summary.TopClients[0].RequestCount);
        Assert.Equal("Weather Ops", summary.TopClients[1].ClientName);
        Assert.Equal(1, summary.TopClients[1].RequestCount);
        Assert.Equal(1, summary.StatusCodeBreakdown.Single(status => status.StatusCode == 200).Count);
        Assert.Equal(1, summary.StatusCodeBreakdown.Single(status => status.StatusCode == 404).Count);
        Assert.Equal(1, summary.StatusCodeBreakdown.Single(status => status.StatusCode == 500).Count);
    }

    [Fact]
    public async Task GetSummaryAsync_clamps_window_minutes()
    {
        var now = new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = new GatewayDbContext(CreateOptions());
        var query = new MonitoringSummaryQuery(dbContext, new FixedTimeProvider(now));

        var summary = await query.GetSummaryAsync(windowMinutes: 0);

        Assert.Equal(15, summary.WindowMinutes);
        Assert.Equal(15, summary.RequestsPerMinute.Count);
    }

    private static AuditEntry CreateAuditEntry(
        Guid? clientId,
        string path,
        int statusCode,
        double latencyMilliseconds,
        DateTimeOffset timestamp)
    {
        var auditEntry = AuditEntry.Create(
            clientId,
            "127.0.0.1",
            "GET",
            path,
            statusCode,
            TimeSpan.FromMilliseconds(latencyMilliseconds));
        typeof(AuditEntry)
            .GetProperty(nameof(AuditEntry.Timestamp), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(auditEntry, timestamp);

        return auditEntry;
    }

    private static DbContextOptions<GatewayDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
