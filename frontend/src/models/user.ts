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

export interface McpServerVariable {
  name: string
  value?: string | null
  isSecret: boolean
  hasValue: boolean
}

export interface McpServerTemplateField {
  name: string
  description: string
  isSecret: boolean
  required: boolean
  defaultValue?: string | null
}

export interface McpServerTemplate {
  key: string
  name: string
  description: string
  transportType: 'stdio' | 'http' | string
  command?: string | null
  arguments: string[]
  workingDirectory?: string | null
  endpoint?: string | null
  environmentVariables: McpServerTemplateField[]
  headers: McpServerTemplateField[]
  notes: string[]
}

export interface McpServer {
  id: number
  name: string
  description: string
  transportType: 'stdio' | 'http' | string
  command?: string | null
  arguments: string[]
  workingDirectory?: string | null
  endpoint?: string | null
  builtInTemplateKey?: string | null
  enabled: boolean
  environmentVariables: McpServerVariable[]
  headers: McpServerVariable[]
  createdAtUtc: string
  updatedAtUtc: string
  lastValidatedAtUtc?: string | null
  lastValidationError?: string | null
  lastToolCount: number
  discoveredTools: string[]
}

export interface McpServerValidationResult {
  success: boolean
  error?: string | null
  toolCount: number
  toolNames: string[]
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
