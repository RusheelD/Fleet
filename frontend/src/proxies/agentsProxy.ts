import { get, post } from './'
import type { AgentExecution, LogEntry } from '../models'

export function getExecutions(projectId: string): Promise<AgentExecution[]> {
  return get<AgentExecution[]>(`/api/projects/${projectId}/agents/executions`)
}

export function getLogs(projectId: string): Promise<LogEntry[]> {
  return get<LogEntry[]>(`/api/projects/${projectId}/agents/logs`)
}

export function startExecution(projectId: string, workItemNumber: number): Promise<{ executionId: string }> {
  return post<{ executionId: string }>(`/api/projects/${projectId}/agents/execute`, { workItemNumber })
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
