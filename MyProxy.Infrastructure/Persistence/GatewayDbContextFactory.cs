using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyProxy.Infrastructure.Persistence;

public sealed class GatewayDbContextFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
    public GatewayDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=myproxy_gateway;Username=postgres;Password=postgres")
            .Options;

        return new GatewayDbContext(options);
    }
}
