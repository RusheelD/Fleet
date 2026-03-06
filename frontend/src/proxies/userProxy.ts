import { get, put, post, del } from './'
import type { UserSettings, UserProfile, UserPreferences, LinkedAccount, GitHubRepo } from '../models'

export function getUserSettings(): Promise<UserSettings> {
  return get<UserSettings>('/api/user/settings')
}

export function updateProfile(profile: Partial<UserProfile>): Promise<UserProfile> {
  return put<UserProfile>('/api/user/profile', profile)
}

export function updatePreferences(preferences: UserPreferences): Promise<UserPreferences> {
  return put<UserPreferences>('/api/user/preferences', preferences)
}

export function getGitHubOAuthState(): Promise<{ state: string }> {
  return get<{ state: string }>('/api/connections/github/state')
}

export function linkGitHub(code: string, redirectUri: string, state: string): Promise<LinkedAccount> {
  return post<LinkedAccount>('/api/connections/github', { code, redirectUri, state })
}

export function unlinkGitHub(): Promise<void> {
  return del<void>('/api/connections/github')
}

export function getGitHubRepos(): Promise<GitHubRepo[]> {
  return get<GitHubRepo[]>('/api/connections/github/repos')
}
