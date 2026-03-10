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
const knownAuthoritiesEnv = import.meta.env.VITE_ENTRA_KNOWN_AUTHORITIES as string | undefined
const googleAuthority = import.meta.env.VITE_ENTRA_GOOGLE_AUTHORITY as string | undefined
const githubAuthority = import.meta.env.VITE_ENTRA_GITHUB_AUTHORITY as string | undefined

export type AuthProvider = 'microsoft' | 'google' | 'github'

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

  for (const authorityValue of [authority, googleAuthority, githubAuthority]) {
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
  providerAuthority: string | undefined,
): RedirectRequest {
  const resolvedAuthority = providerAuthority ?? authority ?? 'https://login.microsoftonline.com/common'

  return {
    ...apiLoginRequest,
    authority: resolvedAuthority,
    // Provider-specific flow should challenge directly without account picker hints.
    prompt: 'login',
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
const knownAuthorities = buildKnownAuthorities()

if (!clientId) {
  console.warn(
    'VITE_ENTRA_CLIENT_ID is not set. Authentication will not work. ' +
    'Create a .env file with your Entra ID configuration.'
  )
}

if (!normalizeOptional(googleAuthority)) {
  console.warn(
    'VITE_ENTRA_GOOGLE_AUTHORITY is not set. For direct Google sign-in, configure a Google-only flow authority.'
  )
}

if (!normalizeOptional(githubAuthority)) {
  console.warn(
    'VITE_ENTRA_GITHUB_AUTHORITY is not set. For direct GitHub sign-in, configure a GitHub-only flow authority.'
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
}

/** Login request that targets Google-specific authority */
export const googleLoginRequest: RedirectRequest = {
  ...createProviderLoginRequest(googleAuthority),
}

/** Login request that targets GitHub-specific authority */
export const githubLoginRequest: RedirectRequest = {
  ...createProviderLoginRequest(githubAuthority),
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
