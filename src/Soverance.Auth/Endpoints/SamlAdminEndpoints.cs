using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Soverance.Auth.DTOs;
using Soverance.Auth.Models;
using Soverance.Auth.Services;

namespace Soverance.Auth.Endpoints;

public static class SamlAdminEndpoints
{
    public static void MapSamlAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/admin/saml
        app.MapGet("/api/admin/saml", async (DbContext db, HttpContext httpContext) =>
        {
            var config = await db.Set<SamlConfig>()
                .Include(c => c.RoleMappings)
                .FirstOrDefaultAsync();
            var baseUrl = SamlService.GetBaseUrl(httpContext);

            if (config == null)
            {
                return Results.Ok(new SamlConfigResponse(
                    false, null, null, null, null, null,
                    $"{baseUrl}/api/auth/saml/acs",
                    $"{baseUrl}/api/auth/saml/metadata",
                    false, new List<SamlRoleMappingDto>()));
            }

            return Results.Ok(new SamlConfigResponse(
                config.IsEnabled,
                config.IdpEntityId,
                config.IdpSsoUrl,
                config.IdpSloUrl,
                config.IdpCertificate,
                config.SpEntityId,
                $"{baseUrl}/api/auth/saml/acs",
                $"{baseUrl}/api/auth/saml/metadata",
                config.AutoProvision,
                config.RoleMappings.Select(m => new SamlRoleMappingDto(m.IdpGroupId, m.RoleName)).ToList()));
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));

        // PUT /api/admin/saml
        app.MapPut("/api/admin/saml", async (DbContext db, SamlConfigUpdateRequest req) =>
        {
            var config = await db.Set<SamlConfig>()
                .Include(c => c.RoleMappings)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                config = new SamlConfig();
                db.Set<SamlConfig>().Add(config);
            }

            config.IsEnabled = req.IsEnabled;
            config.IdpEntityId = req.IdpEntityId;
            config.IdpSsoUrl = req.IdpSsoUrl;
            config.IdpSloUrl = req.IdpSloUrl;
            config.IdpCertificate = req.IdpCertificate;
            config.SpEntityId = req.SpEntityId;
            config.AutoProvision = req.AutoProvision;

            config.RoleMappings.Clear();
            foreach (var mapping in req.RoleMappings)
            {
                config.RoleMappings.Add(new SamlRoleMapping
                {
                    IdpGroupId = mapping.IdpGroupId,
                    RoleName = mapping.RoleName
                });
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));

        // POST /api/admin/saml/validate-certificate
        app.MapPost("/api/admin/saml/validate-certificate", (CertificateValidateRequest req) =>
        {
            try
            {
                var bytes = Convert.FromBase64String(req.Certificate.Trim());
                var cert = X509CertificateLoader.LoadCertificate(bytes);
                return Results.Ok(new CertificateValidateResponse(
                    true, cert.Subject, cert.Issuer, cert.NotBefore, cert.NotAfter, null));
            }
            catch (Exception ex)
            {
                return Results.Ok(new CertificateValidateResponse(
                    false, null, null, null, null, ex.Message));
            }
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));
    }
}
