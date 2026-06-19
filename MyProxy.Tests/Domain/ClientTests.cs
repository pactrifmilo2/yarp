using MyProxy.Domain.Entities;

namespace MyProxy.Tests.DomainRules;

public class ClientTests
{
    [Fact]
    public void Create_hashes_api_key_and_assigns_scope()
    {
        var client = Client.Create(
            name: "Flight Ops",
            apiKeyHash: "sha256-key",
            scopes: new[] { " read:flights " });

        Assert.Equal("Flight Ops", client.Name);
        Assert.Equal("sha256-key", client.ApiKeyHash);
        Assert.Contains(client.Scopes, scope => scope.Name == "read:flights");
        Assert.True(client.IsActive);
    }

    [Fact]
    public void Create_rejects_invalid_scope()
    {
        Assert.Throws<ArgumentException>(
            () => Client.Create("Flight Ops", "sha256-key", new[] { "readflights" }));
    }

    [Fact]
    public void RotateApiKey_replaces_existing_hash()
    {
        var client = Client.Create("Flight Ops", "old-hash", new[] { "read:flights" });

        client.RotateApiKey("new-hash");

        Assert.Equal("new-hash", client.ApiKeyHash);
    }

    [Fact]
    public void RotateApiKey_rejects_empty_hash()
    {
        var client = Client.Create("Flight Ops", "old-hash", new[] { "read:flights" });

        Assert.Throws<ArgumentException>(() => client.RotateApiKey(" "));
    }
}
