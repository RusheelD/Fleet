export interface AgentExecution {
  id: string
  workItemId: number
  workItemTitle: string
  status: 'running' | 'completed' | 'failed' | 'queued' | 'cancelled' | 'paused'
  agents: AgentInfo[]
  startedAt: string
  duration: string
  progress: number
}

export interface AgentInfo {
  role: string
  status: 'running' | 'completed' | 'idle' | 'failed'
  currentTask: string
  progress: number
}

export interface LogEntry {
  time: string
  agent: string
  level: 'info' | 'warn' | 'error' | 'success'
  message: string
}
