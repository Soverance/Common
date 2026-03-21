namespace Soverance.Auth.DTOs;

public record SamlRoleMappingDto(string IdpGroupId, string RoleName);

public record SamlConfigResponse(
    bool IsEnabled,
    string? IdpEntityId,
    string? IdpSsoUrl,
    string? IdpSloUrl,
    string? IdpCertificate,
    string? SpEntityId,
    string SpAcsUrl,
    string SpMetadataUrl,
    bool AutoProvision,
    List<SamlRoleMappingDto> RoleMappings);

public record SamlConfigUpdateRequest(
    bool IsEnabled,
    string IdpEntityId,
    string IdpSsoUrl,
    string? IdpSloUrl,
    string IdpCertificate,
    string SpEntityId,
    bool AutoProvision,
    List<SamlRoleMappingDto> RoleMappings);

public record CertificateValidateRequest(string Certificate);

public record CertificateValidateResponse(
    bool Valid,
    string? Subject,
    string? Issuer,
    DateTime? NotBefore,
    DateTime? NotAfter,
    string? Error);

public record SamlStatusResponse(bool SamlEnabled);
