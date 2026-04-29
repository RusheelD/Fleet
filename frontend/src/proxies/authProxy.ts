import { del, get, post } from './proxy'
import type { LoginIdentity, UserProfile } from '../models'
import type { AuthLoginProvider } from '../auth'

export interface LoginProviderLinkState {
  state: string
}

export function getLoginIdentities(): Promise<LoginIdentity[]> {
  return get<LoginIdentity[]>('/api/auth/login-identities')
}

export function createLoginProviderLinkState(provider: AuthLoginProvider): Promise<LoginProviderLinkState> {
  return post<LoginProviderLinkState>('/api/auth/login-identities/link-state', { provider })
}

export function completeLoginProviderLink(state: string): Promise<UserProfile> {
  return post<UserProfile>('/api/auth/login-identities/complete-link', { state })
}

export function deleteLoginIdentity(identityId: number): Promise<void> {
  return del<void>(`/api/auth/login-identities/${identityId}`)
}
