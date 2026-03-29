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
        base.OnModelCreating(modelBuilder);
    }
}
