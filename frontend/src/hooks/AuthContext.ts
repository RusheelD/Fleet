import { createContext } from 'react'
import type { UserProfile } from '../models'

export interface AuthContextValue {
  isAuthenticated: boolean
  isLoading: boolean
  isAuthConfigured: boolean
  authConfigError: string | null
  user: UserProfile | null
  /** Update the cached user profile (e.g. after saving profile edits) */
  updateUser: (profile: UserProfile) => void
  login: (provider?: 'email' | 'google') => Promise<void>
  signUp: (provider?: 'email' | 'google') => Promise<void>
  logout: () => void
  getAccessToken: () => Promise<string | undefined>
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
