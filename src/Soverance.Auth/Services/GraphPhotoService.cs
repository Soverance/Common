using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Soverance.Auth.Services;

public class GraphPhotoService : IGraphPhotoService
{
    private readonly HttpClient _http;
    private readonly string _tenantId;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly ILogger<GraphPhotoService> _logger;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;

    public GraphPhotoService(HttpClient httpClient, IConfiguration config, ILogger<GraphPhotoService> logger)
    {
        _http = httpClient;
        _tenantId = config["Authentication:AzureAd:TenantId"] ?? throw new InvalidOperationException("Authentication:AzureAd:TenantId is required");
        _clientId = config["Authentication:AzureAd:ClientId"] ?? throw new InvalidOperationException("Authentication:AzureAd:ClientId is required");
        _clientSecret = config["Authentication:AzureAd:ClientSecret"] ?? throw new InvalidOperationException("Authentication:AzureAd:ClientSecret is required");
        _logger = logger;
    }

    public async Task<(byte[] Data, string ContentType)?> GetUserPhotoAsync(string userEmail)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (token == null) return null;

            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(userEmail)}/photo/$value");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return (data, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Graph profile photo for {Email}", userEmail);
            return null;
        }
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        if (_cachedToken != null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        try
        {
            var tokenUrl = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["scope"] = "https://graph.microsoft.com/.default"
            });

            var response = await _http.PostAsync(tokenUrl, content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (result == null) return null;

            _cachedToken = result.AccessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn - 60);
            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acquire Graph API access token");
            return null;
        }
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
