import { get, post, put, del, fetchWithAuth, ApiError } from './'
import type { WorkItem } from '../models'

export function getWorkItems(projectId: string): Promise<WorkItem[]> {
  return get<WorkItem[]>(`/api/projects/${projectId}/work-items`)
}

export interface CreateWorkItemRequest {
  title: string
  description: string
  priority: number
  difficulty: number
  state: string
  assignedTo: string
  tags: string[]
  isAI: boolean
  parentWorkItemNumber: number | null
  levelId: number | null
  assignmentMode?: 'auto' | 'manual'
  assignedAgentCount?: number | null
  acceptanceCriteria?: string
}

export interface UpdateWorkItemRequest {
  title?: string
  description?: string
  priority?: number
  difficulty?: number
  state?: string
  assignedTo?: string
  tags?: string[]
  isAI?: boolean
  parentWorkItemNumber?: number | null
  levelId?: number | null
  assignmentMode?: 'auto' | 'manual'
  assignedAgentCount?: number | null
  acceptanceCriteria?: string
}

export function createWorkItem(projectId: string, request: CreateWorkItemRequest): Promise<WorkItem> {
  return post<WorkItem>(`/api/projects/${projectId}/work-items`, request)
}

export function updateWorkItem(projectId: string, workItemNumber: number, request: UpdateWorkItemRequest): Promise<WorkItem> {
  return put<WorkItem>(`/api/projects/${projectId}/work-items/${workItemNumber}`, request)
}

export function bulkUpdateWorkItems(
  projectId: string,
  workItemNumbers: number[],
  request: UpdateWorkItemRequest,
): Promise<WorkItem[]> {
  return Promise.all(workItemNumbers.map((workItemNumber) => updateWorkItem(projectId, workItemNumber, request)))
}

export function deleteWorkItem(projectId: string, workItemNumber: number): Promise<void> {
  return del<void>(`/api/projects/${projectId}/work-items/${workItemNumber}`)
}

export interface WorkItemsImportResult {
  workItemsImported: number
  workItemLevelsImported: number
}

export async function exportWorkItemsFile(projectId: string): Promise<Blob> {
  const response = await fetchWithAuth(`/api/projects/${projectId}/work-items/export`, { method: 'GET' })
  if (!response.ok) {
    let body: unknown
    try {
      body = await response.json()
    } catch {
      body = await response.text()
    }
    throw new ApiError(response.status, body)
  }

  return response.blob()
}

export function importWorkItemsFile(projectId: string, payload: unknown): Promise<WorkItemsImportResult> {
  return post<WorkItemsImportResult>(`/api/projects/${projectId}/work-items/import`, payload)
}
