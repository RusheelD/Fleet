export interface UserProfile {
  displayName: string
  email: string
  bio: string
  location: string
  avatarUrl: string
  role?: 'free' | 'basic' | 'pro' | 'unlimited' | string
}

export interface LinkedAccount {
  id: number
  provider: string
  connectedAs?: string
  externalUserId?: string
  connectedAt?: string
  isPrimary?: boolean
}

export interface UserPreferences {
  agentCompletedNotification: boolean
  prOpenedNotification: boolean
  agentErrorsNotification: boolean
  workItemUpdatesNotification: boolean
  darkMode: boolean
  compactMode: boolean
  sidebarCollapsed: boolean
}

export interface UserSettings {
  profile: UserProfile
  connections: LinkedAccount[]
  preferences: UserPreferences
}
