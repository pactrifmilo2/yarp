using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace MyProxy.Infrastructure.Proxy;

internal sealed class DatabaseProxyConfig : IProxyConfig
{
    private readonly CancellationTokenSource _changeTokenSource = new();

    public DatabaseProxyConfig(
        IReadOnlyList<RouteConfig> routes,
        IReadOnlyList<ClusterConfig> clusters)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = new CancellationChangeToken(_changeTokenSource.Token);
    }

    public IReadOnlyList<RouteConfig> Routes { get; }

    public IReadOnlyList<ClusterConfig> Clusters { get; }

    public IChangeToken ChangeToken { get; }

    public void SignalChange()
    {
        _changeTokenSource.Cancel();
    }
}
