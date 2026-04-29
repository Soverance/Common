using Soverance.Auth.DTOs;

namespace Soverance.Auth.Services;

public interface IOAuthService
{
    /// <summary>
    /// Exchanges the OAuth authorization code for an access token, then fetches
    /// the userinfo from the provider, and returns a normalized OAuthUserInfo.
    /// </summary>
    /// <param name="provider">"google" or "microsoft" (case-insensitive).</param>
    /// <param name="code">The OAuth authorization code from the SPA's redirect.</param>
    /// <param name="redirectUri">Must match the redirect_uri the SPA used in step 1.</param>
    /// <exception cref="Soverance.Auth.Exceptions.UnsupportedOAuthProviderException">Unknown provider name.</exception>
    /// <exception cref="Soverance.Auth.Exceptions.OAuthProviderException">Token exchange or userinfo fetch failed.</exception>
    Task<OAuthUserInfo> GetUserInfoAsync(
        string provider,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default);
}
