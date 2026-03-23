using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public class CookieSamlSignInHandler : ISamlSignInHandler
{
    public string ErrorRedirectBase => "/login";

    public async Task<IResult> HandleSignInAsync(HttpContext httpContext, User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Results.Redirect("/");
    }
}
