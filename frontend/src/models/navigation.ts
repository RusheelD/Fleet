import type { ReactElement } from 'react'

export interface NavItemConfig {
  icon: ReactElement
  label: string
  path: string
  badge?: string | number | null
  /** If true, match path exactly (not prefix) */
  exact?: boolean
}
