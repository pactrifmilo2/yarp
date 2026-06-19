namespace MyProxy.Infrastructure.Metrics;

public interface IGatewayMetrics
{
    void RecordRequest(
        string clientName,
        string method,
        string path,
        int statusCode,
        TimeSpan latency);

    void RecordRateLimited(string clientName);
}
