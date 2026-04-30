import { get, post, put, del, fetchWithAuth, ApiError } from './proxy'
import type { ProjectBranch, ProjectData, ProjectDashboard, SlugCheckResult } from '../models'

export interface CreateProjectRequest {
  title: string
  description: string
  repo: string
  branchPattern?: string
  commitAuthorMode?: string
  commitAuthorName?: string
  commitAuthorEmail?: string
}

export interface UpdateProjectRequest {
  title?: string
  description?: string
  repo?: string
  branchPattern?: string
  commitAuthorMode?: string
  commitAuthorName?: string
  commitAuthorEmail?: string
}

export interface ProjectsImportResult {
  projectsImported: number
  workItemsImported: number
  workItemLevelsImported: number
  importedProjectIds: string[]
}

export function getProjects(): Promise<ProjectData[]> {
  return get<ProjectData[]>('/api/projects')
}

export function getProjectDashboard(projectId: string): Promise<ProjectDashboard> {
  return get<ProjectDashboard>(`/api/projects/${projectId}`)
}

export function getProjectDashboardBySlug(slug: string): Promise<ProjectDashboard> {
  return get<ProjectDashboard>(`/api/projects/by-slug/${slug}`)
}

export function getProjectBranches(projectId: string): Promise<ProjectBranch[]> {
  return get<ProjectBranch[]>(`/api/projects/${projectId}/branches`)
}

export function createProject(data: CreateProjectRequest): Promise<ProjectData> {
  return post<ProjectData>('/api/projects', data)
}

export function updateProject(projectId: string, data: UpdateProjectRequest): Promise<ProjectData> {
  return put<ProjectData>(`/api/projects/${projectId}`, data)
}

export function deleteProject(projectId: string): Promise<void> {
  return del<void>(`/api/projects/${projectId}`)
}

export function checkSlug(name: string): Promise<SlugCheckResult> {
  return get<SlugCheckResult>(`/api/projects/check-slug?name=${encodeURIComponent(name)}`)
}

export async function exportProjectsFile(): Promise<Blob> {
  const response = await fetchWithAuth('/api/projects/export', { method: 'GET' })
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

export function importProjectsFile(payload: unknown): Promise<ProjectsImportResult> {
  return post<ProjectsImportResult>('/api/projects/import', payload)
}
