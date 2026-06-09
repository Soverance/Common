using Microsoft.EntityFrameworkCore;
using Soverance.Auth.Models;
using Soverance.Auth.Services;
using Xunit;

namespace Soverance.Auth.Tests.Services;

public class UsernameGeneratorTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly UsernameGenerator _sut = new();

    public UsernameGeneratorTests()
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

    private async Task SeedUsernameAsync(string username)
    {
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = $"{username}@seed.test",
            Username = username,
            Role = UserRole.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Generate_UsesEmailLocalPart_Lowercased()
    {
        var handle = await _sut.GenerateAsync("John.Doe@gmail.com", _db);
        Assert.Equal("john.doe", handle);
    }

    [Fact]
    public async Task Generate_StripsDisallowedCharacters()
    {
        var handle = await _sut.GenerateAsync("a+b!c#d@x.com", _db);
        Assert.Equal("abcd", handle);
    }

    [Fact]
    public async Task Generate_AppendsSuffixOnCollision()
    {
        await SeedUsernameAsync("john.doe");
        var handle = await _sut.GenerateAsync("john.doe@gmail.com", _db);
        Assert.Equal("john.doe2", handle);
    }

    [Fact]
    public async Task Generate_IncrementsSuffixUntilFree()
    {
        await SeedUsernameAsync("john.doe");
        await SeedUsernameAsync("john.doe2");
        var handle = await _sut.GenerateAsync("john.doe@gmail.com", _db);
        Assert.Equal("john.doe3", handle);
    }

    [Fact]
    public async Task Generate_FallsBackToUser_WhenNothingSurvives()
    {
        var handle = await _sut.GenerateAsync("!!!@x.com", _db);
        Assert.Equal("user", handle);
    }

    [Fact]
    public async Task Generate_PadsShortHandleToMinimumLength()
    {
        var handle = await _sut.GenerateAsync("jo@x.com", _db);
        Assert.Equal("jo0", handle);
    }

    [Fact]
    public async Task Generate_TruncatesLongLocalPart_NoTrailingSeparator()
    {
        var longLocal = new string('a', 70);
        var handle = await _sut.GenerateAsync($"{longLocal}@x.com", _db);
        Assert.Equal(58, handle.Length);
        Assert.Matches("^[a-z0-9._-]+$", handle);
        Assert.DoesNotMatch("[._-]$", handle); // no trailing separator after truncation
    }
}
