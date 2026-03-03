import { createContext, useContext } from 'react'
import type { UserPreferences } from '../models'

export interface PreferencesContextValue {
  /** Current user preferences (null before first load completes) */
  preferences: UserPreferences | null
  /** Update one or more preference keys — persists to backend and updates context */
  updatePreference: (key: keyof UserPreferences, value: boolean) => void
}

export const PreferencesContext = createContext<PreferencesContextValue | undefined>(undefined)

export function usePreferences(): PreferencesContextValue {
  const ctx = useContext(PreferencesContext)
  if (!ctx) {
    throw new Error('usePreferences must be used within a PreferencesProvider')
  }
  return ctx
}
