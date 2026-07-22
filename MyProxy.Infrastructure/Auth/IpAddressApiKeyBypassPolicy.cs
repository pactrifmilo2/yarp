using System.Net;
using Microsoft.Extensions.Options;

namespace MyProxy.Infrastructure.Auth;

public interface IApiKeyBypassPolicy
{
    bool IsAllowed(IPAddress? remoteIpAddress);
}

public sealed class IpAddressApiKeyBypassPolicy : IApiKeyBypassPolicy
{
    private readonly bool _enabled;
    private readonly HashSet<IPAddress> _allowedIpAddresses;

    public IpAddressApiKeyBypassPolicy(IOptions<ApiKeyBypassOptions> options)
    {
        _enabled = options.Value.Enabled;
        _allowedIpAddresses = options.Value.AllowedIpAddresses
            .Select(IPAddress.Parse)
            .Select(Normalize)
            .ToHashSet();
    }

    public bool IsAllowed(IPAddress? remoteIpAddress)
    {
        return _enabled &&
               remoteIpAddress is not null &&
               _allowedIpAddresses.Contains(Normalize(remoteIpAddress));
    }

    private static IPAddress Normalize(IPAddress ipAddress)
    {
        return ipAddress.IsIPv4MappedToIPv6
            ? ipAddress.MapToIPv4()
            : ipAddress;
    }
}
