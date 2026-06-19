using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Auditing;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Metrics;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Tests.Auditing;

public class AuditLoggingMiddlewareTests
{
    [Fact]
    public async Task Invoke_writes_audit_entry_for_authenticated_request()
    {
        var options = CreateOptions();
        var client = Client.Create("Flight Ops", "api-key-hash", new[] { "read:flights" });
        await SaveClientAsync(options, client);
        var httpContext = CreateHttpContext("/api/flights", "POST", IPAddress.Parse("10.10.0.5"));
        var clientContext = new GatewayClientContext
        {
            Client = client,
        };
        var metrics = new TestGatewayMetrics();
        var middleware = new AuditLoggingMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status202Accepted;
            return Task.CompletedTask;
        }, metrics);

        await using var dbContext = new GatewayDbContext(options);
        await middleware.InvokeAsync(httpContext, dbContext, clientContext);

        var auditEntry = await dbContext.AuditEntries.SingleAsync();
        Assert.Equal(client.Id, auditEntry.ClientId);
        Assert.Equal("10.10.0.5", auditEntry.IpAddress);
        Assert.Equal("POST", auditEntry.Method);
        Assert.Equal("/api/flights", auditEntry.Path);
        Assert.Equal(StatusCodes.Status202Accepted, auditEntry.StatusCode);
        Assert.True(auditEntry.Latency >= TimeSpan.Zero);
        Assert.True(auditEntry.Timestamp <= DateTimeOffset.UtcNow);
        Assert.Equal("Flight Ops", metrics.LastRequest?.ClientName);
        Assert.Equal("POST", metrics.LastRequest?.Method);
        Assert.Equal("/api/flights", metrics.LastRequest?.Path);
        Assert.Equal(StatusCodes.Status202Accepted, metrics.LastRequest?.StatusCode);
    }

    [Fact]
    public async Task Invoke_writes_audit_entry_without_client_for_rejected_request()
    {
        var options = CreateOptions();
        var httpContext = CreateHttpContext("/api/weather", "GET", IPAddress.Parse("127.0.0.1"));
        var metrics = new TestGatewayMetrics();
        var middleware = new AuditLoggingMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }, metrics);

        await using var dbContext = new GatewayDbContext(options);
        await middleware.InvokeAsync(httpContext, dbContext, new GatewayClientContext());

        var auditEntry = await dbContext.AuditEntries.SingleAsync();
        Assert.Null(auditEntry.ClientId);
        Assert.Equal("127.0.0.1", auditEntry.IpAddress);
        Assert.Equal("GET", auditEntry.Method);
        Assert.Equal("/api/weather", auditEntry.Path);
        Assert.Equal(StatusCodes.Status401Unauthorized, auditEntry.StatusCode);
        Assert.True(auditEntry.Latency >= TimeSpan.Zero);
        Assert.Equal("anonymous", metrics.LastRequest?.ClientName);
        Assert.Equal(StatusCodes.Status401Unauthorized, metrics.LastRequest?.StatusCode);
    }

    private static DefaultHttpContext CreateHttpContext(string path, string method, IPAddress remoteIpAddress)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIpAddress;
        httpContext.Request.Path = path;
        httpContext.Request.Method = method;

        return httpContext;
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

    private sealed class TestGatewayMetrics : IGatewayMetrics
    {
        public RecordedRequest? LastRequest { get; private set; }

        public void RecordRequest(
            string clientName,
            string method,
            string path,
            int statusCode,
            TimeSpan latency)
        {
            LastRequest = new RecordedRequest(clientName, method, path, statusCode, latency);
        }

        public void RecordRateLimited(string clientName)
        {
        }
    }

    private sealed record RecordedRequest(
        string ClientName,
        string Method,
        string Path,
        int StatusCode,
        TimeSpan Latency);
}
