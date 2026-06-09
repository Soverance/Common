using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public sealed partial class UsernameGenerator : IUsernameGenerator
{
    private const int MinLength = 3;
    private const int MaxLength = 64;       // matches the Users.Username column cap
    private const int MaxBaseLength = 58;   // base handle, before any numeric suffix

    public async Task<string> GenerateAsync(string email, DbContext db, CancellationToken cancellationToken = default)
    {
        var baseHandle = Sanitize(email);
        var users = db.Set<User>();

        if (!await users.AnyAsync(u => u.Username == baseHandle, cancellationToken))
            return baseHandle;

        for (var suffix = 2; ; suffix++)
        {
            var suffixStr = suffix.ToString();
            // Keep the candidate within the column cap even for very long suffixes.
            var trimmedBase = baseHandle.Length + suffixStr.Length > MaxLength
                ? baseHandle[..(MaxLength - suffixStr.Length)]
                : baseHandle;
            var candidate = trimmedBase + suffixStr;
            if (!await users.AnyAsync(u => u.Username == candidate, cancellationToken))
                return candidate;
        }
    }

    private static string Sanitize(string email)
    {
        var localPart = (email ?? string.Empty).Split('@')[0].ToLowerInvariant();
        var cleaned = DisallowedChars().Replace(localPart, "").Trim('.', '_', '-');

        if (cleaned.Length > MaxBaseLength)
            cleaned = cleaned[..MaxBaseLength].TrimEnd('.', '_', '-');

        if (cleaned.Length == 0)
            return "user";

        if (cleaned.Length < MinLength)
            cleaned = cleaned.PadRight(MinLength, '0');

        return cleaned;
    }

    [GeneratedRegex("[^a-z0-9._-]")]
    private static partial Regex DisallowedChars();
}
