import { get, put, post, del } from './proxy'
import type {
  UserSettings,
  UserProfile,
  UserPreferences,
  LinkedAccount,
  MemoryEntry,
  PromptSkill,
  PromptSkillTemplate,
  GitHubRepo,
  McpServer,
  McpServerTemplate,
  McpServerValidationResult,
  SystemMcpServer,
} from '../models'

export function getUserSettings(): Promise<UserSettings> {
  return get<UserSettings>('/api/user/settings')
}

export function updateProfile(profile: Partial<UserProfile>): Promise<UserProfile> {
  return put<UserProfile>('/api/user/profile', profile)
}

export function updatePreferences(preferences: UserPreferences): Promise<UserPreferences> {
  return put<UserPreferences>('/api/user/preferences', preferences)
}

export interface UpsertMemoryEntryRequest {
  name: string
  description: string
  type: 'user' | 'feedback' | 'project' | 'reference' | string
  content: string
  alwaysInclude: boolean
}

export interface UpsertPromptSkillRequest {
  name: string
  description: string
  whenToUse: string
  content: string
  enabled: boolean
}

export function getUserMemories(): Promise<MemoryEntry[]> {
  return get<MemoryEntry[]>('/api/user/memories')
}

export function createUserMemory(data: UpsertMemoryEntryRequest): Promise<MemoryEntry> {
  return post<MemoryEntry>('/api/user/memories', data)
}

export function updateUserMemory(id: number, data: UpsertMemoryEntryRequest): Promise<MemoryEntry> {
  return put<MemoryEntry>(`/api/user/memories/${id}`, data)
}

export function deleteUserMemory(id: number): Promise<void> {
  return del<void>(`/api/user/memories/${id}`)
}

export function getProjectMemories(projectId: string): Promise<MemoryEntry[]> {
  return get<MemoryEntry[]>(`/api/projects/${projectId}/memories`)
}

export function createProjectMemory(projectId: string, data: UpsertMemoryEntryRequest): Promise<MemoryEntry> {
  return post<MemoryEntry>(`/api/projects/${projectId}/memories`, data)
}

export function updateProjectMemory(projectId: string, id: number, data: UpsertMemoryEntryRequest): Promise<MemoryEntry> {
  return put<MemoryEntry>(`/api/projects/${projectId}/memories/${id}`, data)
}

export function deleteProjectMemory(projectId: string, id: number): Promise<void> {
  return del<void>(`/api/projects/${projectId}/memories/${id}`)
}

export function getSkillTemplates(): Promise<PromptSkillTemplate[]> {
  return get<PromptSkillTemplate[]>('/api/skill-templates')
}

export function getUserSkills(): Promise<PromptSkill[]> {
  return get<PromptSkill[]>('/api/user/skills')
}

export function createUserSkill(data: UpsertPromptSkillRequest): Promise<PromptSkill> {
  return post<PromptSkill>('/api/user/skills', data)
}

export function updateUserSkill(id: number, data: UpsertPromptSkillRequest): Promise<PromptSkill> {
  return put<PromptSkill>(`/api/user/skills/${id}`, data)
}

export function deleteUserSkill(id: number): Promise<void> {
  return del<void>(`/api/user/skills/${id}`)
}

export function getProjectSkills(projectId: string): Promise<PromptSkill[]> {
  return get<PromptSkill[]>(`/api/projects/${projectId}/skills`)
}

export function createProjectSkill(projectId: string, data: UpsertPromptSkillRequest): Promise<PromptSkill> {
  return post<PromptSkill>(`/api/projects/${projectId}/skills`, data)
}

export function updateProjectSkill(projectId: string, id: number, data: UpsertPromptSkillRequest): Promise<PromptSkill> {
  return put<PromptSkill>(`/api/projects/${projectId}/skills/${id}`, data)
}

export function deleteProjectSkill(projectId: string, id: number): Promise<void> {
  return del<void>(`/api/projects/${projectId}/skills/${id}`)
}

export function getGitHubOAuthState(): Promise<{ state: string }> {
  return get<{ state: string }>('/api/connections/github/state')
}

export function getGitHubOAuthClientId(): Promise<{ clientId: string }> {
  return get<{ clientId: string }>('/api/connections/github/client-id')
}

export function linkGitHub(code: string, redirectUri: string, state: string): Promise<LinkedAccount> {
  return post<LinkedAccount>('/api/connections/github', { code, redirectUri, state })
}

export function unlinkGitHub(accountId?: number): Promise<void> {
  if (typeof accountId === 'number') {
    return del<void>(`/api/connections/github/${accountId}`)
  }
  return del<void>('/api/connections/github')
}

export function setPrimaryGitHubAccount(accountId: number): Promise<LinkedAccount> {
  return put<LinkedAccount>(`/api/connections/github/${accountId}/primary`)
}

export function getGitHubRepos(accountId?: number): Promise<GitHubRepo[]> {
  const query = typeof accountId === 'number'
    ? `?accountId=${encodeURIComponent(accountId)}`
    : ''
  return get<GitHubRepo[]>(`/api/connections/github/repos${query}`)
}

export interface CreateGitHubRepoRequest {
  name: string
  description?: string
  private: boolean
  accountId?: number
}

export function createGitHubRepo(data: CreateGitHubRepoRequest): Promise<GitHubRepo> {
  return post<GitHubRepo>('/api/connections/github/repos', data)
}

export interface UpsertMcpServerVariableRequest {
  name: string
  value?: string | null
  isSecret: boolean
  preserveExistingValue?: boolean
}

export interface UpsertMcpServerRequest {
  name: string
  description?: string
  transportType: 'stdio' | 'http' | string
  command?: string
  arguments?: string[]
  workingDirectory?: string
  endpoint?: string
  builtInTemplateKey?: string
  enabled: boolean
  environmentVariables?: UpsertMcpServerVariableRequest[]
  headers?: UpsertMcpServerVariableRequest[]
}

export function getMcpServers(): Promise<McpServer[]> {
  return get<McpServer[]>('/api/mcp-servers')
}

export function getMcpServerTemplates(): Promise<McpServerTemplate[]> {
  return get<McpServerTemplate[]>('/api/mcp-servers/templates')
}

export function createMcpServer(data: UpsertMcpServerRequest): Promise<McpServer> {
  return post<McpServer>('/api/mcp-servers', data)
}

export function updateMcpServer(id: number, data: UpsertMcpServerRequest): Promise<McpServer> {
  return put<McpServer>(`/api/mcp-servers/${id}`, data)
}

export function deleteMcpServer(id: number): Promise<void> {
  return del<void>(`/api/mcp-servers/${id}`)
}

export function validateMcpServer(id: number): Promise<McpServerValidationResult> {
  return post<McpServerValidationResult>(`/api/mcp-servers/${id}/validate`, {})
}

export function getSystemMcpServers(): Promise<SystemMcpServer[]> {
  return get<SystemMcpServer[]>('/api/mcp-servers/system')
}
