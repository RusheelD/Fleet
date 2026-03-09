import { PublicClientApplication, type Configuration, type RedirectRequest } from '@azure/msal-browser'

/**
 * MSAL configuration for Microsoft Entra ID authentication.
 *
 * Replace the placeholder values with your Entra ID app registration details:
 * - VITE_ENTRA_CLIENT_ID: The Application (client) ID of your SPA app registration
 * - VITE_ENTRA_AUTHORITY: https://login.microsoftonline.com/{tenantId}
 * - VITE_ENTRA_API_SCOPE: api://{apiClientId}/access_as_user
 */

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined
const authority = import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE as string | undefined
const googleAuthority = import.meta.env.VITE_ENTRA_GOOGLE_AUTHORITY as string | undefined
const googleDomainHint = import.meta.env.VITE_ENTRA_GOOGLE_DOMAIN_HINT as string | undefined
const googleIdpHint = import.meta.env.VITE_ENTRA_GOOGLE_IDP_HINT as string | undefined
const githubAuthority = import.meta.env.VITE_ENTRA_GITHUB_AUTHORITY as string | undefined
const githubDomainHint = import.meta.env.VITE_ENTRA_GITHUB_DOMAIN_HINT as string | undefined
const githubIdpHint = import.meta.env.VITE_ENTRA_GITHUB_IDP_HINT as string | undefined
const googlePrompt = import.meta.env.VITE_ENTRA_GOOGLE_PROMPT as string | undefined
const githubPrompt = import.meta.env.VITE_ENTRA_GITHUB_PROMPT as string | undefined

export type AuthProvider = 'microsoft' | 'google' | 'github'

function normalizeHint(value: string | undefined, fallback: string): string {
  const normalized = value?.trim()
  return normalized && normalized.length > 0 ? normalized : fallback
}

function normalizeOptional(value: string | undefined): string | undefined {
  const normalized = value?.trim()
  return normalized && normalized.length > 0 ? normalized : undefined
}

function resolvePrompt(value: string | undefined, fallback: RedirectRequest['prompt']): RedirectRequest['prompt'] {
  const normalized = normalizeOptional(value)
  if (!normalized) {
    return fallback
  }

  const allowedPrompts: RedirectRequest['prompt'][] = ['login', 'none', 'consent', 'select_account', 'create']
  return allowedPrompts.includes(normalized as RedirectRequest['prompt'])
    ? normalized as RedirectRequest['prompt']
    : fallback
}

function buildProviderExtraQueryParameters(
  domainHint: string | undefined,
  idpHint: string | undefined,
  fallbackDomainHint: string,
): RedirectRequest['extraQueryParameters'] {
  const resolvedDomainHint = normalizeHint(domainHint, fallbackDomainHint)
  const resolvedIdpHint = idpHint?.trim()

  return {
    // domain_hint helps issuer acceleration in Entra/B2C user flows.
    domain_hint: resolvedDomainHint,
    // idp is optional and tenant-specific; only send it when explicitly configured.
    ...(resolvedIdpHint && resolvedIdpHint.length > 0 ? { idp: resolvedIdpHint } : {}),
  }
}

function createProviderLoginRequest(
  providerAuthority: string | undefined,
  providerDomainHint: string | undefined,
  providerIdpHint: string | undefined,
  providerPrompt: string | undefined,
  fallbackDomainHint: string,
): RedirectRequest {
  const resolvedAuthority = providerAuthority ?? authority ?? 'https://login.microsoftonline.com/common'
  const resolvedDomainHint = normalizeHint(providerDomainHint, fallbackDomainHint)

  return {
    ...apiLoginRequest,
    authority: resolvedAuthority,
    // First-class hint field is the most reliable way to pass HRD hints through MSAL.
    domainHint: resolvedDomainHint,
    // In provider-specific flows force an interactive challenge by default.
    prompt: resolvePrompt(providerPrompt, 'login'),
    // Keep explicit query params for CIAM/B2C providers that key on raw query fields.
    extraQueryParameters: buildProviderExtraQueryParameters(
      providerDomainHint,
      providerIdpHint,
      fallbackDomainHint,
    ),
  }
}

// Normalize redirect URI for localhost: Azure AD requires 'http://localhost:5250', not custom subdomains
const getRedirectUri = () => {
  const origin = window.location.origin
  if (origin.includes('localhost')) {
    return 'http://localhost:5250'
  }
  return origin
}

const redirectUri = getRedirectUri()

if (!clientId) {
  console.warn(
    'VITE_ENTRA_CLIENT_ID is not set. Authentication will not work. ' +
    'Create a .env file with your Entra ID configuration.'
  )
}

const msalConfig: Configuration = {
  auth: {
    clientId: clientId ?? 'PLACEHOLDER_CLIENT_ID',
    authority: authority ?? 'https://login.microsoftonline.com/common',
    redirectUri: redirectUri,
    postLogoutRedirectUri: redirectUri,
  },
  cache: {
    cacheLocation: 'localStorage',
  },
}

/**
 * Scopes requested when acquiring tokens for the Fleet API.
 * The API scope must match the "Expose an API" scope in your Entra ID app registration.
 */
export const apiLoginRequest: RedirectRequest = {
  scopes: apiScope ? [apiScope] : ['User.Read'],
  prompt: 'select_account',
}

/** Login request that hints the user should sign in via Google */
export const googleLoginRequest: RedirectRequest = {
  ...createProviderLoginRequest(
    googleAuthority,
    googleDomainHint,
    googleIdpHint,
    googlePrompt,
    'google.com',
  ),
}

/** Login request that hints the user should sign in via GitHub */
export const githubLoginRequest: RedirectRequest = {
  ...createProviderLoginRequest(
    githubAuthority,
    githubDomainHint,
    githubIdpHint,
    githubPrompt,
    'github.com',
  ),
}

export function getLoginRequest(provider: AuthProvider = 'microsoft'): RedirectRequest {
  if (provider === 'google') {
    return googleLoginRequest
  }

  if (provider === 'github') {
    return githubLoginRequest
  }

  return apiLoginRequest
}

export const msalInstance = new PublicClientApplication(msalConfig)
