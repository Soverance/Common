using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Soverance.Auth.Models;
using Soverance.Data;
using Soverance.Data.Postgres.Extensions;
using Xunit;

namespace Soverance.Data.Postgres.Tests;

public class AddSoverancePostgresTests
{
    private sealed class FakeContext : SoveranceDbContextBase
    {
        public FakeContext(DbContextOptions<FakeContext> options) : base(options) { }
    }

    [Fact]
    public void AddSoverancePostgres_RegistersDbContextAndDbContextAlias()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake;Username=u;Password=p"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSoverancePostgres<FakeContext>(configuration);

        using var provider = services.BuildServiceProvider();

        // Concrete context resolves
        var concrete = provider.GetService<FakeContext>();
        Assert.NotNull(concrete);

        // Alias DbContext resolves and points to the same registration
        using var scope = provider.CreateScope();
        var alias = scope.ServiceProvider.GetService<DbContext>();
        Assert.NotNull(alias);
        Assert.IsType<FakeContext>(alias);
    }
}
