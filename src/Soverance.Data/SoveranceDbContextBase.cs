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

        // Provider-specific filtered unique indexes.
        // SQL Server treats NULL values in unique indexes as equal, so we need
        // explicit filters to allow multiple users with NULL ApiKey or NULL OAuth
        // identifiers. PostgreSQL and SQLite both treat NULLs as distinct in
        // unique indexes by default — their default behavior matches the intent,
        // so they receive no filter.
        //
        // We detect provider via a substring match on the runtime ProviderName
        // (e.g., "Microsoft.EntityFrameworkCore.SqlServer", "Npgsql.EntityFrameworkCore.PostgreSQL")
        // to avoid taking dependencies on either provider package from the core
        // library. This is a heuristic; any provider whose name contains "SqlServer"
        // will receive the filter, which is the correct behavior for any
        // T-SQL-compatible provider.
        var provider = Database.ProviderName ?? string.Empty;
        if (provider.Contains("SqlServer", StringComparison.Ordinal))
        {
            modelBuilder.Entity<User>()
                .HasIndex(new[] { nameof(User.ApiKey) }, "IX_Users_ApiKey")
                .HasFilter("[ApiKey] IS NOT NULL");
            modelBuilder.Entity<User>()
                .HasIndex(
                    new[] { nameof(User.OAuthProvider), nameof(User.OAuthId) },
                    "IX_Users_OAuthProvider_OAuthId")
                .HasFilter("[OAuthProvider] IS NOT NULL AND [OAuthId] IS NOT NULL");
        }

        base.OnModelCreating(modelBuilder);
    }
}
