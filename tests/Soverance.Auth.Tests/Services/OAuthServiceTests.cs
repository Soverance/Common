using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Soverance.Auth.Models;
using Soverance.Auth.Services;
using Xunit;

namespace Soverance.Auth.Tests.Services;

public class OAuthServiceTests
{
    private static OAuthService BuildService(StubHttpMessageHandler handler, OAuthOptions? options = null)
    {
        options ??= new OAuthOptions
        {
            Google = new OAuthProviderOptions { ClientId = "g-id", ClientSecret = "g-secret" },
            Microsoft = new OAuthProviderOptions { ClientId = "m-id", ClientSecret = "m-secret" }
        };
        var httpClient = new HttpClient(handler);
        var factory = new SingleClientHttpClientFactory(httpClient);
        return new OAuthService(factory, Options.Create(options), NullLogger<OAuthService>.Instance);
    }

    private static OAuthService BuildServiceWithLogger(
        StubHttpMessageHandler handler,
        Microsoft.Extensions.Logging.ILogger<OAuthService> logger,
        OAuthOptions? options = null)
    {
        options ??= new OAuthOptions
        {
            Google = new OAuthProviderOptions { ClientId = "g-id", ClientSecret = "g-super-secret-value" },
            Microsoft = new OAuthProviderOptions { ClientId = "m-id", ClientSecret = "m-super-secret-value" }
        };
        var httpClient = new HttpClient(handler);
        var factory = new SingleClientHttpClientFactory(httpClient);
        return new OAuthService(factory, Microsoft.Extensions.Options.Options.Create(options), logger);
    }

    private sealed class SingleClientHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    [Fact]
    public async Task GetUserInfoAsync_Google_HappyPath_ReturnsNormalizedUserInfo()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(
            req => req.Method == HttpMethod.Post
                && req.RequestUri!.ToString() == "https://oauth2.googleapis.com/token",
            HttpStatusCode.OK,
            """{"access_token":"at-google"}""");
        handler.EnqueueJson(
            req => req.Method == HttpMethod.Get
                && req.RequestUri!.ToString() == "https://www.googleapis.com/oauth2/v2/userinfo"
                && req.Headers.Authorization!.Parameter == "at-google",
            HttpStatusCode.OK,
            """{"id":"g-12345","email":"alice@example.com","name":"Alice","picture":"https://gravatar/alice"}""");

        var sut = BuildService(handler);

        var result = await sut.GetUserInfoAsync("google", "auth-code-123", "https://app/callback");

