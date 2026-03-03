import { get, post, put, del } from './'
import type { WorkItem } from '../models'

export function getWorkItems(projectId: string): Promise<WorkItem[]> {
  return get<WorkItem[]>(`/api/projects/${projectId}/work-items`)
}

export interface CreateWorkItemRequest {
  title: string
  description: string
  priority: number
  state: string
  assignedTo: string
  tags: string[]
  isAI: boolean
  parentId: number | null
  levelId: number | null
}

export interface UpdateWorkItemRequest {
  title?: string
  description?: string
  priority?: number
  state?: string
  assignedTo?: string
  tags?: string[]
  isAI?: boolean
  parentId?: number | null
  levelId?: number | null
}

export function createWorkItem(projectId: string, request: CreateWorkItemRequest): Promise<WorkItem> {
  return post<WorkItem>(`/api/projects/${projectId}/work-items`, request)
}

export function updateWorkItem(projectId: string, id: number, request: UpdateWorkItemRequest): Promise<WorkItem> {
  return put<WorkItem>(`/api/projects/${projectId}/work-items/${id}`, request)
}

export function deleteWorkItem(projectId: string, id: number): Promise<void> {
  return del<void>(`/api/projects/${projectId}/work-items/${id}`)
}
