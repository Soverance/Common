using Microsoft.EntityFrameworkCore;
using Soverance.Auth.DTOs;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public class AuthResponseService
{
    private readonly TokenService _tokenService;

    public AuthResponseService(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> GenerateAuthResponseAsync(DbContext db, User user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = _tokenService.GetRefreshTokenExpiration(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Set<RefreshToken>().Add(refreshToken);
        await db.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = _tokenService.GetAccessTokenExpiration()
        };
    }
}
