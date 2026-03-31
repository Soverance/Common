using System.Security.Claims;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Soverance.Auth.DTOs;
using Soverance.Auth.Models;
using Soverance.Auth.Services;

namespace Soverance.Auth.Endpoints;

public static class SamlEndpoints
{
    private const string NameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
    private const string GroupsClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups";
    private const string DisplayNameClaim = "http://schemas.microsoft.com/identity/claims/displayname";

    public static void MapSamlEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/auth/saml/status
        app.MapGet("/api/auth/saml/status", async (DbContext db) =>
        {
            var config = await db.Set<SamlConfig>().FirstOrDefaultAsync();
            return Results.Ok(new SamlStatusResponse(config?.IsEnabled == true));
        });

        // GET /api/auth/saml/login
        app.MapGet("/api/auth/saml/login", async (DbContext db, HttpContext httpContext) =>
        {
            var handler = httpContext.RequestServices.GetRequiredService<ISamlSignInHandler>();
            var config = await db.Set<SamlConfig>().FirstOrDefaultAsync();
            if (config == null || !config.IsEnabled)
                return Results.Redirect($"{handler.ErrorRedirectBase}?error=saml_disabled");

            var saml2Config = SamlService.BuildSaml2Configuration(config, httpContext);

            var binding = new Saml2RedirectBinding();
            var authnRequest = new Saml2AuthnRequest(saml2Config)
            {
                AssertionConsumerServiceUrl = new Uri($"{SamlService.GetBaseUrl(httpContext)}/api/auth/saml/acs")
            };

            binding.Bind(authnRequest);
            return Results.Redirect(binding.RedirectLocation.OriginalString);
        });

        // POST /api/auth/saml/acs
        app.MapPost("/api/auth/saml/acs", async (DbContext db, HttpContext httpContext) =>
        {
            var handler = httpContext.RequestServices.GetRequiredService<ISamlSignInHandler>();
            var config = await db.Set<SamlConfig>()
                .Include(c => c.RoleMappings)
                .FirstOrDefaultAsync();
            if (config == null || !config.IsEnabled)
                return Results.Redirect($"{handler.ErrorRedirectBase}?error=saml_disabled");

            try
            {
                var saml2Config = SamlService.BuildSaml2Configuration(config, httpContext);
                var binding = new Saml2PostBinding();
                var saml2AuthnResponse = new Saml2AuthnResponse(saml2Config);

                var genericRequest = httpContext.Request.ToGenericHttpRequest();
                binding.ReadSamlResponse(genericRequest, saml2AuthnResponse);

                if (saml2AuthnResponse.Status != Saml2StatusCodes.Success)
                    return Results.Redirect($"{handler.ErrorRedirectBase}?error=saml_failed");

                var claimsIdentity = saml2AuthnResponse.ClaimsIdentity;

                var usernameClaim = claimsIdentity.Claims
                    .FirstOrDefault(c => c.Type == NameClaim);

                if (usernameClaim == null || string.IsNullOrWhiteSpace(usernameClaim.Value))
                    return Results.Redirect($"{handler.ErrorRedirectBase}?error=no_username");

                var username = usernameClaim.Value;

                var displayName = claimsIdentity.Claims
                    .FirstOrDefault(c => c.Type == DisplayNameClaim)?.Value;

                // Read group GUIDs from assertion
                var groupIds = claimsIdentity.Claims
                    .Where(c => c.Type == GroupsClaim)
                    .Select(c => c.Value)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Map IdP groups to UserRole enum via role mappings.
                // RoleName stores enum string (e.g., "Admin", "Member").
                // Use the highest-privilege matched role.
                var mappedRoles = config.RoleMappings
                    .Where(m => groupIds.Contains(m.IdpGroupId))
                    .Select(m => Enum.TryParse<UserRole>(m.RoleName, out var role) ? role : (UserRole?)null)
                    .Where(r => r.HasValue)
                    .Select(r => r!.Value)
                    .ToList();

                var assignedRole = mappedRoles.Count > 0
                    ? mappedRoles.Max()  // Admin > Moderator > Member by enum order
                    : UserRole.Member;

                var user = await db.Set<User>()
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (user == null)
                {
                    if (!config.AutoProvision)
                        return Results.Redirect($"{handler.ErrorRedirectBase}?error=no_account");

                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = username, // SAML typically provides email as name claim
                        Username = displayName ?? username,
                        IsEnabled = true,
                        Role = assignedRole,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    db.Set<User>().Add(user);
                    await db.SaveChangesAsync();
                }
                else
                {
                    // Sync role and display name on every login
                    user.Role = assignedRole;
                    if (!string.IsNullOrWhiteSpace(displayName))
                        user.Username = displayName;
                    user.UpdatedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync();
                }

                if (!user.IsEnabled)
                    return Results.Redirect($"{handler.ErrorRedirectBase}?error=disabled");

                // Best-effort avatar sync from Graph API
                var graphPhoto = httpContext.RequestServices.GetService<IGraphPhotoService>();
                var avatarStore = httpContext.RequestServices.GetService<IAvatarStore>();
                if (graphPhoto != null && avatarStore != null)
                {
                    try
                    {
                        var photo = await graphPhoto.GetUserPhotoAsync(username);
                        if (photo != null)
                        {
                            user.AvatarUrl = await avatarStore.SaveAvatarAsync(user.Id, photo.Value.Data, photo.Value.ContentType);
                            await db.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Soverance.Auth.Endpoints.SamlEndpoints");
                        logger.LogWarning(ex, "Avatar sync failed for {Email}", username);
                    }
                }

                return await handler.HandleSignInAsync(httpContext, user);
            }
            catch (Exception ex)
            {
                var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Soverance.Auth.Endpoints.SamlEndpoints");
                logger.LogError(ex, "SAML ACS processing failed");
                return Results.Redirect($"{handler.ErrorRedirectBase}?error=saml_failed");
            }
        }).DisableAntiforgery();

        // GET /api/auth/saml/metadata
        app.MapGet("/api/auth/saml/metadata", async (DbContext db, HttpContext httpContext) =>
        {
            var config = await db.Set<SamlConfig>().FirstOrDefaultAsync();
            var baseUrl = SamlService.GetBaseUrl(httpContext);

            var spEntityId = config?.SpEntityId ?? $"{baseUrl}/api/auth/saml/metadata";

            var saml2Config = new Saml2Configuration { Issuer = spEntityId };
            var entityDescriptor = new EntityDescriptor(saml2Config);

            entityDescriptor.SPSsoDescriptor = new SPSsoDescriptor
            {
                AuthnRequestsSigned = false,
                WantAssertionsSigned = true,
                AssertionConsumerServices = new[]
                {
                    new AssertionConsumerService
                    {
                        Binding = ProtocolBindings.HttpPost,
                        Location = new Uri($"{baseUrl}/api/auth/saml/acs"),
                        IsDefault = true
                    }
                },
                NameIDFormats = new[] { NameIdentifierFormats.Unspecified }
            };

            var xml = new Saml2Metadata(entityDescriptor).CreateMetadata().ToXml();
            return Results.Content(xml, "application/xml");
        });
    }
}
