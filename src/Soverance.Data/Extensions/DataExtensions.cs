using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Soverance.Data.Extensions;

public static class DataExtensions
{
    /// <summary>
    /// Persists ASP.NET Core Data Protection keys to the database so they survive
    /// container restarts and redeployments. Provider-agnostic (works against any
    /// SoveranceDbContextBase regardless of underlying database). Call after the
    /// provider-specific registration extension (AddSoveranceSqlServer or
    /// AddSoverancePostgres).
    /// </summary>
    public static IServiceCollection AddSoveranceDataProtection<TContext>(
        this IServiceCollection services,
        string applicationName)
        where TContext : SoveranceDbContextBase
    {
        services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToDbContext<TContext>();

        return services;
    }
}
