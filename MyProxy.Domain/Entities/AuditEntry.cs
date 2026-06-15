namespace MyProxy.Domain.Entities;

public sealed class AuditEntry
{
    private AuditEntry()
    {
        IpAddress = string.Empty;
        Method = string.Empty;
        Path = string.Empty;
    }

    private AuditEntry(
        Guid? clientId,
        string ipAddress,
        string method,
        string path,
        int statusCode,
        TimeSpan latency)
    {
        Id = Guid.NewGuid();
        Timestamp = DateTimeOffset.UtcNow;
        ClientId = clientId;
        IpAddress = RequireValue(ipAddress, nameof(ipAddress));
        Method = RequireValue(method, nameof(method)).ToUpperInvariant();
        Path = RequireValue(path, nameof(path));
        StatusCode = statusCode;
        Latency = latency;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset Timestamp { get; private set; }

    public Guid? ClientId { get; private set; }

    public Client? Client { get; private set; }

    public string IpAddress { get; private set; }

    public string Method { get; private set; }

    public string Path { get; private set; }

    public int StatusCode { get; private set; }

    public TimeSpan Latency { get; private set; }

    public static AuditEntry Create(
        Guid? clientId,
        string ipAddress,
        string method,
        string path,
        int statusCode,
        TimeSpan latency)
    {
        if (statusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Status code must be a valid HTTP status code.");
        }

        if (latency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(latency), "Latency cannot be negative.");
        }

        return new AuditEntry(clientId, ipAddress, method, path, statusCode, latency);
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
