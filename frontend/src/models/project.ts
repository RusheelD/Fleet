export interface ProjectData {
  id: string
  title: string
  description: string
  repo: string
  workItems: { total: number; active: number; resolved: number }
  agents: { total: number; running: number }
  lastActivity: string
}
