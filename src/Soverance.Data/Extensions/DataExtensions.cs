using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Soverance.Data.Extensions;

public static class DataExtensions
{
    public static IServiceCollection AddSoveranceSqlServer<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringKey = "DefaultConnection")
        where TContext : SoveranceDbContextBase
    {
        services.AddDbContext<TContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString(connectionStringKey),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        // Register as DbContext so shared services (ApiKeyAuthHandler, AdminSeeder)
        // can resolve it without knowing the concrete type.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());

        return services;
    }
}
