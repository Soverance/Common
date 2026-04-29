using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Soverance.Auth.Models;

namespace Soverance.Auth.Tests;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite does not support DateTimeOffset natively in ORDER BY or aggregates.
        // Store as ticks (long) so all comparisons and ordering work correctly.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<NullableDateTimeOffsetToBinaryConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Ignore(u => u.RefreshTokens);
        });
        base.OnModelCreating(modelBuilder);
    }

    private sealed class DateTimeOffsetToBinaryConverter()
        : ValueConverter<DateTimeOffset, long>(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero));

    private sealed class NullableDateTimeOffsetToBinaryConverter()
        : ValueConverter<DateTimeOffset?, long?>(
            v => v == null ? null : v.Value.UtcTicks,
            v => v == null ? null : new DateTimeOffset(v.Value, TimeSpan.Zero));
}
