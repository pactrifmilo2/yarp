using System.Net;
using Microsoft.Extensions.Options;
using MyProxy.Infrastructure.Auth;

namespace MyProxy.Tests.Auth;

public class IpAddressApiKeyBypassPolicyTests
{
    [Fact]
    public void IsAllowed_matches_exact_configured_address()
    {
        var policy = CreatePolicy(enabled: true, "172.29.187.90");

        Assert.True(policy.IsAllowed(IPAddress.Parse("172.29.187.90")));
        Assert.False(policy.IsAllowed(IPAddress.Parse("172.29.187.91")));
    }

    [Fact]
    public void IsAllowed_normalizes_ipv4_mapped_ipv6_address()
    {
        var policy = CreatePolicy(enabled: true, "172.29.187.90");

        Assert.True(policy.IsAllowed(IPAddress.Parse("::ffff:172.29.187.90")));
    }

    [Fact]
    public void IsAllowed_returns_false_when_bypass_is_disabled()
    {
        var policy = CreatePolicy(enabled: false, "172.29.187.90");

        Assert.False(policy.IsAllowed(IPAddress.Parse("172.29.187.90")));
    }

    private static IpAddressApiKeyBypassPolicy CreatePolicy(
        bool enabled,
        params string[] allowedIpAddresses)
    {
        return new IpAddressApiKeyBypassPolicy(Options.Create(new ApiKeyBypassOptions
        {
            Enabled = enabled,
            AllowedIpAddresses = allowedIpAddresses,
        }));
    }
}
