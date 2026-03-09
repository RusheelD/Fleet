export interface GitHubRepo {
  fullName: string
  name: string
  owner: string
  description?: string
  private: boolean
  htmlUrl: string
  linkedAccountId?: number
  linkedAccountLogin?: string
}
