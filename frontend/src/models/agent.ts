export interface AgentExecution {
  id: string
  workItemId: number
  workItemTitle: string
  executionMode: 'standard' | 'orchestration'
  status: 'running' | 'completed' | 'failed' | 'queued' | 'cancelled' | 'paused'
  agents: AgentInfo[]
  startedAt: string
  duration: string
  progress: number
  branchName?: string | null
  pullRequestUrl?: string | null
  currentPhase?: string | null
  reviewLoopCount?: number
  lastReviewRecommendation?: string | null
  parentExecutionId?: string | null
  subFlows?: AgentExecution[]
}

export interface AgentInfo {
  role: string
  status: 'running' | 'completed' | 'idle' | 'failed' | 'cancelled'
  currentTask: string
  progress: number
}

export interface LogEntry {
  time: string
  agent: string
  level: 'info' | 'warn' | 'error' | 'success'
  message: string
  isDetailed: boolean
  executionId?: string | null
}
