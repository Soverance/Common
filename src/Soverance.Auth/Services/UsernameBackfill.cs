using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

/// <summary>
/// One-time, idempotent pass that replaces email-style usernames on OAuth accounts
/// with generated handles. Safe to run on every startup: clean handles are skipped.
/// </summary>
public static class UsernameBackfill
{
    public static async Task BackfillAsync(
        DbContext db,
        IUsernameGenerator generator,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var stale = await db.Set<User>()
            .Where(u => u.OAuthProvider != null && u.Username.Contains("@"))
            .ToListAsync(cancellationToken);

        if (stale.Count == 0) return;

        foreach (var user in stale)
        {
            user.Username = await generator.GenerateAsync(user.Email, db, cancellationToken);
            user.UpdatedAt = DateTimeOffset.UtcNow;
            // Save per-user so the generator's uniqueness check sees prior assignments
            // and two stale users that sanitize to the same base don't collide.
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Username backfill regenerated {Count} email-style usernames", stale.Count);
    }
}
