using Microsoft.EntityFrameworkCore;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Persistence;
using Yarp.ReverseProxy.Configuration;

namespace MyProxy.Infrastructure.Proxy;

public sealed class DatabaseProxyConfigProvider : IProxyConfigProvider
{
    public const string RequiredScopesMetadataKey = "RequiredScopes";

    private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;
    private readonly object _reloadLock = new();
    private volatile DatabaseProxyConfig _config;

    public DatabaseProxyConfigProvider(IDbContextFactory<GatewayDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        _config = LoadConfig();
    }

    public IProxyConfig GetConfig()
    {
        return _config;
    }

    public void Reload()
    {
        lock (_reloadLock)
        {
            var oldConfig = _config;
            _config = LoadConfig();
            oldConfig.SignalChange();
        }
    }

    private DatabaseProxyConfig LoadConfig()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var routes = dbContext.Routes
            .AsNoTracking()
            .Include(route => route.RequiredScopes)
            .Where(route => route.IsEnabled)
            .OrderBy(route => route.RouteId)
            .ToList();

        return new DatabaseProxyConfig(
            MapRoutes(routes),
            MapClusters(routes));
    }

    private static IReadOnlyList<RouteConfig> MapRoutes(IReadOnlyCollection<RouteDefinition> routes)
    {
        return routes
            .Select(route => new RouteConfig
            {
                RouteId = route.RouteId,
                ClusterId = route.ClusterId,
                Match = new RouteMatch
                {
                    Path = route.Path,
                },
                Metadata = MapRouteMetadata(route),
                Transforms = RoutePathTransforms.MapTransforms(route.Path),
            })
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string>? MapRouteMetadata(RouteDefinition route)
    {
        var requiredScopes = route.RequiredScopes
            .Select(scope => scope.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (requiredScopes.Length == 0)
        {
            return null;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RequiredScopesMetadataKey] = string.Join(' ', requiredScopes),
        };
    }

    private static IReadOnlyList<ClusterConfig> MapClusters(IReadOnlyCollection<RouteDefinition> routes)
    {
        return routes
            .GroupBy(route => route.ClusterId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ClusterConfig
            {
                ClusterId = group.Key,
                Destinations = group
                    .Select(route => route.DestinationAddress)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select((address, index) => new
                    {
                        DestinationId = $"destination{index + 1}",
                        Address = address,
                    })
                    .ToDictionary(
                        destination => destination.DestinationId,
                        destination => new DestinationConfig
                        {
                            Address = destination.Address,
                        },
                        StringComparer.OrdinalIgnoreCase),
            })
            .ToArray();
    }
}
