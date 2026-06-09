using Microsoft.EntityFrameworkCore;

namespace Soverance.Auth.Services;

public interface IUsernameGenerator
{
    /// <summary>
    /// Produces a unique, URL-safe handle derived from the email local-part,
    /// appending a numeric suffix on collision against the Users.Username unique index.
    /// </summary>
    Task<string> GenerateAsync(string email, DbContext db, CancellationToken cancellationToken = default);
}
