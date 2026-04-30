using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Soverance.Data.Postgres.Extensions;

public static class PostgresDataExtensions
{
    public static IServiceCollection AddSoverancePostgres<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringKey = "DefaultConnection")
        where TContext : SoveranceDbContextBase
    {
        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString(connectionStringKey),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null)));

        // Register as DbContext so shared services (ApiKeyAuthHandler, AdminSeeder)
        // can resolve it without knowing the concrete type.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());

        return services;
    }
}
