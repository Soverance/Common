using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Soverance.Auth.DTOs;
using Soverance.Auth.Exceptions;
using Soverance.Auth.Services;

namespace Soverance.Auth.Endpoints;

public static class OAuthEndpoints
{
    public static IEndpointRouteBuilder MapOAuthEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("/api/auth/oauth/{provider}", HandleAsync)
            .WithName("OAuthLogin")
            .AllowAnonymous();
        return builder;
    }

    private static async Task<IResult> HandleAsync(
        string provider,
        OAuthRequest request,
        IOAuthService oauthService,
        IOAuthAccountLinker linker,
        DbContext db,
        AuthResponseService authResponseService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Soverance.Auth.Endpoints.OAuthEndpoints");

        logger.LogInformation("OAuth login attempt {Provider}", provider);

        OAuthUserInfo info;
        try
        {
            info = await oauthService.GetUserInfoAsync(provider, request.Code, request.RedirectUri, ct);
        }
        catch (UnsupportedOAuthProviderException ex)
        {
            logger.LogWarning("Unsupported OAuth provider attempted {Provider}", ex.Provider);
            return Results.BadRequest(new { message = $"Unsupported OAuth provider: {ex.Provider}" });
        }
        catch (OAuthProviderException ex)
        {
            // Service already logged the upstream details at Warning.
            return Results.BadRequest(new { message = ex.Message });
        }

        Soverance.Auth.Models.User user;
        try
        {
            user = await linker.LinkOrCreateAsync(info, db, ct);
        }
        catch (OAuthAccountConflictException)
        {
            // Linker already logged the conflict at Warning. Sanitized client message
            // does NOT name the existing provider — enumeration vulnerability.
            return Results.Conflict(new { message = "This email is already registered with a different sign-in method." });
        }

        // Match SamlExchangeEndpoint: disabled accounts cannot log in via OAuth either.
        if (!user.IsEnabled)
        {
            logger.LogWarning("OAuth login blocked for disabled user {UserId}", user.Id);
            return Results.BadRequest(new { message = "Account is disabled" });
        }

        var response = await authResponseService.GenerateAuthResponseAsync(db, user);
        return Results.Ok(response);
    }
}
