using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Persistence;
using MyProxy.Infrastructure.Proxy;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace MyProxy.Tests.Auth;

public class ApiKeyAuthenticationMiddlewareTests
{
    [Fact]
    public async Task Invoke_allows_valid_api_key_with_required_scope()
    {
        var options = CreateOptions();
        var hasher = new Sha256ApiKeyHasher();
        var client = Client.Create("Flight Ops", hasher.Hash("valid-key"), new[] { "read:flights" });
        client.AssignRateLimit(RateLimit.Create(requestLimit: 120, window: TimeSpan.FromMinutes(1)));
        await SaveClientAsync(options, client);
        var context = CreateHttpContext("valid-key", "read:flights");
        var clientContext = new GatewayClientContext();
        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            new DatabaseClientApiKeyResolver(new TestGatewayDbContextFactory(options), hasher),
            clientContext,
            CreateBypassPolicy());

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(120, clientContext.Client?.RateLimit?.RequestLimit);
    }

    [Fact]
    public async Task Invoke_rejects_missing_api_key()
    {
        var context = CreateHttpContext(apiKey: null, requiredScopes: null);
        var middleware = new ApiKeyAuthenticationMiddleware(_ => throw new InvalidOperationException("Next should not run."));

        await middleware.InvokeAsync(
            context,
            new DatabaseClientApiKeyResolver(new TestGatewayDbContextFactory(CreateOptions()), new Sha256ApiKeyHasher()),
            new GatewayClientContext(),
            CreateBypassPolicy());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_rejects_invalid_api_key()
    {
        var options = CreateOptions();
        await SaveClientAsync(
            options,
            Client.Create("Flight Ops", new Sha256ApiKeyHasher().Hash("valid-key"), new[] { "read:flights" }));
        var context = CreateHttpContext("wrong-key", requiredScopes: null);
        var middleware = new ApiKeyAuthenticationMiddleware(_ => throw new InvalidOperationException("Next should not run."));

        await middleware.InvokeAsync(
            context,
            new DatabaseClientApiKeyResolver(new TestGatewayDbContextFactory(options), new Sha256ApiKeyHasher()),
            new GatewayClientContext(),
            CreateBypassPolicy());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_rejects_expired_api_key()
    {
        var options = CreateOptions();
        var hasher = new Sha256ApiKeyHasher();
        var client = Client.Create("Flight Ops", hasher.Hash("expired-key"), new[] { "read:flights" });
        client.SetExpiration(DateTimeOffset.UtcNow.AddMinutes(-1));
        await SaveClientAsync(options, client);
        var context = CreateHttpContext("expired-key", requiredScopes: null);
        var middleware = new ApiKeyAuthenticationMiddleware(_ => throw new InvalidOperationException("Next should not run."));

        await middleware.InvokeAsync(
            context,
            new DatabaseClientApiKeyResolver(new TestGatewayDbContextFactory(options), hasher),
            new GatewayClientContext(),
            CreateBypassPolicy());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_rejects_valid_api_key_without_required_scope()
    {
        var options = CreateOptions();
        var hasher = new Sha256ApiKeyHasher();
        var client = Client.Create("Weather Ops", hasher.Hash("weather-key"), new[] { "read:weather" });
        await SaveClientAsync(options, client);
        var context = CreateHttpContext("weather-key", "read:flights");
        var middleware = new ApiKeyAuthenticationMiddleware(_ => throw new InvalidOperationException("Next should not run."));

        await middleware.InvokeAsync(
            context,
            new DatabaseClientApiKeyResolver(new TestGatewayDbContextFactory(options), hasher),
            new GatewayClientContext(),
            CreateBypassPolicy());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_allows_configured_ip_without_api_key_or_scopes()
    {
        var context = CreateHttpContext(apiKey: null, requiredScopes: "admin:all");
        context.Connection.RemoteIpAddress = IPAddress.Parse("172.29.187.90");
        var clientContext = new GatewayClientContext();
        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            new DatabaseClientApiKeyResolver(
                new TestGatewayDbContextFactory(CreateOptions()),
                new Sha256ApiKeyHasher()),
            clientContext,
            CreateBypassPolicy(enabled: true, "172.29.187.90"));

        Assert.True(nextCalled);
        Assert.True(clientContext.UsedIpBypass);
        Assert.Null(clientContext.Client);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_requires_api_key_when_remote_ip_is_not_configured()
    {
        var context = CreateHttpContext(apiKey: null, requiredScopes: null);
        context.Connection.RemoteIpAddress = IPAddress.Parse("172.29.187.91");
        var middleware = new ApiKeyAuthenticationMiddleware(
            _ => throw new InvalidOperationException("Next should not run."));

        await middleware.InvokeAsync(
            context,
            new DatabaseClientApiKeyResolver(
                new TestGatewayDbContextFactory(CreateOptions()),
                new Sha256ApiKeyHasher()),
            new GatewayClientContext(),
            CreateBypassPolicy(enabled: true, "172.29.187.90"));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateHttpContext(string? apiKey, string? requiredScopes)
    {
        var context = new DefaultHttpContext();

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            context.Request.Headers[ApiKeyAuthenticationMiddleware.ApiKeyHeaderName] = apiKey;
        }

        if (!string.IsNullOrWhiteSpace(requiredScopes))
        {
            var clusterModel = new ClusterModel(
                new ClusterConfig { ClusterId = "test-cluster" },
                new HttpMessageInvoker(new SocketsHttpHandler()));
            var cluster = new ClusterState("test-cluster", clusterModel);
            var route = new RouteModel(
                new RouteConfig
                {
                    RouteId = "test-route",
                    ClusterId = "test-cluster",
                    Match = new RouteMatch { Path = "/api/{**catch-all}" },
                    Metadata = new Dictionary<string, string>
                    {
                        [DatabaseProxyConfigProvider.RequiredScopesMetadataKey] = requiredScopes,
                    },
                },
                cluster,
                HttpTransformer.Default);

            context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature
            {
                Route = route,
                Cluster = clusterModel,
            });
        }

        return context;
    }

    private static async Task SaveClientAsync(DbContextOptions<GatewayDbContext> options, Client client)
    {
        await using var dbContext = new GatewayDbContext(options);
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync();
    }

    private static DbContextOptions<GatewayDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private static IApiKeyBypassPolicy CreateBypassPolicy(
        bool enabled = false,
        params string[] allowedIpAddresses)
    {
        return new IpAddressApiKeyBypassPolicy(Options.Create(new ApiKeyBypassOptions
        {
            Enabled = enabled,
            AllowedIpAddresses = allowedIpAddresses,
        }));
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
