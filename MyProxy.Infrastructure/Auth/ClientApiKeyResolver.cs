using Microsoft.EntityFrameworkCore;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Persistence;

namespace MyProxy.Infrastructure.Auth;

public interface IClientApiKeyResolver
{
    Task<Client?> ResolveAsync(string apiKey, CancellationToken cancellationToken);
}

public sealed class DatabaseClientApiKeyResolver(
    IDbContextFactory<GatewayDbContext> dbContextFactory,
    IApiKeyHasher apiKeyHasher) : IClientApiKeyResolver
{
    public async Task<Client?> ResolveAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var apiKeyHash = apiKeyHasher.Hash(apiKey);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var client = await dbContext.Clients
            .AsNoTracking()
            .Include(entity => entity.Scopes)
            .Include(entity => entity.RateLimit)
            .SingleOrDefaultAsync(entity => entity.ApiKeyHash == apiKeyHash, cancellationToken);

        if (client is null || !client.IsActive || client.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return client;
    }
}
