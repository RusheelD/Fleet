import { LogLevel, PublicClientApplication, type Configuration, type RedirectRequest } from '@azure/msal-browser'
import { describeEntraConfigError, getMsalLogLevel, isPlaceholderValue, resolveKnownAuthorities, resolveProviderDomainHint, resolveRedirectUri } from './msalConfigUtils'

/**
 * MSAL configuration for Microsoft Entra ID authentication.
 *
 * Replace the placeholder values with your Entra ID app registration details:
 * - VITE_ENTRA_CLIENT_ID: The Application (client) ID of your SPA app registration
 * - VITE_ENTRA_AUTHORITY: https://{tenant-name}.ciamlogin.com/
 * - VITE_ENTRA_API_SCOPE: api://{apiClientId}/access_as_user
 * - VITE_ENTRA_KNOWN_AUTHORITIES: {tenant-name}.ciamlogin.com
 * - VITE_ENTRA_REDIRECT_URI: The exact redirect URI registered for the SPA app
 * - VITE_ENTRA_GOOGLE_AUTHORITY / VITE_ENTRA_MICROSOFT_AUTHORITY: Optional provider-specific authorities
 * - VITE_ENTRA_MICROSOFT_DOMAIN_HINT: Optional Microsoft provider domain hint
 */

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined
const authority = import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE as string | undefined
const configuredRedirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI as string | undefined
const configuredKnownAuthorities = import.meta.env.VITE_ENTRA_KNOWN_AUTHORITIES as string | undefined
const googleAuthority = import.meta.env.VITE_ENTRA_GOOGLE_AUTHORITY as string | undefined
const microsoftAuthority = import.meta.env.VITE_ENTRA_MICROSOFT_AUTHORITY as string | undefined
const microsoftDomainHint = import.meta.env.VITE_ENTRA_MICROSOFT_DOMAIN_HINT as string | undefined
const runtimeOrigin = typeof window !== 'undefined' ? window.location.origin : undefined
const runtimeHostname = typeof window !== 'undefined' ? window.location.hostname : undefined
const FALLBACK_CLIENT_ID = '00000000-0000-0000-0000-000000000000'
const FALLBACK_AUTHORITY = 'https://login.microsoftonline.com/common'

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
const knownAuthorities = resolveKnownAuthorities(
  configuredKnownAuthorities,
  resolvedAuthority,
  googleAuthority,
  microsoftAuthority,
)
export const redirectUri = resolveRedirectUri(configuredRedirectUri, runtimeOrigin)
const resolvedMicrosoftDomainHint = resolveProviderDomainHint(microsoftDomainHint, 'login.live.com')

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

export type AuthLoginProvider = 'email' | 'google' | 'microsoft'

/** Interactive email/local-account sign-in should always show the CIAM prompt. */
export const emailLoginRequest: RedirectRequest = {
  ...apiLoginRequest,
  prompt: 'login',
}

/** External ID supports an explicit sign-up prompt for local/email accounts. */
export const emailSignUpRequest: RedirectRequest = {
  ...apiLoginRequest,
  prompt: 'create',
}

/** Optional Google sign-in request that can use a dedicated authority when configured. */
export const googleLoginRequest: RedirectRequest = {
  ...apiLoginRequest,
  authority: resolveOptionalAuthority(googleAuthority, resolvedAuthority),
  extraQueryParameters: {
    domain_hint: 'google.com',
  },
}

/** Optional Microsoft account sign-in request. */
export const microsoftLoginRequest: RedirectRequest = {
  ...apiLoginRequest,
  prompt: 'login',
  authority: resolveOptionalAuthority(microsoftAuthority, resolvedAuthority),
  extraQueryParameters: {
    domain_hint: resolvedMicrosoftDomainHint,
  },
}

export const msalInstance = new PublicClientApplication(msalConfig)
