namespace MyProxy.Domain.Entities;

public sealed class RouteDefinition
{
    private readonly List<Scope> _requiredScopes = [];

    private RouteDefinition()
    {
        RouteId = string.Empty;
        ClusterId = string.Empty;
        Path = string.Empty;
        DestinationAddress = string.Empty;
    }

    private RouteDefinition(
        string routeId,
        string clusterId,
        string path,
        string destinationAddress,
        IEnumerable<string> requiredScopes)
    {
        Id = Guid.NewGuid();
        RouteId = RequireValue(routeId, nameof(routeId));
        ClusterId = RequireValue(clusterId, nameof(clusterId));
        Path = RequireValue(path, nameof(path));
        DestinationAddress = RequireAbsoluteUri(destinationAddress);
        IsEnabled = true;
        CreatedAt = DateTimeOffset.UtcNow;

        foreach (var scope in requiredScopes)
        {
            _requiredScopes.Add(Scope.Create(scope));
        }
    }

    public Guid Id { get; private set; }

    public string RouteId { get; private set; }

    public string ClusterId { get; private set; }

    public string Path { get; private set; }

    public string DestinationAddress { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Scope> RequiredScopes => _requiredScopes.AsReadOnly();

    public static RouteDefinition Create(
        string routeId,
        string clusterId,
        string path,
        string destinationAddress,
        IEnumerable<string> requiredScopes)
    {
        return new RouteDefinition(routeId, clusterId, path, destinationAddress, requiredScopes);
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    private static string RequireAbsoluteUri(string destinationAddress)
    {
        var trimmed = RequireValue(destinationAddress, nameof(destinationAddress));

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Destination address must be an absolute HTTP or HTTPS URI.", nameof(destinationAddress));
        }

        return trimmed;
    }
}
