using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyProxy.Infrastructure.Persistence;
using MyProxy.Infrastructure.Proxy;
using Yarp.ReverseProxy.Configuration;

namespace MyProxy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGatewayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GatewayDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'GatewayDatabase' is required.");
        }

        services.AddDbContextFactory<GatewayDbContext>(options =>
            ConfigureGatewayDbContext(options, connectionString));
        services.AddScoped(provider =>
            provider.GetRequiredService<IDbContextFactory<GatewayDbContext>>().CreateDbContext());
        services.AddSingleton<DatabaseProxyConfigProvider>();
        services.AddSingleton<IProxyConfigProvider>(provider =>
            provider.GetRequiredService<DatabaseProxyConfigProvider>());

        return services;
    }

    private static void ConfigureGatewayDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseNpgsql(connectionString);
    }
}
