using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soverance.Auth.DTOs;
using Soverance.Auth.Exceptions;
using Soverance.Auth.Models;
using Soverance.Auth.Services;
using Xunit;

namespace Soverance.Auth.Tests.Services;

public class OAuthAccountLinkerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly OAuthAccountLinker _sut;

    public OAuthAccountLinkerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new TestDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _sut = new OAuthAccountLinker(NullLogger<OAuthAccountLinker>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static OAuthUserInfo Info(string provider, string providerId, string email, string name = "X", string? avatar = null)
        => new(provider, providerId, email, name, avatar);

    [Fact]
    public async Task LinkOrCreate_Path1_MatchByProviderAndId_ReturnsExistingUser()
    {
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            Username = "alice@example.com",
            OAuthProvider = "google",
            OAuthId = "g-1",
            Role = UserRole.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(existing);
        await _db.SaveChangesAsync();

        var result = await _sut.LinkOrCreateAsync(
            Info("google", "g-1", "alice@example.com", "Alice", "https://avatar/alice"),
            _db);

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(1, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task LinkOrCreate_Path1_RefreshesAvatarAndDisplayNameIfProviderReturnsNonNull()
    {
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            Username = "alice@example.com",
            DisplayName = "OldName",
            AvatarUrl = "https://old/avatar",
            OAuthProvider = "google",
            OAuthId = "g-1",
            Role = UserRole.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(existing);
        await _db.SaveChangesAsync();

        await _sut.LinkOrCreateAsync(
            Info("google", "g-1", "alice@example.com", "NewName", "https://new/avatar"),
            _db);

        var refreshed = await _db.Users.FirstAsync();
        Assert.Equal("NewName", refreshed.DisplayName);
        Assert.Equal("https://new/avatar", refreshed.AvatarUrl);
    }

    [Fact]
    public async Task LinkOrCreate_Path1_PreservesAvatarIfProviderReturnsNull()
    {
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            Username = "alice@example.com",
            DisplayName = "OldName",
            AvatarUrl = "https://old/avatar",
            OAuthProvider = "google",
            OAuthId = "g-1",
            Role = UserRole.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(existing);
        await _db.SaveChangesAsync();

        await _sut.LinkOrCreateAsync(
            Info("google", "g-1", "alice@example.com", "X", null),
            _db);

        var refreshed = await _db.Users.FirstAsync();
        Assert.Equal("https://old/avatar", refreshed.AvatarUrl);
    }

    [Fact]
    public async Task LinkOrCreate_Path2_LocalAccountByEmail_LinksOAuthIdentity()
    {
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "bob@example.com",
            Username = "bob@example.com",
            PasswordHash = "bcrypt-hash",
            OAuthProvider = null,
            OAuthId = null,
            Role = UserRole.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(existing);
        await _db.SaveChangesAsync();

        var result = await _sut.LinkOrCreateAsync(
            Info("google", "g-bob", "bob@example.com", "Bob", "https://avatar/bob"),
            _db);

        Assert.Equal(existing.Id, result.Id);
        var refreshed = await _db.Users.FirstAsync();
        Assert.Equal("google", refreshed.OAuthProvider);
        Assert.Equal("g-bob", refreshed.OAuthId);
        Assert.Equal("https://avatar/bob", refreshed.AvatarUrl);
    }

    [Fact]
    public async Task LinkOrCreate_Path3_EmailLinkedToDifferentProvider_ThrowsConflict()
    {
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "carol@example.com",
            Username = "carol@example.com",
            OAuthProvider = "google",
            OAuthId = "g-carol",
            Role = UserRole.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(existing);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<OAuthAccountConflictException>(
            () => _sut.LinkOrCreateAsync(
                Info("microsoft", "m-carol", "carol@example.com"), _db));

        Assert.Equal("carol@example.com", ex.Email);
    }

    [Fact]
    public async Task LinkOrCreate_Path3_SameProviderDifferentProviderId_ThrowsConflict()
    {
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "dan@example.com",
            Username = "dan@example.com",
            OAuthProvider = "google",
            OAuthId = "g-dan-original",
            Role = UserRole.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(existing);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<OAuthAccountConflictException>(
            () => _sut.LinkOrCreateAsync(
                Info("google", "g-dan-different", "dan@example.com"), _db));
    }

    [Fact]
    public async Task LinkOrCreate_Path4_NoMatch_CreatesNewUserWithMemberRole()
    {
        Assert.Equal(0, await _db.Users.CountAsync());

        var result = await _sut.LinkOrCreateAsync(
            Info("google", "g-eve", "eve@example.com", "Eve", "https://avatar/eve"),
            _db);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("eve@example.com", result.Email);
        Assert.Equal("eve@example.com", result.Username);
        Assert.Equal("Eve", result.DisplayName);
        Assert.Equal("https://avatar/eve", result.AvatarUrl);
        Assert.Equal("google", result.OAuthProvider);
        Assert.Equal("g-eve", result.OAuthId);
        Assert.Equal(UserRole.Member, result.Role);
        Assert.Equal(1, await _db.Users.CountAsync());
    }
}
