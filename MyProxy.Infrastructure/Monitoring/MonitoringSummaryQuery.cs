using Microsoft.EntityFrameworkCore;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Infrastructure.Monitoring;

public sealed class MonitoringSummaryQuery(
    GatewayDbContext dbContext,
    TimeProvider timeProvider)
{
    private const int DefaultWindowMinutes = 60;
    private const int FallbackWindowMinutes = 15;
    private const int MaximumWindowMinutes = 24 * 60;

    public async Task<MonitoringSummary> GetSummaryAsync(
        int? windowMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveWindowMinutes = NormalizeWindowMinutes(windowMinutes);
        var now = TruncateToMinute(timeProvider.GetUtcNow());
        var bucketStart = now.AddMinutes(-(effectiveWindowMinutes - 1));

        var entries = await dbContext.AuditEntries
            .AsNoTracking()
            .Include(entry => entry.Client)
            .Where(entry => entry.Timestamp >= bucketStart && entry.Timestamp <= timeProvider.GetUtcNow())
            .ToListAsync(cancellationToken);

        var totalRequests = entries.Count;
        var errorCount = entries.Count(entry => entry.StatusCode >= 400);
        var latencies = entries
            .Select(entry => entry.Latency.TotalMilliseconds)
            .OrderBy(latency => latency)
            .ToArray();

        var requestCountsByMinute = entries
            .GroupBy(entry => TruncateToMinute(entry.Timestamp))
            .ToDictionary(group => group.Key, group => group.Count());
        var requestsPerMinute = Enumerable.Range(0, effectiveWindowMinutes)
            .Select(offset =>
            {
                var timestamp = bucketStart.AddMinutes(offset);
                return new RequestsPerMinutePoint(
                    timestamp,
                    requestCountsByMinute.GetValueOrDefault(timestamp));
            })
            .ToArray();

        var topClients = entries
            .GroupBy(entry => entry.Client?.Name ?? "Không xác định")
            .Select(group => new TopClientPoint(group.Key, group.Count()))
            .OrderByDescending(point => point.RequestCount)
            .ThenBy(point => point.ClientName, StringComparer.CurrentCulture)
            .Take(5)
            .ToArray();

        var statusCodeBreakdown = entries
            .GroupBy(entry => entry.StatusCode)
            .Select(group => new StatusCodeBreakdownPoint(group.Key, group.Count()))
            .OrderBy(point => point.StatusCode)
            .ToArray();

        return new MonitoringSummary(
            effectiveWindowMinutes,
            totalRequests,
            errorCount,
            CalculateErrorRate(totalRequests, errorCount),
            CalculateAverageLatency(latencies),
            CalculateP95Latency(latencies),
            requestsPerMinute,
            topClients,
            statusCodeBreakdown);
    }

    private static int NormalizeWindowMinutes(int? windowMinutes)
    {
        var requestedWindowMinutes = windowMinutes ?? DefaultWindowMinutes;
        if (requestedWindowMinutes <= 0)
        {
            return FallbackWindowMinutes;
        }

        return Math.Min(requestedWindowMinutes, MaximumWindowMinutes);
    }

    private static DateTimeOffset TruncateToMinute(DateTimeOffset timestamp)
    {
        return new DateTimeOffset(
            timestamp.Year,
            timestamp.Month,
            timestamp.Day,
            timestamp.Hour,
            timestamp.Minute,
            0,
            timestamp.Offset);
    }

    private static double CalculateErrorRate(int totalRequests, int errorCount)
    {
        return totalRequests == 0 ? 0 : errorCount * 100d / totalRequests;
    }

    private static double CalculateAverageLatency(IReadOnlyCollection<double> latencies)
    {
        return latencies.Count == 0 ? 0 : latencies.Average();
    }

    private static double CalculateP95Latency(IReadOnlyList<double> sortedLatencies)
    {
        if (sortedLatencies.Count == 0)
        {
            return 0;
        }

        var index = Math.Clamp((int)Math.Ceiling(sortedLatencies.Count * 0.95d) - 1, 0, sortedLatencies.Count - 1);
        return sortedLatencies[index];
    }
}

public sealed record MonitoringSummary(
    int WindowMinutes,
    int TotalRequests,
    int ErrorCount,
    double ErrorRatePercent,
    double AverageLatencyMs,
    double P95LatencyMs,
    IReadOnlyList<RequestsPerMinutePoint> RequestsPerMinute,
    IReadOnlyList<TopClientPoint> TopClients,
    IReadOnlyList<StatusCodeBreakdownPoint> StatusCodeBreakdown);

public sealed record RequestsPerMinutePoint(DateTimeOffset Timestamp, int Count);

public sealed record TopClientPoint(string ClientName, int RequestCount);

public sealed record StatusCodeBreakdownPoint(int StatusCode, int Count);
