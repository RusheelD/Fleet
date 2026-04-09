import { get, post, put, del, postForm, fetchWithAuth, ApiError } from './proxy'
import type { WorkItem, WorkItemAttachment } from '../models'

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

export async function bulkDeleteWorkItems(projectId: string, workItemNumbers: number[]): Promise<void> {
  // Delete sequentially — callers sort deepest-first so children are
  // removed before their parents, avoiding FK constraint failures.
  for (const workItemNumber of workItemNumbers) {
    await deleteWorkItem(projectId, workItemNumber)
  }
}

export function deleteWorkItem(projectId: string, workItemNumber: number): Promise<void> {
  return del<void>(`/api/projects/${projectId}/work-items/${workItemNumber}`)
}

function buildWorkItemAttachmentsPath(projectId: string, workItemNumber: number): string {
  return `/api/projects/${projectId}/work-items/${workItemNumber}/attachments`
}

export function getWorkItemAttachments(projectId: string, workItemNumber: number): Promise<WorkItemAttachment[]> {
  return get<WorkItemAttachment[]>(buildWorkItemAttachmentsPath(projectId, workItemNumber))
}

export function uploadWorkItemAttachment(projectId: string, workItemNumber: number, file: File): Promise<WorkItemAttachment> {
  const formData = new FormData()
  formData.append('file', file)
  return postForm<WorkItemAttachment>(buildWorkItemAttachmentsPath(projectId, workItemNumber), formData)
}

export function deleteWorkItemAttachment(projectId: string, workItemNumber: number, attachmentId: string): Promise<void> {
  return del<void>(`${buildWorkItemAttachmentsPath(projectId, workItemNumber)}/${attachmentId}`)
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
