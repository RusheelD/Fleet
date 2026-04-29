import { createContext } from 'react'
import type { UserProfile } from '../models'
import type { AuthLoginProvider } from '../auth'

export interface AuthContextValue {
  isAuthenticated: boolean
  isLoading: boolean
  isAuthConfigured: boolean
  authConfigError: string | null
  user: UserProfile | null
  /** Update the cached user profile (e.g. after saving profile edits) */
  updateUser: (profile: UserProfile) => void
  login: (provider?: AuthLoginProvider) => Promise<void>
  signUp: (provider?: AuthLoginProvider) => Promise<void>
  linkLoginProvider: (provider: AuthLoginProvider) => Promise<void>
  logout: () => void
  getAccessToken: () => Promise<string | undefined>
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
