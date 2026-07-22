using MyProxy.Domain.Entities;

namespace MyProxy.Infrastructure.Auth;

public sealed class GatewayClientContext
{
    public Client? Client { get; set; }

    public bool UsedIpBypass { get; set; }
}
