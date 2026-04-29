using System.Text.Json.Serialization;

namespace Soverance.Auth.DTOs;

/// <summary>
/// Normalized user info returned by an OAuth provider after code exchange.
/// </summary>
public sealed record OAuthUserInfo(
    string Provider,
    string ProviderId,
    string Email,
    string Name,
    string? AvatarUrl);

/// <summary>OAuth token-exchange response from any provider.</summary>
internal sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken);

/// <summary>Userinfo response shape from Google's /oauth2/v2/userinfo.</summary>
internal sealed record GoogleUserInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("picture")] string? Picture);

/// <summary>Userinfo response shape from Microsoft Graph /v1.0/me.</summary>
internal sealed record MicrosoftUserInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("mail")] string? Mail,
    [property: JsonPropertyName("userPrincipalName")] string? UserPrincipalName,
    [property: JsonPropertyName("displayName")] string? DisplayName);
