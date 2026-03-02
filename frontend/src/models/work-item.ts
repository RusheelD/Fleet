export type WorkItemState = 'New' | 'Active' | 'In Progress' | 'In Progress (AI)' | 'Resolved' | 'Resolved (AI)' | 'Closed'

export interface WorkItem {
  id: number
  title: string
  state: WorkItemState
  priority: 1 | 2 | 3 | 4
  assignedTo: string
  tags: string[]
  isAI: boolean
  description: string
}
