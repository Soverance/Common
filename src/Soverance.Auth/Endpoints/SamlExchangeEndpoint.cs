using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Soverance.Auth.DTOs;
using Soverance.Auth.Models;
using Soverance.Auth.Services;

namespace Soverance.Auth.Endpoints;

public static class SamlExchangeEndpoint
{
    public static void MapSamlExchangeEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/saml/exchange", async (
            SamlExchangeRequest request,
            ISamlSignInHandler signInHandler,
            AuthResponseService authResponseService,
            DbContext db) =>
        {
            if (signInHandler is not JwtSamlSignInHandler handler)
                return Results.BadRequest(new { message = "SAML code exchange is not supported in this configuration" });

            if (string.IsNullOrWhiteSpace(request.Code))
                return Results.BadRequest(new { message = "Code is required" });

            var userId = handler.RedeemCode(request.Code);
            if (userId is null)
                return Results.BadRequest(new { message = "Invalid, expired, or already used code" });

            var user = await db.Set<User>().FindAsync(userId.Value);
            if (user is null)
                return Results.BadRequest(new { message = "User not found" });

            if (!user.IsEnabled)
                return Results.BadRequest(new { message = "Account is disabled" });

            var response = await authResponseService.GenerateAuthResponseAsync(db, user);
            return Results.Ok(response);
        });
    }
}
