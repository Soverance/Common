using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Soverance.Auth.Auth;
using Soverance.Auth.Models;
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

    /// <summary>
    /// Registers OAuth (Google + Microsoft) services and binds OAuthOptions from
    /// the specified configuration section (default "OAuth"). Also registers
    /// AuthResponseService via TryAddScoped so MapOAuthEndpoints works standalone.
    /// </summary>
    public static IServiceCollection AddSoveranceOAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "OAuth")
    {
        // Note: ValidateOnStart deliberately omitted. The conditional shape of OAuth
        // config (a deployment may configure only Google, only Microsoft, or both)
        // can't be expressed with [Required] data annotations alone — that would
        // demand both providers always be configured. Either both are needed and a
        // custom IValidateOptions<OAuthOptions> is added, or — as today — misconfig
        // surfaces at first OAuth request as a 400 from the provider. Acceptable for
        // initial scope; revisit when adding a third provider or stricter requirements.
        services.AddOptions<OAuthOptions>()
            .Bind(configuration.GetSection(sectionName));
        services.AddHttpClient();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IOAuthAccountLinker, OAuthAccountLinker>();
        services.TryAddScoped<AuthResponseService>();
        return services;
    }
}
