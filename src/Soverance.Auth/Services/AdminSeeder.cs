using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public static class AdminSeeder
{
    public static async Task SeedAsync(
        DbContext db,
        string email,
        string username,
        string password,
        ILogger logger)
    {
        var systemAccounts = await db.Set<User>()
            .Where(u => u.IsSystemAccount)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

        if (systemAccounts.Count > 1)
        {
            logger.LogWarning(
                "Multiple system accounts found ({Count}). Syncing the oldest one.",
                systemAccounts.Count);
        }

        var existing = systemAccounts.FirstOrDefault();
        if (existing is not null)
        {
            var changed = false;

            if (existing.Email != email)
            {
                existing.Email = email;
                changed = true;
            }

            if (existing.Username != username)
            {
                existing.Username = username;
                changed = true;
            }

            if (existing.PasswordHash is null || !PasswordHasher.VerifyPassword(password, existing.PasswordHash))
            {
                existing.PasswordHash = PasswordHasher.HashPassword(password);
                changed = true;
            }

            if (existing.Role != UserRole.Admin) { existing.Role = UserRole.Admin; changed = true; }
            if (!existing.IsSystemAccount) { existing.IsSystemAccount = true; changed = true; }
            if (!existing.IsEnabled) { existing.IsEnabled = true; changed = true; }

            if (changed)
            {
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                logger.LogInformation("System admin account updated: {Username}", existing.Username);
            }

            return;
        }

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = UserRole.Admin,
            IsSystemAccount = true,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Set<User>().Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Admin user seeded: {Username}", username);
    }
}
