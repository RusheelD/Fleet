import { LogLevel, PublicClientApplication, type Configuration, type RedirectRequest } from '@azure/msal-browser'
import { describeEntraConfigError, getMsalLogLevel, isPlaceholderValue, resolveKnownAuthorities, resolveRedirectUri } from './msalConfigUtils'

/**
 * MSAL configuration for Microsoft Entra ID authentication.
 *
 * Replace the placeholder values with your Entra ID app registration details:
 * - VITE_ENTRA_CLIENT_ID: The Application (client) ID of your SPA app registration
 * - VITE_ENTRA_AUTHORITY: https://{tenant-name}.ciamlogin.com/
 * - VITE_ENTRA_API_SCOPE: api://{apiClientId}/access_as_user
 * - VITE_ENTRA_KNOWN_AUTHORITIES: {tenant-name}.ciamlogin.com
 * - VITE_ENTRA_REDIRECT_URI: The exact redirect URI registered for the SPA app
 */

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined
const authority = import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE as string | undefined
const configuredRedirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI as string | undefined
const configuredKnownAuthorities = import.meta.env.VITE_ENTRA_KNOWN_AUTHORITIES as string | undefined
const googleAuthority = import.meta.env.VITE_ENTRA_GOOGLE_AUTHORITY as string | undefined
const googleDomainHint = import.meta.env.VITE_ENTRA_GOOGLE_DOMAIN_HINT as string | undefined
const googleIdpHint = import.meta.env.VITE_ENTRA_GOOGLE_IDP_HINT as string | undefined
const runtimeOrigin = typeof window !== 'undefined' ? window.location.origin : undefined
const runtimeHostname = typeof window !== 'undefined' ? window.location.hostname : undefined
const FALLBACK_CLIENT_ID = '00000000-0000-0000-0000-000000000000'
const FALLBACK_AUTHORITY = 'https://login.microsoftonline.com/common'

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

function resolveOptionalAuthority(value: string | undefined, fallback: string): string {
  const normalized = value?.trim()
  if (!normalized || isPlaceholderValue(normalized)) {
    return fallback
  }

  return normalized
}

export const authConfigError = describeEntraConfigError({
  clientId,
  authority,
  apiScope,
  redirectUri: configuredRedirectUri,
  knownAuthorities: configuredKnownAuthorities,
})

export const isAuthConfigured = authConfigError === null
const resolvedAuthority = resolveOptionalAuthority(authority, FALLBACK_AUTHORITY)
const knownAuthorities = resolveKnownAuthorities(configuredKnownAuthorities, resolvedAuthority)
export const redirectUri = resolveRedirectUri(configuredRedirectUri, runtimeOrigin)

if (authConfigError) {
  console.error(authConfigError)
} else if (!clientId) {
  console.warn(
    'VITE_ENTRA_CLIENT_ID is not set. Authentication will not work. ' +
    'Create a .env file with your Entra ID configuration.'
  )
}

const msalConfig: Configuration = {
  auth: {
    clientId: clientId ?? FALLBACK_CLIENT_ID,
    authority: resolvedAuthority,
    knownAuthorities,
    redirectUri,
    postLogoutRedirectUri: redirectUri,
  },
  cache: {
    cacheLocation: 'localStorage',
  },
  system: {
    loggerOptions: {
      logLevel: getMsalLogLevel(runtimeHostname),
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) {
          return
        }

        switch (level) {
          case LogLevel.Error:
            console.error(message)
            return
          case LogLevel.Warning:
            console.warn(message)
            return
          default:
            console.log(message)
        }
      },
    },
  },
}

/**
 * Scopes requested when acquiring tokens for the Fleet API.
 * The API scope must match the "Expose an API" scope in your Entra ID app registration.
 * MSAL.js automatically adds the standard OpenID Connect scopes to login requests.
 */
export const apiLoginRequest: RedirectRequest = {
  scopes: apiScope ? [apiScope] : [],
}

/** Login request that hints the user should sign in via Google */
export const googleLoginRequest: RedirectRequest = {
  ...apiLoginRequest,
  authority: resolveOptionalAuthority(googleAuthority, resolvedAuthority),
  extraQueryParameters: withProviderHints(
    googleDomainHint,
    googleIdpHint,
    'Google',
  ),
}

export const msalInstance = new PublicClientApplication(msalConfig)
