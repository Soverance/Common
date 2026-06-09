using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soverance.Auth.Models;
using Soverance.Auth.Services;
using Xunit;

namespace Soverance.Auth.Tests.Services;

public class UsernameBackfillTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly UsernameGenerator _generator = new();

    public UsernameBackfillTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new TestDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private User Add(string email, string username, string? provider)
    {
        var u = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            OAuthProvider = provider,
            Role = UserRole.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(u);
        return u;
    }

    [Fact]
    public async Task Backfill_RegeneratesEmailUsernames_ForOAuthUsers()
    {
        Add("john.doe@gmail.com", "john.doe@gmail.com", "google");
        await _db.SaveChangesAsync();

        await UsernameBackfill.BackfillAsync(_db, _generator, NullLogger.Instance);

        var refreshed = await _db.Users.FirstAsync();
        Assert.Equal("john.doe", refreshed.Username);
    }

    [Fact]
    public async Task Backfill_SkipsNonOAuthUsers()
    {
        Add("local@example.com", "local@example.com", null);
        await _db.SaveChangesAsync();

        await UsernameBackfill.BackfillAsync(_db, _generator, NullLogger.Instance);

        var refreshed = await _db.Users.FirstAsync();
        Assert.Equal("local@example.com", refreshed.Username); // untouched
    }

    [Fact]
    public async Task Backfill_SkipsAlreadyCleanHandles()
    {
        Add("jane@gmail.com", "jane", "google");
        await _db.SaveChangesAsync();

        await UsernameBackfill.BackfillAsync(_db, _generator, NullLogger.Instance);

        var refreshed = await _db.Users.FirstAsync();
        Assert.Equal("jane", refreshed.Username);
    }

    [Fact]
    public async Task Backfill_AvoidsCollisions_BetweenRegeneratedUsers()
    {
        Add("john@gmail.com", "john@gmail.com", "google");
        Add("john@outlook.com", "john@outlook.com", "microsoft");
        await _db.SaveChangesAsync();

        await UsernameBackfill.BackfillAsync(_db, _generator, NullLogger.Instance);

        var handles = await _db.Users.Select(u => u.Username).ToListAsync();
        Assert.Equal(2, handles.Distinct().Count()); // no duplicate handles
        Assert.Contains("john", handles);
        Assert.Contains("john2", handles);
    }

    [Fact]
    public async Task Backfill_IsIdempotent()
    {
        Add("john.doe@gmail.com", "john.doe@gmail.com", "google");
        await _db.SaveChangesAsync();

        await UsernameBackfill.BackfillAsync(_db, _generator, NullLogger.Instance);
        await UsernameBackfill.BackfillAsync(_db, _generator, NullLogger.Instance);

        var refreshed = await _db.Users.FirstAsync();
        Assert.Equal("john.doe", refreshed.Username); // not "john.doe2" on second run
    }
}
