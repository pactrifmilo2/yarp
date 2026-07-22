using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Infrastructure.Auth;

public interface IApiKeyBypassPolicy
{
    bool IsAllowed(IPAddress? remoteIpAddress);

    void Reload();
}

public sealed class IpAddressApiKeyBypassPolicy : IApiKeyBypassPolicy
{
    private readonly bool _enabled;
    private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;
    private readonly IPAddress[] _configuredIpAddresses;
    private readonly object _reloadLock = new();
    private volatile HashSet<IPAddress> _allowedIpAddresses;

    public IpAddressApiKeyBypassPolicy(
        IOptions<ApiKeyBypassOptions> options,
        IDbContextFactory<GatewayDbContext> dbContextFactory)
    {
        _enabled = options.Value.Enabled;
        _dbContextFactory = dbContextFactory;
        _configuredIpAddresses = options.Value.AllowedIpAddresses
            .Select(IPAddress.Parse)
            .Select(Normalize)
            .ToArray();
        _allowedIpAddresses = LoadAllowedIpAddresses();
    }

    public bool IsAllowed(IPAddress? remoteIpAddress)
    {
        return _enabled &&
               remoteIpAddress is not null &&
               _allowedIpAddresses.Contains(Normalize(remoteIpAddress));
    }

    public void Reload()
    {
        lock (_reloadLock)
        {
            _allowedIpAddresses = LoadAllowedIpAddresses();
        }
    }

    private HashSet<IPAddress> LoadAllowedIpAddresses()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var databaseAddresses = dbContext.ApiKeyBypassAddresses
            .AsNoTracking()
            .Where(address => address.IsEnabled)
            .Select(address => address.Address)
            .ToArray();

        return _configuredIpAddresses
            .Concat(databaseAddresses.Select(IPAddress.Parse))
            .Select(Normalize)
            .ToHashSet();
    }

    private static IPAddress Normalize(IPAddress ipAddress)
    {
        return ipAddress.IsIPv4MappedToIPv6
            ? ipAddress.MapToIPv4()
            : ipAddress;
    }
}
