import { get, post, put, del } from './'
import type { ProjectData, ProjectDashboard, SlugCheckResult } from '../models'

export interface CreateProjectRequest {
  title: string
  description: string
  repo: string
}

export interface UpdateProjectRequest {
  title?: string
  description?: string
  repo?: string
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
