using MyProxy.Domain.Entities;

namespace MyProxy.Tests.DomainRules;

public class RouteDefinitionTests
{
    [Fact]
    public void Create_assigns_required_scopes()
    {
        var route = RouteDefinition.Create(
            routeId: "flights-route",
            clusterId: "flights-cluster",
            path: "/api/flights/{**catch-all}",
            destinationAddress: "https://flights.internal/",
            requiredScopes: new[] { "read:flights" });

        Assert.Equal("flights-route", route.RouteId);
        Assert.Equal("flights-cluster", route.ClusterId);
        Assert.Equal("/api/flights/{**catch-all}", route.Path);
        Assert.Equal("https://flights.internal/", route.DestinationAddress);
        Assert.Contains(route.RequiredScopes, scope => scope.Name == "read:flights");
    }

    [Theory]
    [InlineData("")]
    [InlineData("flights.internal")]
    public void Create_rejects_invalid_destination_address(string destinationAddress)
    {
        Assert.Throws<ArgumentException>(
            () => RouteDefinition.Create(
                "flights-route",
                "flights-cluster",
                "/api/flights/{**catch-all}",
                destinationAddress,
                Array.Empty<string>()));
    }
}
