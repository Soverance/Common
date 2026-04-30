// Existing exports
export { default as SamlTab } from './components/SamlTab'
export type { SamlTabProps } from './components/SamlTab'
export type {
  SamlRoleMappingDto,
  SamlConfigResponse,
  SamlConfigUpdateRequest,
  CertificateValidateRequest,
  CertificateValidateResponse,
  SamlStatusResponse,
} from './types/saml'

// Theme system (new in 1.1.0)
export { ThemeProvider } from './theme/ThemeProvider'
export { useTheme } from './theme/useTheme'
export type { Theme, ThemeContextValue } from './theme/ThemeContext'
export { ThemeSwitcher } from './components/ThemeSwitcher'