        Assert.Equal("google", result.Provider);
        Assert.Equal("g-12345", result.ProviderId);
        Assert.Equal("alice@example.com", result.Email);
        Assert.Equal("Alice", result.Name);
        Assert.Equal("https://gravatar/alice", result.AvatarUrl);
    }

    [Fact]
    public async Task GetUserInfoAsync_Microsoft_HappyPath_ReturnsNormalizedUserInfo()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(
            req => req.Method == HttpMethod.Post
                && req.RequestUri!.ToString() == "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            HttpStatusCode.OK,
            """{"access_token":"at-microsoft"}""");
        handler.EnqueueJson(
            req => req.Method == HttpMethod.Get
                && req.RequestUri!.ToString() == "https://graph.microsoft.com/v1.0/me"
                && req.Headers.Authorization!.Parameter == "at-microsoft",
            HttpStatusCode.OK,
            """{"id":"m-67890","mail":"bob@example.com","userPrincipalName":"bob@example.com","displayName":"Bob"}""");

        var sut = BuildService(handler);

        var result = await sut.GetUserInfoAsync("microsoft", "auth-code-456", "https://app/callback");

        Assert.Equal("microsoft", result.Provider);
        Assert.Equal("m-67890", result.ProviderId);
        Assert.Equal("bob@example.com", result.Email);
        Assert.Equal("Bob", result.Name);
        Assert.Null(result.AvatarUrl);
    }

    [Fact]
    public async Task GetUserInfoAsync_Microsoft_TokenRequestIncludesScope()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(
            req => req.Method == HttpMethod.Post
                && req.RequestUri!.ToString() == "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            HttpStatusCode.OK,
            """{"access_token":"at-m"}""");
        handler.EnqueueJson(
            req => req.Method == HttpMethod.Get,
            HttpStatusCode.OK,
            """{"id":"m-1","mail":"x@y.com","userPrincipalName":"x@y.com","displayName":"X"}""");

        var sut = BuildService(handler);
        await sut.GetUserInfoAsync("microsoft", "code", "https://callback");

        var tokenRequest = handler.Received[0];
        var body = await tokenRequest.Content!.ReadAsStringAsync();
        Assert.Contains("scope=openid+email+profile+User.Read", body);
    }

    [Fact]
    public async Task GetUserInfoAsync_Google_TokenExchange400_ThrowsOAuthProviderException()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueRaw(
            req => req.RequestUri!.ToString() == "https://oauth2.googleapis.com/token",
            HttpStatusCode.BadRequest,
            """{"error":"invalid_grant"}""");

        var sut = BuildService(handler);

        var ex = await Assert.ThrowsAsync<Soverance.Auth.Exceptions.OAuthProviderException>(
            () => sut.GetUserInfoAsync("google", "bad-code", "https://callback"));

        Assert.Equal("google", ex.Provider);
        Assert.Equal(400, ex.UpstreamStatusCode);
        Assert.Contains("invalid_grant", ex.UpstreamBody);
    }

    [Fact]
    public async Task GetUserInfoAsync_Google_MalformedTokenJson_ThrowsOAuthProviderException()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueRaw(
            req => true,
            HttpStatusCode.OK,
            "this is not json");

        var sut = BuildService(handler);

        await Assert.ThrowsAsync<Soverance.Auth.Exceptions.OAuthProviderException>(
            () => sut.GetUserInfoAsync("google", "code", "https://callback"));
    }

    [Fact]
    public async Task GetUserInfoAsync_Google_Userinfo401_ThrowsOAuthProviderException()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(req => req.Method == HttpMethod.Post, HttpStatusCode.OK, """{"access_token":"at"}""");
        handler.EnqueueRaw(req => req.Method == HttpMethod.Get, HttpStatusCode.Unauthorized, "{}");

        var sut = BuildService(handler);

        var ex = await Assert.ThrowsAsync<Soverance.Auth.Exceptions.OAuthProviderException>(
            () => sut.GetUserInfoAsync("google", "code", "https://callback"));
        Assert.Equal(401, ex.UpstreamStatusCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_Google_NameMissing_FallsBackToEmailPrefix()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(req => req.Method == HttpMethod.Post, HttpStatusCode.OK, """{"access_token":"at"}""");
        handler.EnqueueJson(req => req.Method == HttpMethod.Get, HttpStatusCode.OK,
            """{"id":"g1","email":"carol@example.com","name":null,"picture":null}""");

        var sut = BuildService(handler);
        var result = await sut.GetUserInfoAsync("google", "code", "https://callback");

        Assert.Equal("carol", result.Name);
    }

    [Fact]
    public async Task GetUserInfoAsync_Microsoft_MailNullUsesUserPrincipalName()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(req => req.Method == HttpMethod.Post, HttpStatusCode.OK, """{"access_token":"at"}""");
        handler.EnqueueJson(req => req.Method == HttpMethod.Get, HttpStatusCode.OK,
            """{"id":"m1","mail":null,"userPrincipalName":"dan@contoso.com","displayName":"Dan"}""");

        var sut = BuildService(handler);
        var result = await sut.GetUserInfoAsync("microsoft", "code", "https://callback");

        Assert.Equal("dan@contoso.com", result.Email);
    }

    [Fact]
    public async Task GetUserInfoAsync_Microsoft_DisplayNameNullFallsBackToEmailPrefix()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(req => req.Method == HttpMethod.Post, HttpStatusCode.OK, """{"access_token":"at"}""");
        handler.EnqueueJson(req => req.Method == HttpMethod.Get, HttpStatusCode.OK,
            """{"id":"m2","mail":null,"userPrincipalName":"eve@contoso.com","displayName":null}""");

        var sut = BuildService(handler);
        var result = await sut.GetUserInfoAsync("microsoft", "code", "https://callback");

        Assert.Equal("eve", result.Name);
    }

    [Fact]
    public async Task GetUserInfoAsync_NoEmail_ThrowsOAuthProviderException()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(req => req.Method == HttpMethod.Post, HttpStatusCode.OK, """{"access_token":"at"}""");
        handler.EnqueueJson(req => req.Method == HttpMethod.Get, HttpStatusCode.OK,
            """{"id":"g","email":null,"name":"X","picture":null}""");

        var sut = BuildService(handler);
        var ex = await Assert.ThrowsAsync<Soverance.Auth.Exceptions.OAuthProviderException>(
            () => sut.GetUserInfoAsync("google", "code", "https://callback"));
        Assert.Equal("OAuth provider returned no email address", ex.Message);
    }

    [Fact]
    public async Task GetUserInfoAsync_UnsupportedProvider_ThrowsUnsupportedOAuthProviderException()
    {
        var handler = new StubHttpMessageHandler();
        var sut = BuildService(handler);

        var ex = await Assert.ThrowsAsync<Soverance.Auth.Exceptions.UnsupportedOAuthProviderException>(
            () => sut.GetUserInfoAsync("github", "code", "https://callback"));
        Assert.Equal("github", ex.Provider);
    }

    [Fact]
    public async Task GetUserInfoAsync_CancellationToken_PropagatesOperationCanceled()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(req => true, HttpStatusCode.OK, """{"access_token":"at"}""");
        handler.EnqueueJson(req => true, HttpStatusCode.OK,
            """{"id":"g","email":"a@b.com","name":"A","picture":null}""");

        var sut = BuildService(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.GetUserInfoAsync("google", "code", "https://callback", cts.Token));
    }

    [Fact]
    public async Task GetUserInfoAsync_NoSecretsAppearInLogs()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(req => req.Method == HttpMethod.Post, HttpStatusCode.OK,
            """{"access_token":"super-secret-access-token-do-not-log"}""");
        handler.EnqueueJson(req => req.Method == HttpMethod.Get, HttpStatusCode.OK,
            """{"id":"g","email":"a@b.com","name":"A","picture":null}""");

        var capturingLogger = new CapturingLogger<OAuthService>();
        var sut = BuildServiceWithLogger(handler, capturingLogger);

        await sut.GetUserInfoAsync("google", "auth-code-do-not-log", "https://callback");

        var allLogs = string.Join("\n", capturingLogger.Messages);
        Assert.DoesNotContain("g-super-secret-value", allLogs);   // client_secret
        Assert.DoesNotContain("auth-code-do-not-log", allLogs);   // OAuth code
        Assert.DoesNotContain("super-secret-access-token-do-not-log", allLogs); // access token
    }
}

internal sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public List<string> Messages { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        Microsoft.Extensions.Logging.EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }
}
