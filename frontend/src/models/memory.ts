export type MemoryType = 'user' | 'feedback' | 'project' | 'reference' | string

export interface MemoryEntry {
  id: number
  name: string
  description: string
  type: MemoryType
  content: string
  alwaysInclude: boolean
  scope: 'personal' | 'project' | string
  projectId?: string | null
  createdAtUtc: string
  updatedAtUtc: string
  isStale: boolean
  stalenessMessage?: string | null
}
