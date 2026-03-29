using System.ComponentModel.DataAnnotations;
using Soverance.Auth.Models;

namespace Soverance.Auth.DTOs;

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class OAuthRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string RedirectUri { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class SamlExchangeRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}

public class UserProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    public DateTimeOffset? ApiKeyCreatedAt { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? OAuthProvider { get; set; }
    public string? DefaultServer { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class UpdateDefaultServerRequest
{
    public string? Server { get; set; }
}
