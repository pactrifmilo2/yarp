using System.Diagnostics.Metrics;
using MyProxy.Infrastructure.Metrics;

namespace MyProxy.Tests.Metrics;

public class GatewayMetricsTests
{
    [Fact]
    public void RecordRequest_publishes_request_count_and_duration()
    {
        using var recorder = new MeterRecorder(GatewayMetrics.MeterName);
        using var metrics = new GatewayMetrics();

        metrics.RecordRequest(
            clientName: "Flight Ops",
            method: "POST",
            path: "/api/flights",
            statusCode: 202,
            latency: TimeSpan.FromMilliseconds(125));

        var requestCount = Assert.Single(recorder.LongMeasurements, measurement => measurement.Name == "gateway.requests");
        Assert.Equal(1, requestCount.Value);
        Assert.Equal("Flight Ops", requestCount.Tags["client"]);
        Assert.Equal("POST", requestCount.Tags["method"]);
        Assert.Equal("/api/flights", requestCount.Tags["path"]);
        Assert.Equal("202", requestCount.Tags["status_code"]);

        var duration = Assert.Single(recorder.DoubleMeasurements, measurement => measurement.Name == "gateway.request.duration");
        Assert.Equal(0.125, duration.Value, precision: 3);
        Assert.Equal("Flight Ops", duration.Tags["client"]);
        Assert.Equal("POST", duration.Tags["method"]);
        Assert.Equal("/api/flights", duration.Tags["path"]);
    }

    [Fact]
    public void RecordRateLimited_publishes_rate_limited_count()
    {
        using var recorder = new MeterRecorder(GatewayMetrics.MeterName);
        using var metrics = new GatewayMetrics();

        metrics.RecordRateLimited("Weather Ops");

        var measurement = Assert.Single(recorder.LongMeasurements, measurement => measurement.Name == "gateway.rate_limited");
        Assert.Equal(1, measurement.Value);
        Assert.Equal("Weather Ops", measurement.Tags["client"]);
    }

    private sealed class MeterRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();

        public MeterRecorder(string meterName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                LongMeasurements.Add(new Measurement<long>(instrument.Name, value, CopyTags(tags))));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                DoubleMeasurements.Add(new Measurement<double>(instrument.Name, value, CopyTags(tags))));
            _listener.Start();
        }

        public List<Measurement<long>> LongMeasurements { get; } = [];

        public List<Measurement<double>> DoubleMeasurements { get; } = [];

        public void Dispose()
        {
            _listener.Dispose();
        }

        private static Dictionary<string, string?> CopyTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var copiedTags = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                copiedTags[tag.Key] = tag.Value?.ToString();
            }

            return copiedTags;
        }
    }

    private sealed record Measurement<T>(
        string Name,
        T Value,
        IReadOnlyDictionary<string, string?> Tags);
}
