using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Auth;
using MyProxy.Infrastructure.Monitoring;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Admin.ControlPlane;

public static class ControlPlaneEndpoints
{
    public static IEndpointRouteBuilder MapControlPlaneApi(
        this IEndpointRouteBuilder endpoints,
        GatewayReloadClient gatewayReloadClient)
    {
        var group = endpoints.MapGroup("/api/control")
            .WithTags("Control Plane")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        MapClientEndpoints(group);
        MapRateLimitEndpoints(group);
        MapScopeEndpoints(group);
        MapRouteEndpoints(group, gatewayReloadClient);
        MapAuditEndpoints(group);
        MapMonitoringEndpoints(group);

        return endpoints;
    }

    private static void MapClientEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/clients", async (GatewayDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var clients = await dbContext.Clients
                .AsNoTracking()
                .Include(client => client.Scopes)
                .Include(client => client.RateLimit)
                .OrderBy(client => client.Name)
                .Select(client => ClientResponse.FromEntity(client))
                .ToListAsync(cancellationToken);

            return TypedResults.Ok(clients);
        });

        group.MapGet("/clients/{id:guid}", async Task<Results<Ok<ClientResponse>, NotFound>> (
            Guid id,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var client = await dbContext.Clients
                .AsNoTracking()
                .Include(client => client.Scopes)
                .Include(client => client.RateLimit)
                .SingleOrDefaultAsync(client => client.Id == id, cancellationToken);

            return client is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ClientResponse.FromEntity(client));
        });

        group.MapPost("/clients", async Task<IResult> (
            CreateClientRequest request,
            GatewayDbContext dbContext,
            IApiKeyGenerator apiKeyGenerator,
            IApiKeyHasher apiKeyHasher,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var apiKey = apiKeyGenerator.Generate();
                var client = Client.Create(request.Name, apiKeyHasher.Hash(apiKey), request.Scopes);
                client.SetExpiration(request.ExpiresAt);
                if (request.RateLimit is not null)
                {
                    client.AssignRateLimit(request.RateLimit.ToEntity());
                }

                dbContext.Clients.Add(client);
                await dbContext.SaveChangesAsync(cancellationToken);

                return TypedResults.Created(
                    $"/api/control/clients/{client.Id}",
                    ClientCreatedResponse.FromEntity(client, apiKey));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        group.MapPost("/clients/{id:guid}/regenerate-api-key", async Task<Results<Ok<RegenerateApiKeyResponse>, NotFound>> (
            Guid id,
            GatewayDbContext dbContext,
            IApiKeyGenerator apiKeyGenerator,
            IApiKeyHasher apiKeyHasher,
            CancellationToken cancellationToken) =>
        {
            var client = await dbContext.Clients.SingleOrDefaultAsync(client => client.Id == id, cancellationToken);
            if (client is null)
            {
                return TypedResults.NotFound();
            }

            var apiKey = apiKeyGenerator.Generate();
            client.RotateApiKey(apiKeyHasher.Hash(apiKey));
            await dbContext.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(new RegenerateApiKeyResponse(apiKey));
        });

        group.MapPut("/clients/{id:guid}", async Task<IResult> (
            Guid id,
            UpdateClientRequest request,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var client = await dbContext.Clients
                .Include(client => client.Scopes)
                .Include(client => client.RateLimit)
                .SingleOrDefaultAsync(client => client.Id == id, cancellationToken);

            if (client is null)
            {
                return TypedResults.NotFound();
            }

            try
            {
                var replacedScopes = client.Scopes.ToArray();
                dbContext.Scopes.RemoveRange(replacedScopes);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();

                client = await dbContext.Clients
                    .Include(client => client.Scopes)
                    .Include(client => client.RateLimit)
                    .SingleAsync(client => client.Id == id, cancellationToken);

                client.Update(request.Name, request.IsActive, request.ExpiresAt);
                client.ReplaceScopes(request.Scopes);
                dbContext.Scopes.AddRange(client.Scopes);
                if (request.RateLimit is not null)
                {
                    if (client.RateLimit is null)
                    {
                        client.AssignRateLimit(request.RateLimit.ToEntity());
                    }
                    else
                    {
                        request.RateLimit.ApplyTo(client.RateLimit);
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                return TypedResults.Ok(ClientResponse.FromEntity(client));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        group.MapDelete("/clients/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var client = await dbContext.Clients.SingleOrDefaultAsync(client => client.Id == id, cancellationToken);
            if (client is null)
            {
                return TypedResults.NotFound();
            }

            dbContext.Clients.Remove(client);
            await dbContext.SaveChangesAsync(cancellationToken);

            return TypedResults.NoContent();
        });
    }

    private static void MapRateLimitEndpoints(RouteGroupBuilder group)
    {
        group.MapPut("/clients/{clientId:guid}/rate-limit", async Task<IResult> (
            Guid clientId,
            RateLimitRequest request,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var client = await dbContext.Clients
                .Include(client => client.RateLimit)
                .SingleOrDefaultAsync(client => client.Id == clientId, cancellationToken);

            if (client is null)
            {
                return TypedResults.NotFound();
            }

            try
            {
                if (client.RateLimit is null)
                {
                    client.AssignRateLimit(request.ToEntity());
                }
                else
                {
                    request.ApplyTo(client.RateLimit);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                return TypedResults.Ok(ClientResponse.FromEntity(client));
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return BadRequest(exception);
            }
        });

        group.MapDelete("/clients/{clientId:guid}/rate-limit", async Task<Results<NoContent, NotFound>> (
            Guid clientId,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var rateLimit = await dbContext.RateLimits.SingleOrDefaultAsync(
                rateLimit => rateLimit.ClientId == clientId,
                cancellationToken);

            if (rateLimit is null)
            {
                return TypedResults.NotFound();
            }

            dbContext.RateLimits.Remove(rateLimit);
            await dbContext.SaveChangesAsync(cancellationToken);

            return TypedResults.NoContent();
        });
    }

    private static void MapScopeEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/scopes", async (GatewayDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var scopes = await dbContext.Scopes
                .AsNoTracking()
                .OrderBy(scope => scope.Name)
                .Select(scope => ScopeResponse.FromEntity(scope))
                .ToListAsync(cancellationToken);

            return TypedResults.Ok(scopes);
        });

        group.MapPost("/scopes", async Task<IResult> (
            ScopeRequest request,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var scope = Scope.Create(request.Name);
                dbContext.Scopes.Add(scope);
                await dbContext.SaveChangesAsync(cancellationToken);

                return TypedResults.Created($"/api/control/scopes/{scope.Id}", ScopeResponse.FromEntity(scope));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        group.MapPut("/scopes/{id:guid}", async Task<IResult> (
            Guid id,
            ScopeRequest request,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var scope = await dbContext.Scopes.SingleOrDefaultAsync(scope => scope.Id == id, cancellationToken);
            if (scope is null)
            {
                return TypedResults.NotFound();
            }

            try
            {
                dbContext.Scopes.Remove(scope);
                var replacement = Scope.Create(request.Name);
                dbContext.Scopes.Add(replacement);
                await dbContext.SaveChangesAsync(cancellationToken);

                return TypedResults.Ok(ScopeResponse.FromEntity(replacement));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        group.MapDelete("/scopes/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var scope = await dbContext.Scopes.SingleOrDefaultAsync(scope => scope.Id == id, cancellationToken);
            if (scope is null)
            {
                return TypedResults.NotFound();
            }

            dbContext.Scopes.Remove(scope);
            await dbContext.SaveChangesAsync(cancellationToken);

            return TypedResults.NoContent();
        });
    }

    private static void MapRouteEndpoints(RouteGroupBuilder group, GatewayReloadClient gatewayReloadClient)
    {
        group.MapGet("/routes", async (GatewayDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var routes = await dbContext.Routes
                .AsNoTracking()
                .Include(route => route.RequiredScopes)
                .OrderBy(route => route.RouteId)
                .Select(route => RouteResponse.FromEntity(route))
                .ToListAsync(cancellationToken);

            return TypedResults.Ok(routes);
        });

        group.MapGet("/routes/{id:guid}", async Task<Results<Ok<RouteResponse>, NotFound>> (
            Guid id,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var route = await dbContext.Routes
                .AsNoTracking()
                .Include(route => route.RequiredScopes)
                .SingleOrDefaultAsync(route => route.Id == id, cancellationToken);

            return route is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(RouteResponse.FromEntity(route));
        });

        group.MapPost("/routes", async Task<IResult> (
            RouteRequest request,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var route = request.ToEntity();
                dbContext.Routes.Add(route);
                await dbContext.SaveChangesAsync(cancellationToken);
                await gatewayReloadClient.ReloadAsync(cancellationToken);

                return TypedResults.Created($"/api/control/routes/{route.Id}", RouteResponse.FromEntity(route));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        group.MapPut("/routes/{id:guid}", async Task<IResult> (
            Guid id,
            RouteRequest request,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var route = await dbContext.Routes
                .Include(route => route.RequiredScopes)
                .SingleOrDefaultAsync(route => route.Id == id, cancellationToken);

            if (route is null)
            {
                return TypedResults.NotFound();
            }

            try
            {
                var replacedScopes = route.RequiredScopes.ToArray();
                dbContext.Scopes.RemoveRange(replacedScopes);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();

                route = await dbContext.Routes
                    .Include(route => route.RequiredScopes)
                    .SingleAsync(route => route.Id == id, cancellationToken);

                request.ApplyTo(route);
                dbContext.Scopes.AddRange(route.RequiredScopes);
                await dbContext.SaveChangesAsync(cancellationToken);
                await gatewayReloadClient.ReloadAsync(cancellationToken);

                return TypedResults.Ok(RouteResponse.FromEntity(route));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        group.MapDelete("/routes/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id,
            GatewayDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var route = await dbContext.Routes.SingleOrDefaultAsync(route => route.Id == id, cancellationToken);
            if (route is null)
            {
                return TypedResults.NotFound();
            }

            dbContext.Routes.Remove(route);
            await dbContext.SaveChangesAsync(cancellationToken);
            await gatewayReloadClient.ReloadAsync(cancellationToken);

            return TypedResults.NoContent();
        });
    }

    private static void MapAuditEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/audit-entries", async (
            GatewayDbContext dbContext,
            int? take,
            CancellationToken cancellationToken) =>
        {
            var entryLimit = Math.Clamp(take ?? 100, 1, 500);
            var auditEntries = await dbContext.AuditEntries
                .AsNoTracking()
                .Include(entry => entry.Client)
                .OrderByDescending(entry => entry.Timestamp)
                .Take(entryLimit)
                .Select(entry => AuditEntryResponse.FromEntity(entry))
                .ToListAsync(cancellationToken);

            return TypedResults.Ok(auditEntries);
        });
    }

    private static void MapMonitoringEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/monitoring/summary", async (
            [FromServices] MonitoringSummaryQuery query,
            [FromQuery] int? windowMinutes,
            CancellationToken cancellationToken) =>
        {
            var summary = await query.GetSummaryAsync(windowMinutes, cancellationToken);
            return TypedResults.Ok(summary);
        });
    }

    private static BadRequest<ProblemDetails> BadRequest(Exception exception)
    {
        return TypedResults.BadRequest(new ProblemDetails
        {
            Title = "Invalid request.",
            Detail = exception.Message,
            Status = StatusCodes.Status400BadRequest,
        });
    }
}

public sealed record CreateClientRequest(
    string Name,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset? ExpiresAt,
    RateLimitRequest? RateLimit);

public sealed record UpdateClientRequest(
    string Name,
    bool IsActive,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset? ExpiresAt,
    RateLimitRequest? RateLimit);

public sealed record RateLimitRequest(int RequestLimit, int WindowSeconds)
{
    public RateLimit ToEntity()
    {
        return RateLimit.Create(RequestLimit, TimeSpan.FromSeconds(WindowSeconds));
    }

    public void ApplyTo(RateLimit rateLimit)
    {
        rateLimit.Update(RequestLimit, TimeSpan.FromSeconds(WindowSeconds));
    }
}

public sealed record ScopeRequest(string Name);

public sealed record RouteRequest(
    string RouteId,
    string ClusterId,
    string Path,
    string DestinationAddress,
    IReadOnlyCollection<string> RequiredScopes,
    bool IsEnabled)
{
    public RouteDefinition ToEntity()
    {
        var route = RouteDefinition.Create(RouteId, ClusterId, Path, DestinationAddress, RequiredScopes);
        route.Update(RouteId, ClusterId, Path, DestinationAddress, IsEnabled, RequiredScopes);
        return route;
    }

    public void ApplyTo(RouteDefinition route)
    {
        route.Update(RouteId, ClusterId, Path, DestinationAddress, IsEnabled, RequiredScopes);
    }
}

public sealed record ClientCreatedResponse(
    Guid Id,
    string Name,
    string ApiKey,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    IReadOnlyCollection<string> Scopes,
    RateLimitResponse? RateLimit)
{
    public static ClientCreatedResponse FromEntity(Client client, string apiKey)
    {
        return new ClientCreatedResponse(
            client.Id,
            client.Name,
            apiKey,
            client.IsActive,
            client.CreatedAt,
            client.ExpiresAt,
            client.Scopes.Select(scope => scope.Name).Order(StringComparer.Ordinal).ToArray(),
            client.RateLimit is null ? null : RateLimitResponse.FromEntity(client.RateLimit));
    }
}

public sealed record RegenerateApiKeyResponse(string ApiKey);

public sealed record ClientResponse(
    Guid Id,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    IReadOnlyCollection<string> Scopes,
    RateLimitResponse? RateLimit)
{
    public static ClientResponse FromEntity(Client client)
    {
        return new ClientResponse(
            client.Id,
            client.Name,
            client.IsActive,
            client.CreatedAt,
            client.ExpiresAt,
            client.Scopes.Select(scope => scope.Name).Order(StringComparer.Ordinal).ToArray(),
            client.RateLimit is null ? null : RateLimitResponse.FromEntity(client.RateLimit));
    }
}

public sealed record RateLimitResponse(Guid Id, int RequestLimit, int WindowSeconds)
{
    public static RateLimitResponse FromEntity(RateLimit rateLimit)
    {
        return new RateLimitResponse(rateLimit.Id, rateLimit.RequestLimit, (int)rateLimit.Window.TotalSeconds);
    }
}

public sealed record ScopeResponse(Guid Id, string Name)
{
    public static ScopeResponse FromEntity(Scope scope)
    {
        return new ScopeResponse(scope.Id, scope.Name);
    }
}

public sealed record RouteResponse(
    Guid Id,
    string RouteId,
    string ClusterId,
    string Path,
    string DestinationAddress,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<string> RequiredScopes)
{
    public static RouteResponse FromEntity(RouteDefinition route)
    {
        return new RouteResponse(
            route.Id,
            route.RouteId,
            route.ClusterId,
            route.Path,
            route.DestinationAddress,
            route.IsEnabled,
            route.CreatedAt,
            route.RequiredScopes.Select(scope => scope.Name).Order(StringComparer.Ordinal).ToArray());
    }
}

public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    Guid? ClientId,
    string? ClientName,
    string IpAddress,
    string Method,
    string Path,
    string? QueryString,
    int StatusCode,
    double LatencyMilliseconds)
{
    public static AuditEntryResponse FromEntity(AuditEntry auditEntry)
    {
        return new AuditEntryResponse(
            auditEntry.Id,
            auditEntry.Timestamp,
            auditEntry.ClientId,
            auditEntry.Client?.Name,
            auditEntry.IpAddress,
            auditEntry.Method,
            auditEntry.Path,
            auditEntry.QueryString,
            auditEntry.StatusCode,
            auditEntry.Latency.TotalMilliseconds);
    }
}
