import { LogLevel } from '@azure/msal-browser'
import { describe, expect, it, vi } from 'vitest'
import {
  describeEntraConfigError,
  getMsalLogLevel,
  isPlaceholderValue,
  normalizeRedirectUri,
  resolveKnownAuthorities,
  resolveRedirectUri,
} from './msalConfigUtils'

describe('msalConfigUtils', () => {
  it('normalizes root redirect URIs to the origin without a trailing slash', () => {
    expect(normalizeRedirectUri('https://app.fleet-ai.dev/')).toBe('https://app.fleet-ai.dev')
    expect(normalizeRedirectUri('http://localhost:5250/')).toBe('http://localhost:5250')
  })

  it('prefers the runtime origin when a configured root redirect URI points at a different host', () => {
    const warn = vi.fn()

    const redirectUri = resolveRedirectUri(
      'https://app.fleet-ai.dev/',
      'https://app-dev.fleet-ai.dev',
      { warn },
    )

    expect(redirectUri).toBe('https://app-dev.fleet-ai.dev')
    expect(warn).toHaveBeenCalledOnce()
  })

  it('silently prefers the runtime origin on production host mismatches', () => {
    const warn = vi.fn()

    const redirectUri = resolveRedirectUri(
      'https://app-dev.fleet-ai.dev/',
      'https://app.fleet-ai.dev',
      { warn },
    )

    expect(redirectUri).toBe('https://app.fleet-ai.dev')
    expect(warn).not.toHaveBeenCalled()
  })

  it('preserves an explicit non-root redirect path', () => {
    const redirectUri = resolveRedirectUri(
      'https://app.fleet-ai.dev/auth/callback',
      'https://app.fleet-ai.dev',
    )

    expect(redirectUri).toBe('https://app.fleet-ai.dev/auth/callback')
  })

  it('derives known authorities from the configured authority when none are provided', () => {
    expect(resolveKnownAuthorities(undefined, 'https://fleetaidev.ciamlogin.com/')).toEqual([
      'fleetaidev.ciamlogin.com',
    ])
  })

  it('ignores placeholder known authorities and falls back to the authority host', () => {
    expect(resolveKnownAuthorities('YOUR_ENTRA_TENANT_NAME.ciamlogin.com', 'https://fleetaidev.ciamlogin.com/')).toEqual([
      'fleetaidev.ciamlogin.com',
    ])
  })

  it('includes optional provider authority hosts', () => {
    expect(resolveKnownAuthorities(
      'fleetaidev.ciamlogin.com',
      'https://fleetaidev.ciamlogin.com/',
      'https://fleet-google.ciamlogin.com/',
      'https://fleet-microsoft.ciamlogin.com/',
    )).toEqual([
      'fleetaidev.ciamlogin.com',
      'fleet-google.ciamlogin.com',
      'fleet-microsoft.ciamlogin.com',
    ])
  })

  it('detects placeholder config values', () => {
    expect(isPlaceholderValue('YOUR_ENTRA_TENANT_ID')).toBe(true)
    expect(isPlaceholderValue('https://your-tenant-name.ciamlogin.com/your-tenant-id')).toBe(true)
    expect(isPlaceholderValue('https://app.fleet-ai.dev')).toBe(false)
  })

  it('reports a clear auth config error for placeholder CIAM values', () => {
    expect(describeEntraConfigError({
      clientId: 'f14181d0-8350-4c48-83f5-ac9a486a35b4',
      authority: 'https://YOUR_ENTRA_TENANT_NAME.ciamlogin.com/YOUR_ENTRA_TENANT_ID',
      apiScope: 'api://73df32d0-82b8-4eba-bc4a-6df3d1f0a281/access_as_user',
      redirectUri: 'https://app.fleet-ai.dev',
      knownAuthorities: 'YOUR_ENTRA_TENANT_NAME.ciamlogin.com',
    })).toContain('VITE_ENTRA_AUTHORITY')
  })

  it('accepts a complete CIAM configuration', () => {
    expect(describeEntraConfigError({
      clientId: 'f14181d0-8350-4c48-83f5-ac9a486a35b4',
      authority: 'https://fleetaidev.ciamlogin.com/',
      apiScope: 'api://73df32d0-82b8-4eba-bc4a-6df3d1f0a281/access_as_user',
      redirectUri: 'https://app.fleet-ai.dev',
      knownAuthorities: 'fleetaidev.ciamlogin.com',
    })).toBeNull()
  })

  it('uses warning logging for local and non-production app hosts', () => {
    expect(getMsalLogLevel('localhost')).toBe(LogLevel.Warning)
    expect(getMsalLogLevel('app-dev.fleet-ai.dev')).toBe(LogLevel.Warning)
    expect(getMsalLogLevel('app-staging.fleet-ai.dev')).toBe(LogLevel.Warning)
    expect(getMsalLogLevel('app.fleet-ai.dev')).toBe(LogLevel.Error)
  })
})
