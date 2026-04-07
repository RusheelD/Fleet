import { LogLevel } from '@azure/msal-browser'

const LOCALHOST_REDIRECT_URI = 'http://localhost:5250'
const ENTRA_HOST_SUFFIXES = ['.ciamlogin.com', '.b2clogin.com']
const PLACEHOLDER_TOKENS = [
  'your_',
  'your-',
  'your ',
  'placeholder',
  'example.com',
  'your-domain',
]

export interface EntraConfigValidationInput {
  clientId: string | undefined
  authority: string | undefined
  apiScope: string | undefined
  redirectUri: string | undefined
  knownAuthorities: string | undefined
}

function tryParseUrl(value: string): URL | null {
  try {
    return new URL(value)
  } catch {
    return null
  }
}

export function normalizeRedirectUri(value: string): string {
  const trimmed = value.trim()
  const parsed = tryParseUrl(trimmed)
  if (!parsed) {
    return trimmed
  }

  const isOriginOnly = parsed.pathname === '/' && !parsed.search && !parsed.hash
  return isOriginOnly ? parsed.origin : trimmed
}

export function isPlaceholderValue(value: string | undefined): boolean {
  const normalized = value?.trim().toLowerCase()
  if (!normalized) {
    return false
  }

  return PLACEHOLDER_TOKENS.some(token => normalized.includes(token))
}

function isSupportedEntraAuthorityHost(hostname: string): boolean {
  const normalizedHostname = hostname.trim().toLowerCase()
  return normalizedHostname === 'login.microsoftonline.com' ||
    ENTRA_HOST_SUFFIXES.some(suffix => normalizedHostname.endsWith(suffix))
}

function normalizeOptionalValue(value: string | undefined): string | undefined {
  const normalized = value?.trim()
  return normalized && normalized.length > 0 ? normalized : undefined
}

function shouldWarnRedirectUriMismatch(runtimeUrl: URL | null): boolean {
  const hostname = runtimeUrl?.hostname.trim().toLowerCase() ?? ''
  return hostname === 'localhost' ||
    hostname === '127.0.0.1' ||
    hostname.startsWith('app-dev.') ||
    hostname.startsWith('app-staging.')
}

export function resolveKnownAuthorities(
  configuredKnownAuthorities: string | undefined,
  authority: string | undefined,
): string[] | undefined {
  const configured = configuredKnownAuthorities
    ?.split(',')
    .map(value => value.trim())
    .filter(value => value.length > 0 && !isPlaceholderValue(value))

  if (configured && configured.length > 0) {
    return configured
  }

  const parsedAuthority = authority?.trim() ? tryParseUrl(authority.trim()) : null
  return parsedAuthority?.hostname ? [parsedAuthority.hostname] : undefined
}

export function resolveRedirectUri(
  configuredRedirectUri: string | undefined,
  runtimeOrigin: string | undefined,
  logger: Pick<Console, 'warn'> = console,
): string {
  const normalizedRuntimeOrigin = runtimeOrigin?.trim()
    ? normalizeRedirectUri(runtimeOrigin)
    : undefined
  const normalizedConfiguredRedirectUri = configuredRedirectUri?.trim()
    ? normalizeRedirectUri(configuredRedirectUri)
    : undefined

  if (normalizedConfiguredRedirectUri) {
    const configuredUrl = tryParseUrl(normalizedConfiguredRedirectUri)
    const runtimeUrl = normalizedRuntimeOrigin ? tryParseUrl(normalizedRuntimeOrigin) : null
    const configuredIsOriginOnly = configuredUrl !== null && normalizedConfiguredRedirectUri === configuredUrl.origin

    if (
      configuredIsOriginOnly &&
      runtimeUrl &&
      configuredUrl.origin !== runtimeUrl.origin
    ) {
      if (shouldWarnRedirectUriMismatch(runtimeUrl)) {
        logger.warn(
          `Configured VITE_ENTRA_REDIRECT_URI (${normalizedConfiguredRedirectUri}) does not match the current origin (${normalizedRuntimeOrigin}). ` +
          'Using the current origin to avoid Entra redirect URI mismatches.',
        )
      }

      return normalizedRuntimeOrigin ?? LOCALHOST_REDIRECT_URI
    }

    return normalizedConfiguredRedirectUri
  }

  return normalizedRuntimeOrigin ?? LOCALHOST_REDIRECT_URI
}

export function describeEntraConfigError({
  clientId,
  authority,
  apiScope,
  redirectUri,
  knownAuthorities,
}: EntraConfigValidationInput): string | null {
  const normalizedClientId = normalizeOptionalValue(clientId)
  if (!normalizedClientId || isPlaceholderValue(normalizedClientId)) {
    return 'Fleet sign-in is not configured. Set VITE_ENTRA_CLIENT_ID and rebuild the frontend.'
  }

  const normalizedAuthority = normalizeOptionalValue(authority)
  if (!normalizedAuthority || isPlaceholderValue(normalizedAuthority)) {
    return 'Fleet sign-in is not configured. Set VITE_ENTRA_AUTHORITY to your CIAM authority URL and rebuild the frontend.'
  }

  const parsedAuthority = tryParseUrl(normalizedAuthority)
  if (!parsedAuthority || parsedAuthority.protocol !== 'https:') {
    return 'VITE_ENTRA_AUTHORITY must be a valid HTTPS URL like https://contoso.ciamlogin.com/<tenant-id>.'
  }

  if (
    isPlaceholderValue(parsedAuthority.hostname) ||
    isPlaceholderValue(parsedAuthority.pathname)
  ) {
    return 'Fleet sign-in is not configured. Replace the placeholder tenant name and tenant ID in VITE_ENTRA_AUTHORITY.'
  }

  if (!isSupportedEntraAuthorityHost(parsedAuthority.hostname)) {
    return 'VITE_ENTRA_AUTHORITY must point to an Entra authority host such as *.ciamlogin.com.'
  }

  const normalizedApiScope = normalizeOptionalValue(apiScope)
  if (!normalizedApiScope || isPlaceholderValue(normalizedApiScope)) {
    return 'Fleet sign-in is not configured. Set VITE_ENTRA_API_SCOPE to your Fleet API scope and rebuild the frontend.'
  }

  const normalizedKnownAuthorities = normalizeOptionalValue(knownAuthorities)
  if (normalizedKnownAuthorities) {
    const invalidAuthority = normalizedKnownAuthorities
      .split(',')
      .map(value => value.trim())
      .find(value => isPlaceholderValue(value))

    if (invalidAuthority) {
      return 'Fleet sign-in is not configured. Replace the placeholder VITE_ENTRA_KNOWN_AUTHORITIES value and rebuild the frontend.'
    }
  }

  const normalizedRedirectUri = normalizeOptionalValue(redirectUri)
  if (normalizedRedirectUri) {
    if (isPlaceholderValue(normalizedRedirectUri)) {
      return 'Fleet sign-in is not configured. Replace the placeholder VITE_ENTRA_REDIRECT_URI value and rebuild the frontend.'
    }

    if (!tryParseUrl(normalizedRedirectUri)) {
      return 'VITE_ENTRA_REDIRECT_URI must be a valid absolute URL.'
    }
  }

  return null
}

export function getMsalLogLevel(hostname: string | undefined): LogLevel {
  const normalizedHostname = hostname?.trim().toLowerCase() ?? ''
  if (
    normalizedHostname === 'localhost' ||
    normalizedHostname === '127.0.0.1' ||
    normalizedHostname.startsWith('app-dev.') ||
    normalizedHostname.startsWith('app-staging.')
  ) {
    return LogLevel.Warning
  }

  return LogLevel.Error
}
