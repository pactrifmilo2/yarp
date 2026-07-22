using MyProxy.Domain.Entities;

namespace MyProxy.Tests.Domain;

public class ApiKeyBypassAddressTests
{
    [Fact]
    public void Create_normalizes_ipv4_mapped_ipv6_address()
    {
        var address = ApiKeyBypassAddress.Create("::ffff:172.29.187.90", "  ATFM server  ");

        Assert.Equal("172.29.187.90", address.Address);
        Assert.Equal("ATFM server", address.Description);
        Assert.True(address.IsEnabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public void Create_rejects_invalid_address(string value)
    {
        Assert.Throws<ArgumentException>(() => ApiKeyBypassAddress.Create(value));
    }

    [Fact]
    public void Update_changes_description_and_enabled_state()
    {
        var address = ApiKeyBypassAddress.Create("10.0.0.1");

        address.Update("Workstation", isEnabled: false);

        Assert.Equal("Workstation", address.Description);
        Assert.False(address.IsEnabled);
    }
}
