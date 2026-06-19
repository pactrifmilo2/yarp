using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MyProxy.Infrastructure.Metrics;

public sealed class GatewayMetrics : IGatewayMetrics, IDisposable
{
    public const string MeterName = "MyProxy.Gateway";

    private readonly Meter _meter;
    private readonly Counter<long> _requests;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _rateLimited;

    public GatewayMetrics()
    {
        _meter = new Meter(MeterName);
        _requests = _meter.CreateCounter<long>("gateway.requests");
        _requestDuration = _meter.CreateHistogram<double>("gateway.request.duration", unit: "s");
        _rateLimited = _meter.CreateCounter<long>("gateway.rate_limited");
    }

    public void RecordRequest(
        string clientName,
        string method,
        string path,
        int statusCode,
        TimeSpan latency)
    {
        var normalizedClient = NormalizeClient(clientName);
        var normalizedMethod = method.ToUpperInvariant();
        var requestTags = new TagList
        {
            { "client", normalizedClient },
            { "method", normalizedMethod },
            { "path", path },
            { "status_code", statusCode.ToString() },
        };
        var durationTags = new TagList
        {
            { "client", normalizedClient },
            { "method", normalizedMethod },
            { "path", path },
        };

        _requests.Add(1, requestTags);
        _requestDuration.Record(latency.TotalSeconds, durationTags);
    }

    public void RecordRateLimited(string clientName)
    {
        _rateLimited.Add(
            1,
            new TagList
            {
                { "client", NormalizeClient(clientName) },
            });
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string NormalizeClient(string clientName)
    {
        return string.IsNullOrWhiteSpace(clientName) ? "anonymous" : clientName.Trim();
    }
}
