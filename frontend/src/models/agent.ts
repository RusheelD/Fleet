export interface AgentExecution {
  id: string
  workItemId: number
  workItemTitle: string
  executionMode: 'standard' | 'orchestration'
  deliveryMode?: 'pull_request' | 'target_branch'
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

const KNOWN_LOG_LEVELS = new Set<LogEntry['level']>(['info', 'warn', 'error', 'success'])

function normalizeOptionalString(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

export function normalizeLogEntry(log: Partial<LogEntry> | null | undefined): LogEntry {
  const level = typeof log?.level === 'string' && KNOWN_LOG_LEVELS.has(log.level as LogEntry['level'])
    ? log.level as LogEntry['level']
    : 'info'

  return {
    time: normalizeOptionalString(log?.time),
    agent: normalizeOptionalString(log?.agent) || 'System',
    level,
    message: normalizeOptionalString(log?.message),
    isDetailed: Boolean(log?.isDetailed),
    executionId: typeof log?.executionId === 'string' && log.executionId.trim().length > 0
      ? log.executionId
      : null,
  }
}

export function normalizeLogEntries(logs: Array<Partial<LogEntry> | null | undefined> | null | undefined): LogEntry[] {
  return (logs ?? []).map(normalizeLogEntry)
}

export function compareLogEntriesByTime(left: Partial<LogEntry> | null | undefined, right: Partial<LogEntry> | null | undefined): number {
  const leftTimeRaw = normalizeOptionalString(left?.time)
  const rightTimeRaw = normalizeOptionalString(right?.time)
  const leftTime = Date.parse(leftTimeRaw)
  const rightTime = Date.parse(rightTimeRaw)

  if (Number.isNaN(leftTime) || Number.isNaN(rightTime)) {
    return leftTimeRaw.localeCompare(rightTimeRaw)
  }

  return leftTime - rightTime
}
