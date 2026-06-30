using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Soverance.Auth.Auth;
using Soverance.Auth.Models;
using Soverance.Auth.Services;
using Soverance.Data;
using Xunit;

namespace Soverance.Auth.Tests.Auth;

public class ResolverTestDbContext(DbContextOptions<ResolverTestDbContext> options)
    : SoveranceDbContextBase(options);

public class ApiKeyUserResolverTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<ResolverTestDbContext> _options;

    public ApiKeyUserResolverTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _options = new DbContextOptionsBuilder<ResolverTestDbContext>().UseSqlite(_conn).Options;
        using var db = new ResolverTestDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _conn.Dispose();

    private ResolverTestDbContext NewDb() => new(_options);

    private static User MakeUser(string email) => new()
    {
        Id = Guid.NewGuid(), Email = email, Username = email.Split('@')[0],
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Resolves_migrated_user_by_lookup_hash()
    {
        const string key = "the-real-key";
        using (var db = NewDb())
        {
            var u = MakeUser("a@test.com");
            u.ApiKey = PasswordHasher.HashPassword(key);
            u.ApiKeyLookup = ApiKeyHasher.Lookup(key);
            db.Add(u);
            await db.SaveChangesAsync();
        }
        using var db2 = NewDb();
        var user = await ApiKeyUserResolver.ResolveAsync(db2, key);
        Assert.NotNull(user);
        Assert.Equal("a@test.com", user!.Email);
    }

    [Fact]
    public async Task Returns_null_for_unknown_key()
    {
        using (var db = NewDb())
        {
            var u = MakeUser("a@test.com");
            u.ApiKey = PasswordHasher.HashPassword("some-key");
            u.ApiKeyLookup = ApiKeyHasher.Lookup("some-key");
            db.Add(u);
            await db.SaveChangesAsync();
        }
        using var db2 = NewDb();
        Assert.Null(await ApiKeyUserResolver.ResolveAsync(db2, "not-a-real-key"));
    }

    [Fact]
    public async Task Resolves_legacy_bcrypt_only_user_and_backfills_lookup()
    {
        const string key = "legacy-key";
        Guid id;
        using (var db = NewDb())
        {
            var u = MakeUser("legacy@test.com");
            u.ApiKey = PasswordHasher.HashPassword(key); // ApiKeyLookup intentionally null
            db.Add(u);
            await db.SaveChangesAsync();
            id = u.Id;
        }
        using (var db2 = NewDb())
        {
            var user = await ApiKeyUserResolver.ResolveAsync(db2, key);
            Assert.NotNull(user);
            Assert.Equal("legacy@test.com", user!.Email);
        }
        using var db3 = NewDb();
        var reloaded = await db3.Set<User>().FirstAsync(u => u.Id == id);
        Assert.Equal(ApiKeyHasher.Lookup(key), reloaded.ApiKeyLookup);
    }

    [Fact]
    public async Task Resolves_migrated_user_among_many_legacy_users()
    {
        const string key = "needle-key";
        using (var db = NewDb())
        {
            for (var i = 0; i < 50; i++)
            {
                var legacy = MakeUser($"legacy{i}@test.com");
                legacy.ApiKey = PasswordHasher.HashPassword($"haystack-{i}");
                db.Add(legacy);
            }
            var target = MakeUser("needle@test.com");
            target.ApiKey = PasswordHasher.HashPassword(key);
            target.ApiKeyLookup = ApiKeyHasher.Lookup(key);
            db.Add(target);
            await db.SaveChangesAsync();
        }
        using var db2 = NewDb();
        var user = await ApiKeyUserResolver.ResolveAsync(db2, key);
        Assert.NotNull(user);
        Assert.Equal("needle@test.com", user!.Email);
    }
}
