using Microsoft.AspNetCore.Http;
using MyProxy.Infrastructure.Proxy;
using Yarp.ReverseProxy.Model;

namespace MyProxy.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationMiddleware(RequestDelegate next)
{
    public const string ApiKeyHeaderName = "X-API-Key";

    private static readonly char[] ScopeSeparators = [' ', ',', ';'];

    public async Task InvokeAsync(
        HttpContext httpContext,
        IClientApiKeyResolver clientResolver,
        GatewayClientContext clientContext,
        IApiKeyBypassPolicy bypassPolicy)
    {
        if (bypassPolicy.IsAllowed(httpContext.Connection.RemoteIpAddress))
        {
            clientContext.UsedIpBypass = true;
            await next(httpContext);
            return;
        }

        var apiKey = httpContext.Request.Headers[ApiKeyHeaderName].ToString();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var client = await clientResolver.ResolveAsync(apiKey, httpContext.RequestAborted);

        if (client is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        clientContext.Client = client;

        if (!HasRequiredScopes(httpContext, client.Scopes.Select(scope => scope.Name)))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(httpContext);
    }

    private static bool HasRequiredScopes(HttpContext httpContext, IEnumerable<string> clientScopes)
    {
        var requiredScopes = GetRequiredScopes(httpContext);

        if (requiredScopes.Length == 0)
        {
            return true;
        }

        var grantedScopes = clientScopes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requiredScopes.All(grantedScopes.Contains);
    }

    private static string[] GetRequiredScopes(HttpContext httpContext)
    {
        var metadata = httpContext.Features
            .Get<IReverseProxyFeature>()?
            .Route?
            .Config
            .Metadata;

        if (metadata is null ||
            !metadata.TryGetValue(DatabaseProxyConfigProvider.RequiredScopesMetadataKey, out var requiredScopes) ||
            string.IsNullOrWhiteSpace(requiredScopes))
        {
            return [];
        }

        return requiredScopes
            .Split(ScopeSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
