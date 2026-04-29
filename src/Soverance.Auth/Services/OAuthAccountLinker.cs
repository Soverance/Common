using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Soverance.Auth.DTOs;
using Soverance.Auth.Exceptions;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public sealed class OAuthAccountLinker : IOAuthAccountLinker
{
    private readonly ILogger<OAuthAccountLinker> _logger;

    public OAuthAccountLinker(ILogger<OAuthAccountLinker> logger)
    {
        _logger = logger;
    }

    public async Task<User> LinkOrCreateAsync(
        OAuthUserInfo info,
        DbContext db,
        CancellationToken cancellationToken = default)
    {
        var users = db.Set<User>();

        // Path 1: exact match by (provider, providerId)
        var existing = await users.FirstOrDefaultAsync(
            u => u.OAuthProvider == info.Provider && u.OAuthId == info.ProviderId,
            cancellationToken);

        if (existing is not null)
        {
            if (info.AvatarUrl is not null)
                existing.AvatarUrl = info.AvatarUrl;
            if (!string.IsNullOrEmpty(info.Name))
                existing.DisplayName = info.Name;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("OAuth user matched {UserId} {MatchPath}", existing.Id, "provider_id");
            return existing;
        }

        // Email lookup for Paths 2 and 3
        var byEmail = await users.FirstOrDefaultAsync(u => u.Email == info.Email, cancellationToken);

        if (byEmail is not null)
        {
            if (byEmail.OAuthProvider is not null)
            {
                // Path 3: email matches a user already linked to a different OAuth identity
                _logger.LogWarning(
                    "OAuth account conflict {Email} existing-provider={Existing} attempted-provider={Attempted}",
                    info.Email, byEmail.OAuthProvider, info.Provider);
                throw new OAuthAccountConflictException(info.Email);
            }

            // Path 2: link OAuth identity to existing local-password account
            byEmail.OAuthProvider = info.Provider;
            byEmail.OAuthId = info.ProviderId;
            byEmail.AvatarUrl = info.AvatarUrl;
            byEmail.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("OAuth user matched {UserId} {MatchPath}", byEmail.Id, "email_link");
            return byEmail;
        }

        // Path 4: create new user
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = info.Email,
            Username = info.Email,
            DisplayName = info.Name,
            AvatarUrl = info.AvatarUrl,
            OAuthProvider = info.Provider,
            OAuthId = info.ProviderId,
            Role = UserRole.Member,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        users.Add(newUser);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OAuth user matched {UserId} {MatchPath}", newUser.Id, "created");
        return newUser;
    }
}
