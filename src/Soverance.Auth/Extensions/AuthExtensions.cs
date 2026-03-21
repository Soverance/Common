using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Soverance.Auth.Auth;
using Soverance.Auth.Services;

namespace Soverance.Auth.Extensions;

public static class AuthExtensions
{
    /// <summary>
    /// Cookie auth for browser-based SPAs (soverance.com pattern).
    /// </summary>
    public static AuthenticationBuilder AddSoveranceCookieAuth(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        return services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };
            });
    }

    /// <summary>
    /// JWT bearer auth for API clients (Vanalytics pattern).
    /// Reads Jwt:Secret, Jwt:Issuer, Jwt:Audience from configuration.
    /// </summary>
    public static AuthenticationBuilder AddSoveranceJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<TokenService>();

        return services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!))
                };
            });
    }

    /// <summary>
    /// API key auth scheme (Vanalytics addon sync).
    /// Call on the AuthenticationBuilder returned by AddSoveranceJwtAuth or AddSoveranceCookieAuth.
    /// </summary>
    public static AuthenticationBuilder AddSoveranceApiKeyAuth(
        this AuthenticationBuilder builder)
    {
        return builder.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);
    }
}
