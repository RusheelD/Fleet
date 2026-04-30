export interface ProjectData {
  id: string
  ownerId: string
  title: string
  slug: string
  description: string
  repo: string
  workItems: { total: number; active: number; resolved: number }
  agents: { total: number; running: number }
  lastActivity: string
  branchPattern: string
  commitAuthorMode: string
  commitAuthorName?: string | null
  commitAuthorEmail?: string | null
}

export interface ProjectBranch {
  name: string
  isDefault: boolean
  isProtected: boolean
  canUseForDynamicIteration: boolean
  dynamicIterationBlockedReason?: string | null
}

export interface SlugCheckResult {
  slug: string
  available: boolean
}
