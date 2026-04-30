using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Soverance.Auth.Models;

namespace Soverance.Data;

public abstract class SoveranceDbContextBase : DbContext, IDataProtectionKeyContext
{
    protected SoveranceDbContextBase(DbContextOptions options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SamlConfig> SamlConfigs => Set<SamlConfig>();
    public DbSet<SamlRoleMapping> SamlRoleMappings => Set<SamlRoleMapping>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SoveranceDbContextBase).Assembly);

        // ApiKey and OAuth unique indexes are declared here (not in
        // UserConfiguration) so that SQL-Server-specific filters can be
        // attached conditionally without EF Core treating them as separate
        // indexes. EF Core does NOT merge HasIndex declarations across
        // IEntityTypeConfiguration and OnModelCreating even when the
        // property expression is identical — declaring the index in both
        // places produces a duplicate. Single declaration here keeps the
        // model snapshot clean for all providers.
        var apiKeyIndex = modelBuilder.Entity<User>()
            .HasIndex(u => u.ApiKey)
            .IsUnique();
        var oauthIndex = modelBuilder.Entity<User>()
            .HasIndex(u => new { u.OAuthProvider, u.OAuthId })
            .IsUnique();

        // SQL Server treats NULL values in unique indexes as equal, so we need
        // explicit filters to allow multiple users with NULL ApiKey or NULL OAuth
        // identifiers. PostgreSQL and SQLite treat NULLs as distinct in unique
        // indexes by default — their default behavior matches the intent, so
        // they receive no filter.
        //
        // We detect the provider via a substring match on the runtime
        // ProviderName (e.g., "Microsoft.EntityFrameworkCore.SqlServer",
        // "Npgsql.EntityFrameworkCore.PostgreSQL") to avoid taking dependencies
        // on either provider package from this core library. This is a
        // heuristic; any provider whose name contains "SqlServer" will receive
        // the filter, which is the correct behavior for any T-SQL-compatible
        // provider.
        var provider = Database.ProviderName ?? string.Empty;
        if (provider.Contains("SqlServer", StringComparison.Ordinal))
        {
            apiKeyIndex.HasFilter("[ApiKey] IS NOT NULL");
            oauthIndex.HasFilter("[OAuthProvider] IS NOT NULL AND [OAuthId] IS NOT NULL");
        }

        base.OnModelCreating(modelBuilder);
    }
}
