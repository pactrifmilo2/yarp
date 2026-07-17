using Microsoft.EntityFrameworkCore;
using MyProxy.Domain.Entities;
using MyProxy.Infrastructure.Auditing;

namespace MyProxy.Infrastructure.Persistence;

public sealed class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Scope> Scopes => Set<Scope>();

    public DbSet<RateLimit> RateLimits => Set<RateLimit>();

    public DbSet<RouteDefinition> Routes => Set<RouteDefinition>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureClients(modelBuilder);
        ConfigureScopes(modelBuilder);
        ConfigureRateLimits(modelBuilder);
        ConfigureRoutes(modelBuilder);
        ConfigureAuditEntries(modelBuilder);
    }

    private static void ConfigureClients(ModelBuilder modelBuilder)
    {
        var client = modelBuilder.Entity<Client>();

        client.ToTable("clients");
        client.HasKey(entity => entity.Id);
        client.Property(entity => entity.Id).HasColumnName("id");
        client.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        client.Property(entity => entity.ApiKeyHash).HasColumnName("api_key_hash").HasMaxLength(256).IsRequired();
        client.Property(entity => entity.IsActive).HasColumnName("is_active").IsRequired();
        client.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        client.Property(entity => entity.ExpiresAt).HasColumnName("expires_at");
        client.HasIndex(entity => entity.ApiKeyHash).IsUnique();
        client.Navigation(entity => entity.Scopes).UsePropertyAccessMode(PropertyAccessMode.Field);

        client.HasMany(entity => entity.Scopes)
            .WithOne(scope => scope.Client)
            .HasForeignKey(scope => scope.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        client.HasOne(entity => entity.RateLimit)
            .WithOne(rateLimit => rateLimit.Client)
            .HasForeignKey<RateLimit>(rateLimit => rateLimit.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureScopes(ModelBuilder modelBuilder)
    {
        var scope = modelBuilder.Entity<Scope>();

        scope.ToTable("scopes");
        scope.HasKey(entity => entity.Id);
        scope.Property(entity => entity.Id).HasColumnName("id");
        scope.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        scope.Property(entity => entity.ClientId).HasColumnName("client_id");
        scope.Property(entity => entity.RouteDefinitionId).HasColumnName("route_definition_id");
        scope.HasIndex(entity => new { entity.ClientId, entity.Name });
        scope.HasIndex(entity => new { entity.RouteDefinitionId, entity.Name });
    }

    private static void ConfigureRateLimits(ModelBuilder modelBuilder)
    {
        var rateLimit = modelBuilder.Entity<RateLimit>();

        rateLimit.ToTable("rate_limits");
        rateLimit.HasKey(entity => entity.Id);
        rateLimit.Property(entity => entity.Id).HasColumnName("id");
        rateLimit.Property(entity => entity.ClientId).HasColumnName("client_id");
        rateLimit.Property(entity => entity.RequestLimit).HasColumnName("request_limit").IsRequired();
        rateLimit.Property(entity => entity.Window).HasColumnName("window").IsRequired();
        rateLimit.HasIndex(entity => entity.ClientId).IsUnique();
    }

    private static void ConfigureRoutes(ModelBuilder modelBuilder)
    {
        var route = modelBuilder.Entity<RouteDefinition>();

        route.ToTable("routes");
        route.HasKey(entity => entity.Id);
        route.Property(entity => entity.Id).HasColumnName("id");
        route.Property(entity => entity.RouteId).HasColumnName("route_id").HasMaxLength(200).IsRequired();
        route.Property(entity => entity.ClusterId).HasColumnName("cluster_id").HasMaxLength(200).IsRequired();
        route.Property(entity => entity.Path).HasColumnName("path").HasMaxLength(500).IsRequired();
        route.Property(entity => entity.DestinationAddress).HasColumnName("destination_address").HasMaxLength(2048).IsRequired();
        route.Property(entity => entity.IsEnabled).HasColumnName("is_enabled").IsRequired();
        route.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        route.HasIndex(entity => entity.RouteId).IsUnique();
        route.Navigation(entity => entity.RequiredScopes).UsePropertyAccessMode(PropertyAccessMode.Field);

        route.HasMany(entity => entity.RequiredScopes)
            .WithOne(scope => scope.RouteDefinition)
            .HasForeignKey(scope => scope.RouteDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureAuditEntries(ModelBuilder modelBuilder)
    {
        var auditEntry = modelBuilder.Entity<AuditEntry>();

        auditEntry.ToTable("audit_entries");
        auditEntry.HasKey(entity => entity.Id);
        auditEntry.Property(entity => entity.Id).HasColumnName("id");
        auditEntry.Property(entity => entity.Timestamp).HasColumnName("timestamp").IsRequired();
        auditEntry.Property(entity => entity.ClientId).HasColumnName("client_id");
        auditEntry.Property(entity => entity.IpAddress).HasColumnName("ip_address").HasMaxLength(128).IsRequired();
        auditEntry.Property(entity => entity.Method).HasColumnName("method").HasMaxLength(16).IsRequired();
        auditEntry.Property(entity => entity.Path).HasColumnName("path").HasMaxLength(2048).IsRequired();
        auditEntry.Property(entity => entity.QueryString)
            .HasColumnName("query_string")
            .HasMaxLength(AuditQueryStringSanitizer.MaxStoredLength);
        auditEntry.Property(entity => entity.StatusCode).HasColumnName("status_code").IsRequired();
        auditEntry.Property(entity => entity.Latency).HasColumnName("latency").IsRequired();
        auditEntry.HasIndex(entity => entity.Timestamp);

        auditEntry.HasOne(entity => entity.Client)
            .WithMany()
            .HasForeignKey(entity => entity.ClientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
