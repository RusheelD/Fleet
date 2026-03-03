import { get, post, put, del } from './'
import type { WorkItemLevel } from '../models'

export interface CreateWorkItemLevelRequest {
  name: string
  iconName: string
  color: string
  ordinal: number
}

export interface UpdateWorkItemLevelRequest {
  name?: string
  iconName?: string
  color?: string
  ordinal?: number
}

export function getWorkItemLevels(projectId: string): Promise<WorkItemLevel[]> {
  return get<WorkItemLevel[]>(`/api/projects/${projectId}/levels`)
}

export function createWorkItemLevel(projectId: string, request: CreateWorkItemLevelRequest): Promise<WorkItemLevel> {
  return post<WorkItemLevel>(`/api/projects/${projectId}/levels`, request)
}

export function updateWorkItemLevel(projectId: string, id: number, request: UpdateWorkItemLevelRequest): Promise<WorkItemLevel> {
  return put<WorkItemLevel>(`/api/projects/${projectId}/levels/${id}`, request)
}

export function deleteWorkItemLevel(projectId: string, id: number): Promise<void> {
  return del<void>(`/api/projects/${projectId}/levels/${id}`)
}
