using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Soverance.Auth.Models;
using Soverance.Auth.Services;

namespace Soverance.Auth.Auth;

/// <summary>
/// Resolves the <see cref="User"/> for a raw API key. Fast path: a single indexed lookup by the
/// deterministic SHA-256 hash (<see cref="ApiKeyHasher"/>). Legacy path (keys created before
/// ApiKeyLookup existed, stored BCrypt-only): a parallel BCrypt scan over just those rows, which
/// self-heals by backfilling the lookup hash so the user is O(1) thereafter.
/// </summary>
public static class ApiKeyUserResolver
{
    public static async Task<User?> ResolveAsync(DbContext db, string apiKey, ILogger? logger = null)
    {
        var lookup = ApiKeyHasher.Lookup(apiKey);

        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.ApiKeyLookup == lookup);
        if (user is not null) return user;

        // Transitional: only un-migrated (BCrypt-only) rows; shrinks to empty as users backfill.
        var legacy = await db.Set<User>()
            .Where(u => u.ApiKey != null && u.ApiKeyLookup == null)
            .ToListAsync();
        user = legacy.AsParallel().FirstOrDefault(u => BCrypt.Net.BCrypt.Verify(apiKey, u.ApiKey!));
        if (user is null) return null;

        try
        {
            user.ApiKeyLookup = lookup;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Auth already succeeded; a failed backfill just means the next request retries it.
            logger?.LogWarning(ex, "API key lookup backfill failed for user {UserId}", user.Id);
        }
        return user;
    }
}
