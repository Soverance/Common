using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Soverance.Messaging.Extensions;

namespace Soverance.Messaging.Tests;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<NullableDateTimeOffsetToBinaryConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyMessagingConfigurations();
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
