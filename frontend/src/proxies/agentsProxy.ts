import { del, get, post } from './'
import type { AgentExecution, LogEntry } from '../models'

export function getExecutions(projectId: string): Promise<AgentExecution[]> {
  return get<AgentExecution[]>(`/api/projects/${projectId}/agents/executions`)
}

export function getLogs(projectId: string): Promise<LogEntry[]> {
  return get<LogEntry[]>(`/api/projects/${projectId}/agents/logs`)
}

export function clearLogs(projectId: string): Promise<{ deletedCount: number }> {
  return del<{ deletedCount: number }>(`/api/projects/${projectId}/agents/logs`)
}

export function clearExecutionLogs(projectId: string, executionId: string): Promise<{ executionId: string; deletedCount: number }> {
  return del<{ executionId: string; deletedCount: number }>(`/api/projects/${projectId}/agents/executions/${executionId}/logs`)
}

export interface StartExecutionRequest {
  workItemNumber: number
  targetBranch?: string
}

export function startExecution(projectId: string, request: StartExecutionRequest): Promise<{ executionId: string }> {
  return post<{ executionId: string }>(`/api/projects/${projectId}/agents/execute`, request)
}

export interface ExecutionStatus {
  id: string
  status: string
  currentPhase: string | null
  progress: number
  branchName: string | null
  pullRequestUrl: string | null
  error: string | null
}

export function getExecutionStatus(projectId: string, executionId: string): Promise<ExecutionStatus> {
  return get<ExecutionStatus>(`/api/projects/${projectId}/agents/executions/${executionId}/status`)
}

export function cancelExecution(projectId: string, executionId: string): Promise<{ executionId: string; status: string }> {
  return post<{ executionId: string; status: string }>(`/api/projects/${projectId}/agents/executions/${executionId}/cancel`, {})
}

export function pauseExecution(projectId: string, executionId: string): Promise<{ executionId: string; status: string }> {
  return post<{ executionId: string; status: string }>(`/api/projects/${projectId}/agents/executions/${executionId}/pause`, {})
}

export function steerExecution(projectId: string, executionId: string, note: string): Promise<{ executionId: string; status: string }> {
  return post<{ executionId: string; status: string }>(`/api/projects/${projectId}/agents/executions/${executionId}/steer`, { note })
}

export function retryExecution(projectId: string, executionId: string): Promise<{ executionId: string }> {
  return post<{ executionId: string }>(`/api/projects/${projectId}/agents/executions/${executionId}/retry`, {})
}

export function deleteExecution(projectId: string, executionId: string): Promise<{ executionId: string; deletedLogCount: number }> {
  return del<{ executionId: string; deletedLogCount: number }>(`/api/projects/${projectId}/agents/executions/${executionId}`)
}

export interface ExecutionDocumentation {
  executionId: string
  title: string
  markdown: string
  pullRequestUrl: string | null
  diffUrl: string | null
}

export function getExecutionDocumentation(projectId: string, executionId: string): Promise<ExecutionDocumentation> {
  return get<ExecutionDocumentation>(`/api/projects/${projectId}/agents/executions/${executionId}/docs`)
}
