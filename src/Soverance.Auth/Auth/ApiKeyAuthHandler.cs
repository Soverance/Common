using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Soverance.Auth.Models;

namespace Soverance.Auth.Auth;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
            return AuthenticateResult.NoResult();

        var apiKey = apiKeyHeader.ToString();
        if (string.IsNullOrEmpty(apiKey))
            return AuthenticateResult.NoResult();

        // Resolve the app's registered DbContext from the request scope.
        // Works because both apps register their derived SoveranceDbContextBase via AddSoveranceSqlServer<T>(),
        // which also registers it as DbContext.
        var db = Context.RequestServices.GetRequiredService<DbContext>();

        var usersWithKeys = await db.Set<User>()
            .Where(u => u.ApiKey != null)
            .ToListAsync();

        var user = usersWithKeys.FirstOrDefault(u =>
            BCrypt.Net.BCrypt.Verify(apiKey, u.ApiKey));
        if (user is null)
            return AuthenticateResult.Fail("Invalid API key");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
