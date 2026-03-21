using System.Security.Cryptography.X509Certificates;
using ITfoxtec.Identity.Saml2;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public static class SamlService
{
    public static Saml2Configuration BuildSaml2Configuration(SamlConfig config, HttpContext httpContext)
    {
        var certBytes = Convert.FromBase64String(config.IdpCertificate.Trim());
        var cert = X509CertificateLoader.LoadCertificate(certBytes);

        var saml2Config = new Saml2Configuration
        {
            Issuer = config.SpEntityId,
            SingleSignOnDestination = new Uri(config.IdpSsoUrl),
            CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
        };
        saml2Config.SignatureValidationCertificates.Add(cert);

        saml2Config.AllowedAudienceUris.Add(config.SpEntityId);
        var trimmed = config.SpEntityId.TrimEnd('/');
        if (trimmed != config.SpEntityId)
            saml2Config.AllowedAudienceUris.Add(trimmed);
        else
            saml2Config.AllowedAudienceUris.Add(config.SpEntityId + "/");

        if (!string.IsNullOrWhiteSpace(config.IdpSloUrl))
        {
            saml2Config.SingleLogoutDestination = new Uri(config.IdpSloUrl);
        }

        return saml2Config;
    }

    public static string GetBaseUrl(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var env = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var scheme = env.IsDevelopment() ? request.Scheme : "https";
        return $"{scheme}://{request.Host}";
    }
}
