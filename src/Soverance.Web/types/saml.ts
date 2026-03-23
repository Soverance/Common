export interface SamlRoleMappingDto {
  idpGroupId: string
  roleName: string
}

export interface SamlConfigResponse {
  isEnabled: boolean
  idpEntityId: string | null
  idpSsoUrl: string | null
  idpSloUrl: string | null
  idpCertificate: string | null
  spEntityId: string | null
  spAcsUrl: string
  spMetadataUrl: string
  autoProvision: boolean
  roleMappings: SamlRoleMappingDto[]
}

export interface SamlConfigUpdateRequest {
  isEnabled: boolean
  idpEntityId: string
  idpSsoUrl: string
  idpSloUrl: string | null
  idpCertificate: string
  spEntityId: string
  autoProvision: boolean
  roleMappings: SamlRoleMappingDto[]
}

export interface CertificateValidateRequest {
  certificate: string
}

export interface CertificateValidateResponse {
  valid: boolean
  subject: string | null
  issuer: string | null
  notBefore: string | null
  notAfter: string | null
  error: string | null
}

export interface SamlStatusResponse {
  samlEnabled: boolean
}
