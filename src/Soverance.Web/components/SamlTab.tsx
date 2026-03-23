import { useState, useEffect } from 'react'
import type {
  SamlConfigResponse,
  SamlConfigUpdateRequest,
  CertificateValidateRequest,
  CertificateValidateResponse,
} from '../types/saml'

export interface SamlTabProps {
  getSamlConfig: () => Promise<SamlConfigResponse>
  updateSamlConfig: (data: SamlConfigUpdateRequest) => Promise<void>
  validateCertificate: (data: CertificateValidateRequest) => Promise<CertificateValidateResponse>
}

const ROLE_OPTIONS = ['Member', 'Moderator', 'Admin']

export default function SamlTab({ getSamlConfig, updateSamlConfig, validateCertificate }: SamlTabProps) {
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const [isEnabled, setIsEnabled] = useState(false)
  const [idpEntityId, setIdpEntityId] = useState('')
  const [idpSsoUrl, setIdpSsoUrl] = useState('')
  const [idpSloUrl, setIdpSloUrl] = useState('')
  const [idpCertificate, setIdpCertificate] = useState('')
  const [spEntityId, setSpEntityId] = useState('')
  const [spAcsUrl, setSpAcsUrl] = useState('')
  const [spMetadataUrl, setSpMetadataUrl] = useState('')
  const [autoProvision, setAutoProvision] = useState(false)
  const [roleMappings, setRoleMappings] = useState<{ idpGroupId: string; roleName: string }[]>([])

  const [certResult, setCertResult] = useState<CertificateValidateResponse | null>(null)
  const [certValidating, setCertValidating] = useState(false)
  const [copied, setCopied] = useState<string | null>(null)

  useEffect(() => {
    getSamlConfig()
      .then(config => {
        setIsEnabled(config.isEnabled)
        setIdpEntityId(config.idpEntityId ?? '')
        setIdpSsoUrl(config.idpSsoUrl ?? '')
        setIdpSloUrl(config.idpSloUrl ?? '')
        setIdpCertificate(config.idpCertificate ?? '')
        setSpEntityId(config.spEntityId ?? '')
        setSpAcsUrl(config.spAcsUrl)
        setSpMetadataUrl(config.spMetadataUrl)
        setAutoProvision(config.autoProvision)
        setRoleMappings(config.roleMappings.map(m => ({ idpGroupId: m.idpGroupId, roleName: m.roleName })))
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  async function handleSave(e: React.FormEvent) {
    e.preventDefault()
    setMessage(null)
    setSaving(true)
    try {
      await updateSamlConfig({
        isEnabled,
        idpEntityId,
        idpSsoUrl,
        idpSloUrl: idpSloUrl || null,
        idpCertificate,
        spEntityId,
        autoProvision,
        roleMappings: roleMappings.filter(m => m.idpGroupId.trim() && m.roleName.trim()),
      })
      setMessage({ type: 'success', text: 'SAML configuration saved.' })
    } catch (err: any) {
      setMessage({ type: 'error', text: err.message || 'Failed to save configuration.' })
    } finally {
      setSaving(false)
    }
  }

  async function handleValidateCert() {
    setCertResult(null)
    setCertValidating(true)
    try {
      const result = await validateCertificate({ certificate: idpCertificate })
      setCertResult(result)
    } catch {
      setCertResult({ valid: false, subject: null, issuer: null, notBefore: null, notAfter: null, error: 'Failed to validate certificate.' })
    } finally {
      setCertValidating(false)
    }
  }

  function copyToClipboard(text: string, label: string) {
    navigator.clipboard.writeText(text).then(() => {
      setCopied(label)
      setTimeout(() => setCopied(null), 2000)
    })
  }

  function addMapping() {
    setRoleMappings(prev => [...prev, { idpGroupId: '', roleName: ROLE_OPTIONS[0] ?? '' }])
  }

  function removeMapping(index: number) {
    setRoleMappings(prev => prev.filter((_, i) => i !== index))
  }

  function updateMapping(index: number, field: 'idpGroupId' | 'roleName', value: string) {
    setRoleMappings(prev => prev.map((m, i) => i === index ? { ...m, [field]: value } : m))
  }

  if (loading) {
    return <div className="py-16 text-center text-sov-light/50">Loading SAML configuration...</div>
  }

  return (
    <form onSubmit={handleSave} className="space-y-8">
      {/* Enable/Disable */}
      <div className="flex items-center gap-3">
        <label className="relative inline-flex items-center cursor-pointer">
          <input type="checkbox" checked={isEnabled} onChange={e => setIsEnabled(e.target.checked)} className="sr-only peer" />
          <div className="w-11 h-6 bg-sov-card rounded-full peer peer-checked:bg-sov-cyan/80 after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:after:translate-x-full"></div>
        </label>
        <span className="text-sov-light font-semibold">Enable SAML Authentication</span>
      </div>

      {/* IDP Configuration */}
      <fieldset className="bg-sov-card/30 rounded-lg p-4 space-y-4">
        <legend className="text-white font-semibold text-sm uppercase tracking-wider px-2">Identity Provider (IDP)</legend>
        <div>
          <label className="block text-sm text-sov-light/70 mb-1">Entity ID</label>
          <input
            type="text"
            value={idpEntityId}
            onChange={e => setIdpEntityId(e.target.value)}
            placeholder="https://sts.windows.net/{tenant-id}/"
            className="w-full px-3 py-2 rounded bg-sov-card border border-sov-card/60 text-sov-light focus:outline-none focus:border-sov-cyan transition-colors"
          />
        </div>
        <div>
          <label className="block text-sm text-sov-light/70 mb-1">SSO URL</label>
          <input
            type="url"
            value={idpSsoUrl}
            onChange={e => setIdpSsoUrl(e.target.value)}
            placeholder="https://login.microsoftonline.com/{tenant-id}/saml2"
            className="w-full px-3 py-2 rounded bg-sov-card border border-sov-card/60 text-sov-light focus:outline-none focus:border-sov-cyan transition-colors"
          />
        </div>
        <div>
          <label className="block text-sm text-sov-light/70 mb-1">SLO URL <span className="text-sov-light/40">(optional)</span></label>
          <input
            type="url"
            value={idpSloUrl}
            onChange={e => setIdpSloUrl(e.target.value)}
            placeholder="https://login.microsoftonline.com/{tenant-id}/saml2"
            className="w-full px-3 py-2 rounded bg-sov-card border border-sov-card/60 text-sov-light focus:outline-none focus:border-sov-cyan transition-colors"
          />
        </div>
        <div>
          <label className="block text-sm text-sov-light/70 mb-1">Signing Certificate</label>
          <div className="flex items-center gap-3 mb-2">
            <label className="px-3 py-1 rounded border border-sov-cyan/50 text-sov-cyan text-sm hover:bg-sov-cyan/10 transition-colors cursor-pointer">
              Upload .cer file
              <input
                type="file"
                accept=".cer,.crt,.pem"
                className="hidden"
                onChange={e => {
                  const file = e.target.files?.[0]
                  if (!file) return
                  const reader = new FileReader()
                  reader.onload = () => {
                    const arrayBuffer = reader.result as ArrayBuffer
                    const bytes = new Uint8Array(arrayBuffer)
                    // Check if it's PEM text (starts with "-----")
                    const firstBytes = String.fromCharCode(...bytes.slice(0, 5))
                    let base64: string
                    if (firstBytes === '-----') {
                      // PEM-encoded: strip headers/footers and whitespace
                      const pem = new TextDecoder().decode(bytes)
                      base64 = pem.replace(/-----[A-Z ]+-----/g, '').replace(/\s/g, '')
                    } else {
                      // DER binary: convert to base64
                      const binary = Array.from(bytes, b => String.fromCharCode(b)).join('')
                      base64 = btoa(binary)
                    }
                    setIdpCertificate(base64)
                    setCertResult(null)
                  }
                  reader.readAsArrayBuffer(file)
                  e.target.value = ''
                }}
              />
            </label>
            <span className="text-sov-light/40 text-xs">or paste base64 below</span>
          </div>
          <textarea
            value={idpCertificate}
            onChange={e => { setIdpCertificate(e.target.value); setCertResult(null) }}
            rows={4}
            placeholder="Base64-encoded certificate data (auto-filled when you upload a .cer file)"
            className="w-full px-3 py-2 rounded bg-sov-card border border-sov-card/60 text-sov-light focus:outline-none focus:border-sov-cyan transition-colors font-mono text-xs"
          />
          <div className="flex items-center gap-3 mt-2">
            <button
              type="button"
              onClick={handleValidateCert}
              disabled={!idpCertificate.trim() || certValidating}
              className="px-3 py-1 rounded border border-sov-cyan/50 text-sov-cyan text-sm hover:bg-sov-cyan/10 transition-colors disabled:opacity-50"
            >
              {certValidating ? 'Validating...' : 'Validate Certificate'}
            </button>
            {certResult && (
              certResult.valid ? (
                <span className="text-green-400 text-sm">
                  Valid &mdash; {certResult.subject} (expires {certResult.notAfter ? new Date(certResult.notAfter).toLocaleDateString() : '?'})
                </span>
              ) : (
                <span className="text-red-400 text-sm">{certResult.error}</span>
              )
            )}
          </div>
        </div>
      </fieldset>

      {/* SP Configuration */}
      <fieldset className="bg-sov-card/30 rounded-lg p-4 space-y-4">
        <legend className="text-white font-semibold text-sm uppercase tracking-wider px-2">Service Provider (SP)</legend>
        <div>
          <label className="block text-sm text-sov-light/70 mb-1">Entity ID</label>
          <input
            type="text"
            value={spEntityId}
            onChange={e => setSpEntityId(e.target.value)}
            placeholder="https://yourdomain.com/api/auth/saml/metadata"
            className="w-full px-3 py-2 rounded bg-sov-card border border-sov-card/60 text-sov-light focus:outline-none focus:border-sov-cyan transition-colors"
          />
        </div>
        <div>
          <label className="block text-sm text-sov-light/70 mb-1">Reply URL / ACS <span className="text-sov-light/40">(read-only &mdash; add this to Entra ID)</span></label>
          <div className="flex gap-2">
            <input
              type="text"
              value={spAcsUrl}
              readOnly
              className="flex-1 px-3 py-2 rounded bg-sov-card border border-sov-card/60 text-sov-light/60 cursor-default"
            />
            <button
              type="button"
              onClick={() => copyToClipboard(spAcsUrl, 'acs')}
              className="px-3 py-2 rounded border border-sov-card text-sov-light/70 text-sm hover:text-white transition-colors"
            >
              {copied === 'acs' ? 'Copied!' : 'Copy'}
            </button>
          </div>
        </div>
        <div>
          <label className="block text-sm text-sov-light/70 mb-1">Metadata URL <span className="text-sov-light/40">(read-only)</span></label>
          <div className="flex gap-2">
            <input
              type="text"
              value={spMetadataUrl}
              readOnly
              className="flex-1 px-3 py-2 rounded bg-sov-card border border-sov-card/60 text-sov-light/60 cursor-default"
            />
            <a
              href={spMetadataUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="px-3 py-2 rounded border border-sov-cyan/50 text-sov-cyan text-sm hover:bg-sov-cyan/10 transition-colors"
            >
              Open
            </a>
            <button
              type="button"
              onClick={() => copyToClipboard(spMetadataUrl, 'metadata')}
              className="px-3 py-2 rounded border border-sov-card text-sov-light/70 text-sm hover:text-white transition-colors"
            >
              {copied === 'metadata' ? 'Copied!' : 'Copy'}
            </button>
          </div>
        </div>
      </fieldset>

      {/* Auto-Provisioning */}
      <fieldset className="bg-sov-card/30 rounded-lg p-4 space-y-4">
        <legend className="text-white font-semibold text-sm uppercase tracking-wider px-2">Auto-Provisioning</legend>
        <label className="flex items-center gap-3 cursor-pointer">
          <input type="checkbox" checked={autoProvision} onChange={e => setAutoProvision(e.target.checked)} />
          <span className="text-sov-light text-sm">Automatically create accounts for new SSO users</span>
        </label>
      </fieldset>

      {/* Role Mapping */}
      <fieldset className="bg-sov-card/30 rounded-lg p-4 space-y-4">
        <legend className="text-white font-semibold text-sm uppercase tracking-wider px-2">Role Mapping</legend>
        <p className="text-sov-light/60 text-sm">
          Map Entra ID group GUIDs to application roles. On each SSO login, user roles are synced to match their current IDP group membership.
        </p>
        {roleMappings.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-sov-card/50">
                  <th className="px-3 py-2 text-xs font-semibold text-sov-light/70 uppercase tracking-wider">IdP Group ID</th>
                  <th className="px-3 py-2 text-xs font-semibold text-sov-light/70 uppercase tracking-wider">Role</th>
                  <th className="px-3 py-2 text-xs font-semibold text-sov-light/70 uppercase tracking-wider w-16"></th>
                </tr>
              </thead>
              <tbody>
                {roleMappings.map((mapping, index) => (
                  <tr key={index} className="border-b border-sov-card/30">
                    <td className="px-3 py-2">
                      <input
                        type="text"
                        value={mapping.idpGroupId}
                        onChange={e => updateMapping(index, 'idpGroupId', e.target.value)}
                        placeholder="e.g. a1b2c3d4-e5f6-7890-abcd-ef1234567890"
                        className="w-full px-2 py-1 rounded bg-sov-card border border-sov-card/60 text-sov-light text-sm focus:outline-none focus:border-sov-cyan transition-colors font-mono"
                      />
                    </td>
                    <td className="px-3 py-2">
                      <select
                        value={mapping.roleName}
                        onChange={e => updateMapping(index, 'roleName', e.target.value)}
                        className="px-2 py-1 rounded bg-sov-card border border-sov-card/60 text-sov-light text-sm focus:outline-none focus:border-sov-cyan transition-colors"
                      >
                        {ROLE_OPTIONS.map(role => (
                          <option key={role} value={role}>{role}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-3 py-2">
                      <button
                        type="button"
                        onClick={() => removeMapping(index)}
                        className="px-2 py-1 rounded border border-red-500/50 text-red-400 text-xs hover:bg-red-500/10 transition-colors"
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <button
          type="button"
          onClick={addMapping}
          className="px-3 py-1 rounded border border-sov-cyan/50 text-sov-cyan text-sm hover:bg-sov-cyan/10 transition-colors"
        >
          + Add Mapping
        </button>
      </fieldset>

      {/* Save */}
      {message && (
        <p className={`text-sm ${message.type === 'success' ? 'text-green-400' : 'text-red-400'}`}>
          {message.text}
        </p>
      )}
      <button
        type="submit"
        disabled={saving}
        className="px-6 py-2 rounded bg-gradient-to-r from-sov-teal to-sov-cyan text-white font-semibold hover:opacity-90 transition-opacity disabled:opacity-50"
      >
        {saving ? 'Saving...' : 'Save Configuration'}
      </button>
    </form>
  )
}
