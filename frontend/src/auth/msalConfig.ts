import { PublicClientApplication, type Configuration, type RedirectRequest } from '@azure/msal-browser'

/**
 * MSAL configuration for Microsoft Entra ID authentication.
 *
 * Replace the placeholder values with your Entra ID app registration details:
 * - VITE_ENTRA_CLIENT_ID: The Application (client) ID of your SPA app registration
 * - VITE_ENTRA_AUTHORITY: https://{tenant-name}.ciamlogin.com/{tenant-id}
 * - VITE_ENTRA_API_SCOPE: api://{apiClientId}/access_as_user
 * - VITE_ENTRA_KNOWN_AUTHORITIES: {tenant-name}.ciamlogin.com
 * - VITE_ENTRA_REDIRECT_URI: The exact redirect URI registered for the SPA app
 */

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined
const authority = import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE as string | undefined
const configuredRedirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI as string | undefined
const knownAuthorities = (import.meta.env.VITE_ENTRA_KNOWN_AUTHORITIES as string | undefined)
  ?.split(',')
  .map(value => value.trim())
  .filter(value => value.length > 0)
const googleAuthority = import.meta.env.VITE_ENTRA_GOOGLE_AUTHORITY as string | undefined
const googleDomainHint = import.meta.env.VITE_ENTRA_GOOGLE_DOMAIN_HINT as string | undefined
const googleIdpHint = import.meta.env.VITE_ENTRA_GOOGLE_IDP_HINT as string | undefined
const githubAuthority = import.meta.env.VITE_ENTRA_GITHUB_AUTHORITY as string | undefined
const githubDomainHint = import.meta.env.VITE_ENTRA_GITHUB_DOMAIN_HINT as string | undefined
const githubIdpHint = import.meta.env.VITE_ENTRA_GITHUB_IDP_HINT as string | undefined

function normalizeHint(value: string | undefined, fallback: string): string {
  const normalized = value?.trim()
  return normalized && normalized.length > 0 ? normalized : fallback
}

function withProviderHints(
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

function ensureRootRedirectUriHasTrailingSlash(value: string): string {
  try {
    const parsed = new URL(value)
    const isOriginOnly = parsed.pathname === '/' && !parsed.search && !parsed.hash
    return isOriginOnly && !value.endsWith('/') ? `${value}/` : value
  } catch {
    return value
  }
}

function resolveRedirectUri(): string {
  const explicit = configuredRedirectUri?.trim()
  if (explicit) {
    return ensureRootRedirectUriHasTrailingSlash(explicit)
  }

  const origin = window.location.origin
  if (origin.includes('localhost')) {
    return 'http://localhost:5250/'
  }

  return ensureRootRedirectUriHasTrailingSlash(origin)
}

export const redirectUri = resolveRedirectUri()

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
    knownAuthorities: knownAuthorities && knownAuthorities.length > 0 ? knownAuthorities : undefined,
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

/** Login request that hints the user should sign in via Google */
export const googleLoginRequest: RedirectRequest = {
  ...apiLoginRequest,
  authority: googleAuthority ?? authority ?? 'https://login.microsoftonline.com/common',
  extraQueryParameters: withProviderHints(
    googleDomainHint,
    googleIdpHint,
    'Google',
  ),
}

/** Login request that hints the user should sign in via GitHub */
export const githubLoginRequest: RedirectRequest = {
  ...apiLoginRequest,
  authority: githubAuthority ?? authority ?? 'https://login.microsoftonline.com/common',
  extraQueryParameters: withProviderHints(
    githubDomainHint,
    githubIdpHint,
    'github.com',
  ),
}

export const msalInstance = new PublicClientApplication(msalConfig)
