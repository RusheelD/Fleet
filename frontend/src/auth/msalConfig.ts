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
const githubAuthority = import.meta.env.VITE_ENTRA_GITHUB_AUTHORITY as string | undefined
const githubDomainHint = import.meta.env.VITE_ENTRA_GITHUB_DOMAIN_HINT as string | undefined

function withDomainHint(domainHint: string | undefined, fallback: string): RedirectRequest['extraQueryParameters'] {
  const normalized = domainHint?.trim()
  return {
    domain_hint: normalized && normalized.length > 0 ? normalized : fallback,
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
}

/** Login request that hints the user should sign in via Google */
export const googleLoginRequest: RedirectRequest = {
  ...apiLoginRequest,
  authority: googleAuthority ?? authority ?? 'https://login.microsoftonline.com/common',
  // Issuer acceleration for External ID customer flows.
  extraQueryParameters: withDomainHint(googleDomainHint, 'Google'),
}

/** Login request that hints the user should sign in via GitHub */
export const githubLoginRequest: RedirectRequest = {
  ...apiLoginRequest,
  authority: githubAuthority ?? authority ?? 'https://login.microsoftonline.com/common',
  // For GitHub this may vary by provider type (built-in vs custom OIDC).
  extraQueryParameters: withDomainHint(githubDomainHint, 'github.com'),
}

export const msalInstance = new PublicClientApplication(msalConfig)
