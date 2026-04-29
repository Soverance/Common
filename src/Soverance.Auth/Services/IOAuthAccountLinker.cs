using Microsoft.EntityFrameworkCore;
using Soverance.Auth.DTOs;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public interface IOAuthAccountLinker
{
    /// <summary>
    /// Resolves an OAuth login to a User. Path 1: existing (provider, providerId) match.
    /// Path 2: link by verified email when user has no OAuth set. Path 3: throw conflict
    /// if email matches a user already linked to a different OAuth identity.
    /// Path 4: create a new user with default Member role.
    /// </summary>
    /// <exception cref="Soverance.Auth.Exceptions.OAuthAccountConflictException">Path 3.</exception>
    Task<User> LinkOrCreateAsync(
        OAuthUserInfo userInfo,
        DbContext db,
        CancellationToken cancellationToken = default);
}
