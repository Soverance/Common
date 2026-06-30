namespace Soverance.Auth.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? PasswordHash { get; set; }
    public string? ApiKey { get; set; }
    public DateTimeOffset? ApiKeyCreatedAt { get; set; }
    /// <summary>Deterministic SHA-256 lookup hash of the raw API key, for O(1) indexed auth.
    /// Null for keys generated before this existed (self-healed on first authentication).</summary>
    public string? ApiKeyLookup { get; set; }
    public string? AvatarUrl { get; set; }
    public string? OAuthProvider { get; set; }
    public string? OAuthId { get; set; }
    public UserRole Role { get; set; } = UserRole.Member;
    public bool IsSystemAccount { get; set; }
    public string? DefaultServer { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
