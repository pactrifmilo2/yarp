using System.Net;

namespace MyProxy.Domain.Entities;

public sealed class ApiKeyBypassAddress
{
    private ApiKeyBypassAddress()
    {
        Address = string.Empty;
    }

    private ApiKeyBypassAddress(string address, string? description)
    {
        Id = Guid.NewGuid();
        Address = NormalizeAddress(address);
        Description = NormalizeDescription(description);
        IsEnabled = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Address { get; private set; }

    public string? Description { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ApiKeyBypassAddress Create(string address, string? description = null)
    {
        return new ApiKeyBypassAddress(address, description);
    }

    public void Update(string? description, bool isEnabled)
    {
        Description = NormalizeDescription(description);
        IsEnabled = isEnabled;
    }

    private static string NormalizeAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address) || !IPAddress.TryParse(address.Trim(), out var parsed))
        {
            throw new ArgumentException("Address must be a valid IPv4 or IPv6 address.", nameof(address));
        }

        return parsed.IsIPv4MappedToIPv6
            ? parsed.MapToIPv4().ToString()
            : parsed.ToString();
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
