import { get } from './'
import type { AgentExecution, LogEntry } from '../models'

export function getExecutions(projectId: string): Promise<AgentExecution[]> {
  return get<AgentExecution[]>(`/api/projects/${projectId}/agents/executions`)
}

export function getLogs(projectId: string): Promise<LogEntry[]> {
  return get<LogEntry[]>(`/api/projects/${projectId}/agents/logs`)
}
