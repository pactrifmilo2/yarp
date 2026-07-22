using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Persistence;

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

    [Fact]
    public async Task Reload_applies_enabled_database_addresses()
    {
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestGatewayDbContextFactory(options);
        var policy = CreatePolicy(factory, enabled: true);

        Assert.False(policy.IsAllowed(IPAddress.Parse("10.10.0.5")));

        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.ApiKeyBypassAddresses.Add(
                MyProxy.Domain.Entities.ApiKeyBypassAddress.Create("10.10.0.5", "Test device"));
            await dbContext.SaveChangesAsync();
        }

        policy.Reload();

        Assert.True(policy.IsAllowed(IPAddress.Parse("10.10.0.5")));
    }

    private static IpAddressApiKeyBypassPolicy CreatePolicy(
        bool enabled,
        params string[] allowedIpAddresses)
    {
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return CreatePolicy(new TestGatewayDbContextFactory(options), enabled, allowedIpAddresses);
    }

    private static IpAddressApiKeyBypassPolicy CreatePolicy(
        IDbContextFactory<GatewayDbContext> dbContextFactory,
        bool enabled,
        params string[] allowedIpAddresses)
    {
        return new IpAddressApiKeyBypassPolicy(
            Options.Create(new ApiKeyBypassOptions
            {
                Enabled = enabled,
                AllowedIpAddresses = allowedIpAddresses,
            }),
            dbContextFactory);
    }

    private sealed class TestGatewayDbContextFactory(DbContextOptions<GatewayDbContext> options)
        : IDbContextFactory<GatewayDbContext>
    {
        public GatewayDbContext CreateDbContext()
        {
            return new GatewayDbContext(options);
        }
    }
}
