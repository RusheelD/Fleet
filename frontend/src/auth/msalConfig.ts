import { PublicClientApplication, type Configuration, type RedirectRequest } from '@azure/msal-browser'

/**
 * MSAL configuration for Microsoft Entra ID authentication.
 *
 * Replace the placeholder values with your Entra ID app registration details:
 * - VITE_ENTRA_CLIENT_ID: The Application (client) ID of your SPA app registration
 * - VITE_ENTRA_AUTHORITY: https://{tenant}.ciamlogin.com/{tenantId}
 * - VITE_ENTRA_API_SCOPE: api://{apiClientId}/access_as_user
 */

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined
const authority = import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE as string | undefined
const knownAuthoritiesEnv = import.meta.env.VITE_ENTRA_KNOWN_AUTHORITIES as string | undefined
const redirectUriEnv = import.meta.env.VITE_ENTRA_REDIRECT_URI as string | undefined

export type AuthProvider = 'microsoft' | 'google' | 'github'

const providerDomainHints: Record<Exclude<AuthProvider, 'microsoft'>, string> = {
  google: 'google.com',
  github: 'github.com',
}

function normalizeOptional(value: string | undefined): string | undefined {
  const normalized = value?.trim()
  return normalized && normalized.length > 0 ? normalized : undefined
}

function parseAuthorityHost(authorityValue: string | undefined): string | undefined {
  const normalized = normalizeOptional(authorityValue)
  if (!normalized) {
    return undefined
  }

  try {
    return new URL(normalized).host
  } catch {
    return undefined
  }
}

function buildKnownAuthorities(): string[] {
  const knownAuthorities = new Set<string>()

  for (const token of (knownAuthoritiesEnv ?? '').split(',')) {
    const normalized = token.trim()
    if (normalized.length > 0) {
      knownAuthorities.add(normalized)
    }
  }

  for (const authorityValue of [authority]) {
    const host = parseAuthorityHost(authorityValue)
    if (!host) {
      continue
    }

    // Entra External ID / B2C authorities should be explicitly trusted by MSAL.
    if (host.endsWith('.ciamlogin.com') || host.endsWith('.b2clogin.com')) {
      knownAuthorities.add(host)
    }
  }

  return [...knownAuthorities]
}

function createProviderLoginRequest(
  provider: 'google' | 'github',
): RedirectRequest {
  const resolvedAuthority = authority ?? 'https://login.microsoftonline.com/common'
  const domainHint = providerDomainHints[provider]

  return {
    ...apiLoginRequest,
    authority: resolvedAuthority,
    // With a single app authority, provider hints accelerate home-realm discovery.
    domainHint,
    extraQueryParameters: { domain_hint: domainHint },
    prompt: 'login',
  }
}

// Normalize redirect URI for localhost: Azure AD requires 'http://localhost:5250', not custom subdomains
const getRedirectUri = () => {
  const configured = normalizeOptional(redirectUriEnv)
  if (configured) {
    return configured
  }

  const origin = window.location.origin
  if (origin.includes('localhost')) {
    return 'http://localhost:5250/'
  }

  // Use trailing slash for stricter redirect URI matching with Entra.
  return `${origin}/`
}

const redirectUri = getRedirectUri()
const knownAuthorities = buildKnownAuthorities()

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
    ...(knownAuthorities.length > 0 ? { knownAuthorities } : {}),
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
  authority: authority ?? 'https://login.microsoftonline.com/common',
}

export function getLoginRequest(provider: AuthProvider = 'microsoft'): RedirectRequest {
  if (provider === 'google') {
    return createProviderLoginRequest('google')
  }

  if (provider === 'github') {
    return createProviderLoginRequest('github')
  }

  return apiLoginRequest
}

export const msalInstance = new PublicClientApplication(msalConfig)
