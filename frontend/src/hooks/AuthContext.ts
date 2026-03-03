import { createContext } from 'react'
import type { UserProfile } from '../models'

export interface AuthContextValue {
  isAuthenticated: boolean
  isLoading: boolean
  user: UserProfile | null
  /** Update the cached user profile (e.g. after saving profile edits) */
  updateUser: (profile: UserProfile) => void
  login: (provider?: 'microsoft' | 'google' | 'github') => Promise<void>
  logout: () => void
  getAccessToken: () => Promise<string | undefined>
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
