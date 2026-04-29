using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Soverance.Auth.DTOs;
using Soverance.Auth.Exceptions;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public sealed class OAuthService : IOAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OAuthOptions _options;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        IHttpClientFactory httpClientFactory,
        IOptions<OAuthOptions> options,
        ILogger<OAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<OAuthUserInfo> GetUserInfoAsync(
        string provider,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
        => provider.ToLowerInvariant() switch
        {
            "google" => GetGoogleAsync(code, redirectUri, cancellationToken),
            "microsoft" => GetMicrosoftAsync(code, redirectUri, cancellationToken),
            _ => throw new UnsupportedOAuthProviderException(provider)
        };

    private async Task<OAuthUserInfo> GetGoogleAsync(string code, string redirectUri, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        _logger.LogDebug("OAuth token exchange request {Provider} redirect {RedirectUri}", "google", redirectUri);

        var tokenResponse = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.Google.ClientId,
                ["client_secret"] = _options.Google.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }),
            ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var body = await tokenResponse.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("OAuth provider exchange failed {Provider} {Status} {Body}", "google", (int)tokenResponse.StatusCode, body);
            throw new OAuthProviderException("google", "Failed to authenticate with OAuth provider", (int)tokenResponse.StatusCode, body);
        }

        OAuthTokenResponse? tokenData;
        try
        {
            tokenData = await JsonSerializer.DeserializeAsync<OAuthTokenResponse>(
                await tokenResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OAuth token response malformed JSON {Provider}", "google");
            throw new OAuthProviderException("google", "Failed to authenticate with OAuth provider", inner: ex);
        }

        if (tokenData is null || string.IsNullOrEmpty(tokenData.AccessToken))
            throw new OAuthProviderException("google", "Failed to authenticate with OAuth provider");

        _logger.LogDebug("OAuth userinfo request {Provider}", "google");

        var infoRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
        infoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
        var infoResponse = await client.SendAsync(infoRequest, ct);

        if (!infoResponse.IsSuccessStatusCode)
        {
            var body = await infoResponse.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("OAuth provider exchange failed {Provider} {Status} {Body}", "google", (int)infoResponse.StatusCode, body);
            throw new OAuthProviderException("google", "Failed to authenticate with OAuth provider", (int)infoResponse.StatusCode, body);
        }

        GoogleUserInfo? user;
        try
        {
            user = await JsonSerializer.DeserializeAsync<GoogleUserInfo>(
                await infoResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OAuth userinfo malformed JSON {Provider}", "google");
            throw new OAuthProviderException("google", "Failed to authenticate with OAuth provider", inner: ex);
        }

        if (user is null || string.IsNullOrEmpty(user.Email))
            throw new OAuthProviderException("google", "OAuth provider returned no email address");

        _logger.LogInformation("OAuth userinfo received {Provider} {ProviderId}", "google", user.Id);

        return new OAuthUserInfo(
            Provider: "google",
            ProviderId: user.Id,
            Email: user.Email,
            Name: user.Name ?? user.Email.Split('@')[0],
            AvatarUrl: user.Picture);
    }

    private async Task<OAuthUserInfo> GetMicrosoftAsync(string code, string redirectUri, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        _logger.LogDebug("OAuth token exchange request {Provider} redirect {RedirectUri}", "microsoft", redirectUri);

        var tokenResponse = await client.PostAsync(
            "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.Microsoft.ClientId,
                ["client_secret"] = _options.Microsoft.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["scope"] = "openid email profile User.Read"
            }),
            ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var body = await tokenResponse.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("OAuth provider exchange failed {Provider} {Status} {Body}", "microsoft", (int)tokenResponse.StatusCode, body);
            throw new OAuthProviderException("microsoft", "Failed to authenticate with OAuth provider", (int)tokenResponse.StatusCode, body);
        }

        OAuthTokenResponse? tokenData;
        try
        {
            tokenData = await JsonSerializer.DeserializeAsync<OAuthTokenResponse>(
                await tokenResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OAuth token response malformed JSON {Provider}", "microsoft");
            throw new OAuthProviderException("microsoft", "Failed to authenticate with OAuth provider", inner: ex);
        }

        if (tokenData is null || string.IsNullOrEmpty(tokenData.AccessToken))
            throw new OAuthProviderException("microsoft", "Failed to authenticate with OAuth provider");

        _logger.LogDebug("OAuth userinfo request {Provider}", "microsoft");

        var infoRequest = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
        infoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
        var infoResponse = await client.SendAsync(infoRequest, ct);

        if (!infoResponse.IsSuccessStatusCode)
        {
            var body = await infoResponse.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("OAuth provider exchange failed {Provider} {Status} {Body}", "microsoft", (int)infoResponse.StatusCode, body);
            throw new OAuthProviderException("microsoft", "Failed to authenticate with OAuth provider", (int)infoResponse.StatusCode, body);
        }

        MicrosoftUserInfo? user;
        try
        {
            user = await JsonSerializer.DeserializeAsync<MicrosoftUserInfo>(
                await infoResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OAuth userinfo malformed JSON {Provider}", "microsoft");
            throw new OAuthProviderException("microsoft", "Failed to authenticate with OAuth provider", inner: ex);
        }

        if (user is null) throw new OAuthProviderException("microsoft", "Failed to authenticate with OAuth provider");

        var email = user.Mail ?? user.UserPrincipalName;
        if (string.IsNullOrEmpty(email))
            throw new OAuthProviderException("microsoft", "OAuth provider returned no email address");

        var name = user.DisplayName ?? email.Split('@')[0];

        _logger.LogInformation("OAuth userinfo received {Provider} {ProviderId}", "microsoft", user.Id);

        return new OAuthUserInfo(
            Provider: "microsoft",
            ProviderId: user.Id,
            Email: email,
            Name: name,
            AvatarUrl: null);
    }
}
