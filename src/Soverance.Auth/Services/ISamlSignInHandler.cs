using Microsoft.AspNetCore.Http;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public interface ISamlSignInHandler
{
    string ErrorRedirectBase { get; }
    Task<IResult> HandleSignInAsync(HttpContext httpContext, User user);
}
