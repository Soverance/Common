using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public class JwtSamlSignInHandler : ISamlSignInHandler, IDisposable
{
    private readonly ConcurrentDictionary<string, (Guid UserId, DateTimeOffset Expiry)> _codes = new();
    private readonly Timer _cleanupTimer;

    public JwtSamlSignInHandler()
    {
        _cleanupTimer = new Timer(_ => PurgeExpired(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public string ErrorRedirectBase => "/";

    public Task<IResult> HandleSignInAsync(HttpContext httpContext, User user)
    {
        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        _codes[code] = (user.Id, DateTimeOffset.UtcNow.AddMinutes(2));

        return Task.FromResult(Results.Redirect($"/?saml_code={code}"));
    }

    public Guid? RedeemCode(string code)
    {
        if (!_codes.TryRemove(code, out var entry))
            return null;

        if (DateTimeOffset.UtcNow > entry.Expiry)
            return null;

        return entry.UserId;
    }

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _codes)
        {
            if (now > kvp.Value.Expiry)
                _codes.TryRemove(kvp.Key, out _);
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}
