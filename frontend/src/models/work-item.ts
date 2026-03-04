export type WorkItemState = 'New' | 'Active' | 'In Progress' | 'In Progress (AI)' | 'Resolved' | 'Resolved (AI)' | 'Closed'

export interface WorkItem {
  workItemNumber: number
  title: string
  state: WorkItemState
  priority: 1 | 2 | 3 | 4
  difficulty: 1 | 2 | 3 | 4 | 5
  assignedTo: string
  tags: string[]
  isAI: boolean
  description: string
  parentWorkItemNumber: number | null
  childWorkItemNumbers: number[]
  levelId: number | null
}

export interface WorkItemLevel {
  id: number
  name: string
  iconName: string
  color: string
  ordinal: number
  isDefault: boolean
}
