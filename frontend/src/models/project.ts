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
}

export interface SlugCheckResult {
  slug: string
  available: boolean
}
