namespace MyProxy.Domain.Entities;

public sealed class Client
{
    private readonly List<Scope> _scopes = [];

    private Client()
    {
        Name = string.Empty;
        ApiKeyHash = string.Empty;
    }

    private Client(string name, string apiKeyHash, IEnumerable<string> scopes)
    {
        Id = Guid.NewGuid();
        Name = RequireValue(name, nameof(name));
        ApiKeyHash = RequireValue(apiKeyHash, nameof(apiKeyHash));
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;

        foreach (var scope in scopes)
        {
            _scopes.Add(Scope.Create(scope));
        }
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string ApiKeyHash { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public IReadOnlyCollection<Scope> Scopes => _scopes.AsReadOnly();

    public RateLimit? RateLimit { get; private set; }

    public static Client Create(string name, string apiKeyHash, IEnumerable<string> scopes)
    {
        return new Client(name, apiKeyHash, scopes);
    }

    public void AssignRateLimit(RateLimit rateLimit)
    {
        ArgumentNullException.ThrowIfNull(rateLimit);
        RateLimit = rateLimit;
    }

    public void SetExpiration(DateTimeOffset? expiresAt)
    {
        ExpiresAt = expiresAt;
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
