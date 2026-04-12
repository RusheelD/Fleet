import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { ApiError, fetchWithAuth } from '../proxies/proxy'
import { useAuth } from './useAuthHook'
import { normalizeLogEntry, type AgentExecution, type ChatData, type ChatSessionActivity, type ChatSessionData, type LogEntry } from '../models'
import { normalizeChatSessionActivities, normalizeChatSessionActivity } from '../models/chat'
import {
  buildServerEventConnectionDetail,
  cacheServerEventConnection,
  getCachedServerEventConnection,
  type ServerEventConnectionDetail,
  type ServerEventConnectionState,
} from './serverEventConnectionState'

interface ServerEventMessage {
  eventName: string
  data: unknown
}

export const CHAT_TOOL_EVENT_WINDOW_EVENT = 'fleet:chat-tool-event'
export const CHAT_SESSION_EVENT_WINDOW_EVENT = 'fleet:chat-session-event'
export const SERVER_EVENT_CONNECTION_WINDOW_EVENT = 'fleet:server-event-connection'

export interface ChatToolEventPayload {
  projectId?: string | null
  sessionId: string
  toolName: string
  argumentsJson: string
  result: string
  succeeded: boolean
  timestampUtc: string
}

export interface ChatSessionEventPayload {
  projectId?: string | null
  sessionId: string
  isGenerating: boolean
  generationState: ChatSessionData['generationState']
  generationStatus: string | null
  generationUpdatedAtUtc: string
  activity?: ChatSessionActivity | null
}

interface AgentsUpdatedEventPayload {
  projectId?: string | null
  executionId?: string | null
  status?: string | null
  execution?: AgentExecution | null
}

interface LogsUpdatedEventPayload {
  projectId?: string | null
  executionId?: string | null
  deletedCount?: number | null
  logEntry?: LogEntry | null
}

const SSE_STALE_AFTER_MS = 25_000
const AGENT_EVENT_REFRESH_DEBOUNCE_MS = 750
const LOG_EVENT_REFRESH_DEBOUNCE_MS = 1500
const WORK_ITEM_EVENT_REFRESH_DEBOUNCE_MS = 2000
const PROJECT_EVENT_REFRESH_DEBOUNCE_MS = 3000
const DEFAULT_EVENT_REFRESH_DEBOUNCE_MS = 500
const KNOWN_EXECUTION_STATUSES = new Set<AgentExecution['status']>([
  'running',
  'completed',
  'failed',
  'queued',
  'cancelled',
  'paused',
])

function normalizeExecutionTree(execution: AgentExecution): AgentExecution {
  return {
    ...execution,
    subFlows: (execution.subFlows ?? []).map(normalizeExecutionTree),
  }
}

function mergeExecutionSnapshot(existing: AgentExecution, incoming: AgentExecution): AgentExecution {
  const incomingChildren = incoming.subFlows ?? []
  const mergedChildren = incomingChildren.length > 0 ? incomingChildren : existing.subFlows ?? incomingChildren

  return {
    ...incoming,
    branchName: incoming.branchName ?? existing.branchName,
    pullRequestUrl: incoming.pullRequestUrl ?? existing.pullRequestUrl,
    currentPhase: incoming.currentPhase ?? existing.currentPhase,
    reviewLoopCount: incoming.reviewLoopCount && incoming.reviewLoopCount > 0
      ? incoming.reviewLoopCount
      : existing.reviewLoopCount,
    lastReviewRecommendation: incoming.lastReviewRecommendation ?? existing.lastReviewRecommendation,
    subFlows: mergedChildren,
  }
}

function upsertExecutionCollection(
  current: AgentExecution[],
  incoming: AgentExecution,
): { executions: AgentExecution[]; found: boolean } {
  let found = false

  const executions = current.map((execution) => {
    if (execution.id === incoming.id) {
      found = true
      return mergeExecutionSnapshot(execution, incoming)
    }

    if (incoming.parentExecutionId && execution.id === incoming.parentExecutionId) {
      found = true
      const existingChildren = execution.subFlows ?? []
      const existingChildIndex = existingChildren.findIndex((child) => child.id === incoming.id)
      const nextChildren = existingChildIndex >= 0
        ? existingChildren.map((child, index) => (
          index === existingChildIndex ? mergeExecutionSnapshot(child, incoming) : child
        ))
        : [incoming, ...existingChildren]

      return {
        ...execution,
        subFlows: nextChildren,
      }
    }

    if ((execution.subFlows?.length ?? 0) > 0) {
      const nested = upsertExecutionCollection(execution.subFlows ?? [], incoming)
      if (nested.found) {
        found = true
        return {
          ...execution,
          subFlows: nested.executions,
        }
      }
    }

    return execution
  })

  return { executions, found }
}

function removeExecutionCollection(
  current: AgentExecution[],
  executionId: string,
): { executions: AgentExecution[]; removed: boolean } {
  let removed = false

  const executions = current
    .filter((execution) => {
      if (execution.id === executionId) {
        removed = true
        return false
      }

      return true
    })
    .map((execution) => {
      if ((execution.subFlows?.length ?? 0) === 0) {
        return execution
      }

      const nested = removeExecutionCollection(execution.subFlows ?? [], executionId)
      if (!nested.removed) {
        return execution
      }

      removed = true
      return {
        ...execution,
        subFlows: nested.executions,
      }
    })

  return { executions, removed }
}

function patchExecutionCollection(
  current: AgentExecution[],
  executionId: string,
  patch: Partial<AgentExecution>,
): { executions: AgentExecution[]; found: boolean } {
  let found = false

  const executions = current.map((execution) => {
    if (execution.id === executionId) {
      found = true
      return {
        ...execution,
        ...patch,
        subFlows: execution.subFlows,
      }
    }

    if ((execution.subFlows?.length ?? 0) === 0) {
      return execution
    }

    const nested = patchExecutionCollection(execution.subFlows ?? [], executionId, patch)
    if (!nested.found) {
      return execution
    }

    found = true
    return {
      ...execution,
      subFlows: nested.executions,
    }
  })

  return { executions, found }
}

function queryKeyMatchesProject(queryKey: readonly unknown[], projectId?: string | null): boolean {
  if (!projectId) {
    return true
  }

  const paramsBlob = queryKey[1]
  if (typeof paramsBlob !== 'string') {
    return true
  }

  try {
    const parsed = JSON.parse(paramsBlob)
    return Array.isArray(parsed) ? parsed[0] === projectId : true
  } catch {
    return true
  }
}

function parseEventBlock(block: string): ServerEventMessage | null {
  const normalized = block.replace(/\r\n/g, '\n').replace(/\r/g, '\n')
  const lines = normalized.split('\n')

  let eventName = 'message'
  const dataLines: string[] = []

  for (const line of lines) {
    if (!line || line.startsWith(':')) {
      continue
    }

    const separatorIndex = line.indexOf(':')
    const field = separatorIndex >= 0 ? line.slice(0, separatorIndex) : line
    const value = separatorIndex >= 0 ? line.slice(separatorIndex + 1).trimStart() : ''

    if (field === 'event') {
      eventName = value || 'message'
      continue
    }

    if (field === 'data') {
      dataLines.push(value)
    }
  }

  if (dataLines.length === 0) {
    return null
  }

  const rawData = dataLines.join('\n')
  try {
    return {
      eventName,
      data: JSON.parse(rawData),
    }
  } catch {
    return {
      eventName,
      data: rawData,
    }
  }
}

function parseAndDispatchChunks(
  buffer: string,
  onEvent: (event: ServerEventMessage) => void,
): string {
  let remaining = buffer
  while (true) {
    const separatorIndex = remaining.indexOf('\n\n')
    if (separatorIndex < 0) {
      break
    }

    const eventBlock = remaining.slice(0, separatorIndex)
    remaining = remaining.slice(separatorIndex + 2)
    const parsed = parseEventBlock(eventBlock)
    if (parsed) {
      onEvent(parsed)
    }
  }

  return remaining
}

async function streamServerEvents(
  path: string,
  onEvent: (event: ServerEventMessage) => void,
  signal: AbortSignal,
): Promise<void> {
  const response = await fetchWithAuth(path, {
    method: 'GET',
    headers: {
      Accept: 'text/event-stream',
      'Cache-Control': 'no-cache',
    },
    cache: 'no-store',
    signal,
  })

  if (!response.ok) {
    let body: unknown
    try {
      body = await response.json()
    } catch {
      body = await response.text()
    }
    throw new ApiError(response.status, body)
  }

  if (!response.body) {
    throw new Error('SSE response did not include a body stream.')
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) {
      break
    }

    buffer += decoder.decode(value, { stream: true })
    buffer = buffer.replace(/\r\n/g, '\n').replace(/\r/g, '\n')
    buffer = parseAndDispatchChunks(buffer, onEvent)
  }

  if (buffer.trim().length > 0) {
    const parsed = parseEventBlock(buffer)
    if (parsed) {
      onEvent(parsed)
    }
  }
}

function isSameLogEntry(left: LogEntry, right: LogEntry): boolean {
  return left.time === right.time &&
    left.agent === right.agent &&
    left.level === right.level &&
    left.message === right.message &&
    left.isDetailed === right.isDetailed &&
    left.executionId === right.executionId
}

function getExecutionPhaseForStatus(status: string | null | undefined): string | undefined {
  switch (status?.toLowerCase()) {
    case 'paused':
      return 'Paused'
    case 'cancelled':
      return 'Cancelled'
    case 'failed':
      return 'Failed'
    case 'completed':
      return 'Completed'
    case 'running':
      return 'Running'
    default:
      return undefined
  }
}

export function useServerEventConnection(projectId?: string) {
  const normalizedProjectId = projectId ?? null
  const [connection, setConnection] = useState<ServerEventConnectionDetail>(() => getCachedServerEventConnection(normalizedProjectId))

  useEffect(() => {
    setConnection(buildServerEventConnectionDetail(normalizedProjectId))

    if (typeof window === 'undefined') {
      return
    }

    const handleConnectionUpdate = (event: Event) => {
      const detail = (event as CustomEvent<ServerEventConnectionDetail>).detail
      if ((detail.projectId ?? null) !== normalizedProjectId) {
        return
      }

      setConnection(detail)
    }

    window.addEventListener(SERVER_EVENT_CONNECTION_WINDOW_EVENT, handleConnectionUpdate as EventListener)
    return () => {
      window.removeEventListener(SERVER_EVENT_CONNECTION_WINDOW_EVENT, handleConnectionUpdate as EventListener)
    }
  }, [normalizedProjectId])

  return connection
}

export function useServerEvents(projectId?: string) {
  const queryClient = useQueryClient()
  const { isAuthenticated } = useAuth()

  useEffect(() => {
    if (!isAuthenticated) {
      return
    }

    let cancelled = false
    let activeController: AbortController | null = null
    let currentConnectionState: ServerEventConnectionState = 'connecting'
    let hasConnectedOnce = false
    const lastInvalidationByTopic = new Map<string, number>()
    const pendingInvalidationTimers = new Map<string, number>()

    const emitConnectionState = (state: ServerEventConnectionState) => {
      const previousDetail = getCachedServerEventConnection(projectId ?? null)
      const detail = cacheServerEventConnection(buildServerEventConnectionDetail(projectId ?? null, state))
      const stateChanged = currentConnectionState !== state
      const cachedStateChanged =
        (previousDetail.projectId ?? null) !== (detail.projectId ?? null) ||
        previousDetail.state !== detail.state

      currentConnectionState = state

      if ((stateChanged || cachedStateChanged) && typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent<ServerEventConnectionDetail>(SERVER_EVENT_CONNECTION_WINDOW_EVENT, {
          detail,
        }))
      }
    }

    const getMinIntervalMs = (topic: string): number => {
      if (topic === 'agents.updated') {
        return AGENT_EVENT_REFRESH_DEBOUNCE_MS
      }

      if (topic === 'logs.updated') {
        return LOG_EVENT_REFRESH_DEBOUNCE_MS
      }

      if (topic === 'work-items.updated') {
        return WORK_ITEM_EVENT_REFRESH_DEBOUNCE_MS
      }

      if (topic === 'projects.updated') {
        return PROJECT_EVENT_REFRESH_DEBOUNCE_MS
      }

      return DEFAULT_EVENT_REFRESH_DEBOUNCE_MS
    }

    const refreshQuery = (queryName: string) => {
      void queryClient.invalidateQueries({ queryKey: [queryName], refetchType: 'active' })
    }

    const refreshMany = (queryNames: readonly string[]) => {
      for (const queryName of queryNames) {
        refreshQuery(queryName)
      }
    }

    const appendRecentActivity = (
      current: ChatSessionActivity[] | undefined,
      nextActivity: ChatSessionActivity | null | undefined,
    ): ChatSessionActivity[] => {
      const existing = normalizeChatSessionActivities(current)
      if (!nextActivity) {
        return existing
      }

      const normalizedActivity = normalizeChatSessionActivity(nextActivity, existing.length)

      const last = existing.length > 0 ? existing[existing.length - 1] : undefined
      if (
        last &&
        last.kind === normalizedActivity.kind &&
        last.message === normalizedActivity.message &&
        last.toolName === normalizedActivity.toolName &&
        last.succeeded === normalizedActivity.succeeded
      ) {
        return existing
      }

      return [...existing, normalizedActivity].slice(-16)
    }

    const updateChatSessionCaches = (payload: ChatSessionEventPayload) => {
      queryClient.setQueriesData<ChatData>(
        { queryKey: ['chat-data'] },
        (current) => {
          if (!current) {
            return current
          }

          let changed = false
          const nextSessions = current.sessions.map((session) => {
            if (session.id !== payload.sessionId) {
              return session
            }

            changed = true
            return {
              ...session,
              isGenerating: payload.isGenerating,
              generationState: payload.generationState,
              generationStatus: payload.generationStatus,
              generationUpdatedAtUtc: payload.generationUpdatedAtUtc,
              recentActivity: appendRecentActivity(session.recentActivity, payload.activity),
            }
          })

          return changed
            ? {
              ...current,
              sessions: nextSessions,
            }
            : current
        },
      )
    }

    const updateExecutionCaches = (payload: AgentsUpdatedEventPayload): boolean => {
      if (payload.executionId && payload.status?.toLowerCase() === 'deleted') {
        const executionQueries = queryClient.getQueriesData<AgentExecution[]>({ queryKey: ['executions'] })

        for (const [queryKey, snapshot] of executionQueries) {
          if (!Array.isArray(snapshot)) {
            continue
          }

          const queryKeyParts = Array.isArray(queryKey) ? queryKey : [queryKey]
          if (!queryKeyMatchesProject(queryKeyParts, payload.projectId)) {
            continue
          }

          const updated = removeExecutionCollection(snapshot, payload.executionId)
          if (updated.removed) {
            queryClient.setQueryData(queryKey, updated.executions)
          }
        }

        return true
      }

      if (payload.execution) {
        const incomingExecution = normalizeExecutionTree(payload.execution)
        const executionQueries = queryClient.getQueriesData<AgentExecution[]>({ queryKey: ['executions'] })

        for (const [queryKey, snapshot] of executionQueries) {
          if (!Array.isArray(snapshot)) {
            continue
          }

          const queryKeyParts = Array.isArray(queryKey) ? queryKey : [queryKey]
          if (!queryKeyMatchesProject(queryKeyParts, payload.projectId)) {
            continue
          }

          const updated = upsertExecutionCollection(snapshot, incomingExecution)
          if (updated.found) {
            queryClient.setQueryData(queryKey, updated.executions)
            continue
          }

          if (!incomingExecution.parentExecutionId) {
            queryClient.setQueryData(queryKey, [incomingExecution, ...snapshot])
          }
        }

        return true
      }

      if (payload.executionId && payload.status) {
        const normalizedStatus = payload.status.toLowerCase()
        const nextStatus = KNOWN_EXECUTION_STATUSES.has(normalizedStatus as AgentExecution['status'])
          ? normalizedStatus as AgentExecution['status']
          : undefined
        const patch: Partial<AgentExecution> = {
          status: nextStatus,
          currentPhase: getExecutionPhaseForStatus(normalizedStatus),
        }

        const executionQueries = queryClient.getQueriesData<AgentExecution[]>({ queryKey: ['executions'] })
        for (const [queryKey, snapshot] of executionQueries) {
          if (!Array.isArray(snapshot)) {
            continue
          }

          const queryKeyParts = Array.isArray(queryKey) ? queryKey : [queryKey]
          if (!queryKeyMatchesProject(queryKeyParts, payload.projectId)) {
            continue
          }

          const updated = patchExecutionCollection(snapshot, payload.executionId, patch)
          if (updated.found) {
            queryClient.setQueryData(queryKey, updated.executions)
          }
        }

        return true
      }

      return false
    }

    const updateLogCaches = (payload: LogsUpdatedEventPayload): boolean => {
      if (!payload.logEntry) {
        return false
      }

      const logEntry = normalizeLogEntry(payload.logEntry)
      const logQueries = queryClient.getQueriesData<LogEntry[]>({ queryKey: ['logs'] })
      for (const [queryKey, snapshot] of logQueries) {
        if (!Array.isArray(snapshot)) {
          continue
        }

        const queryKeyParts = Array.isArray(queryKey) ? queryKey : [queryKey]
        if (!queryKeyMatchesProject(queryKeyParts, payload.projectId)) {
          continue
        }

        if (snapshot.some((entry) => isSameLogEntry(entry, logEntry))) {
          continue
        }

        queryClient.setQueryData(queryKey, [...snapshot, logEntry])
      }

      return true
    }

    const invalidateForTopicNow = (topic: string) => {
      if (topic === 'connected') {
        refreshMany([
          'chat-data',
          'chat-messages',
          'chat-attachments',
          'executions',
          'logs',
          'work-items',
          'project-dashboard',
          'project-dashboard-slug',
          'projects',
          'notifications',
        ])
        return
      }

      if (topic === 'chat.updated') {
        refreshMany(['chat-data', 'chat-messages', 'chat-attachments'])
        return
      }

      if (topic === 'notifications.updated') {
        refreshQuery('notifications')
        return
      }

      if (topic === 'heartbeat') {
        return
      }

      if (topic === 'logs.updated') {
        refreshMany(['logs'])
        return
      }

      if (topic === 'agents.updated') {
        refreshMany(['executions'])
        return
      }

      if (topic === 'work-items.updated') {
        refreshMany(['work-items', 'project-dashboard', 'project-dashboard-slug', 'projects'])
        return
      }

      if (topic === 'projects.updated') {
        refreshMany(['projects', 'project-dashboard', 'project-dashboard-slug'])
      }
    }

    const scheduleInvalidateForTopic = (topic: string) => {
      const now = Date.now()
      const minIntervalMs = getMinIntervalMs(topic)
      const previous = lastInvalidationByTopic.get(topic) ?? 0
      const elapsed = now - previous

      if (elapsed >= minIntervalMs) {
        lastInvalidationByTopic.set(topic, now)
        invalidateForTopicNow(topic)
        return
      }

      if (pendingInvalidationTimers.has(topic)) {
        return
      }

      const delay = Math.max(0, minIntervalMs - elapsed)
      const timerId = window.setTimeout(() => {
        pendingInvalidationTimers.delete(topic)
        if (cancelled) {
          return
        }

        lastInvalidationByTopic.set(topic, Date.now())
        invalidateForTopicNow(topic)
      }, delay)

      pendingInvalidationTimers.set(topic, timerId)
    }

    const streamPath = projectId
      ? `/api/events/stream?projectId=${encodeURIComponent(projectId)}`
      : '/api/events/stream'

    const run = async () => {
      let reconnectDelayMs = 1000

      while (!cancelled) {
        emitConnectionState(hasConnectedOnce ? 'reconnecting' : 'connecting')

        const controller = new AbortController()
        activeController = controller
        let staleTimerId: number | null = null

        const resetStaleTimer = () => {
          if (staleTimerId !== null) {
            window.clearTimeout(staleTimerId)
          }

          staleTimerId = window.setTimeout(() => {
            if (cancelled || controller.signal.aborted) {
              return
            }

            console.warn('SSE stream became stale; forcing reconnect.')
            controller.abort()
          }, SSE_STALE_AFTER_MS)
        }

        try {
          resetStaleTimer()
          await streamServerEvents(
            streamPath,
            ({ eventName, data }) => {
              const wasPreviouslyConnected = hasConnectedOnce
              hasConnectedOnce = true
              reconnectDelayMs = 1000
              resetStaleTimer()
              emitConnectionState('live')

              let handledByCache = false

              if (eventName === 'agents.updated') {
                handledByCache = updateExecutionCaches(data as AgentsUpdatedEventPayload)
              }

              if (eventName === 'logs.updated') {
                handledByCache = updateLogCaches(data as LogsUpdatedEventPayload)
              }

              if (eventName === 'chat.session-event') {
                const detail = data as ChatSessionEventPayload
                updateChatSessionCaches(detail)

                if (typeof window !== 'undefined') {
                  window.dispatchEvent(new CustomEvent<ChatSessionEventPayload>(CHAT_SESSION_EVENT_WINDOW_EVENT, {
                    detail,
                  }))
                }
              }

              if (eventName === 'chat.tool-event' && typeof window !== 'undefined') {
                window.dispatchEvent(new CustomEvent<ChatToolEventPayload>(CHAT_TOOL_EVENT_WINDOW_EVENT, {
                  detail: data as ChatToolEventPayload,
                }))
              }

              const shouldInvalidate =
                (eventName === 'connected' && wasPreviouslyConnected) ||
                eventName === 'chat.updated' ||
                eventName === 'notifications.updated' ||
                eventName === 'work-items.updated' ||
                eventName === 'projects.updated' ||
                (eventName === 'agents.updated' && !handledByCache) ||
                (eventName === 'logs.updated' && !handledByCache)

              if (shouldInvalidate) {
                scheduleInvalidateForTopic(eventName)
              }
            },
            controller.signal,
          )
        } catch (error) {
          if (!cancelled) {
            emitConnectionState('reconnecting')
            console.warn('SSE stream disconnected; retrying.', error)
          }
        } finally {
          if (staleTimerId !== null) {
            window.clearTimeout(staleTimerId)
          }
        }

        if (cancelled) {
          break
        }

        await new Promise<void>((resolve) => {
          window.setTimeout(resolve, reconnectDelayMs)
        })
        reconnectDelayMs = Math.min(reconnectDelayMs * 2, 15000)
      }
    }

    void run()

    return () => {
      cancelled = true
      if (activeController) {
        activeController.abort()
      }
      for (const timerId of pendingInvalidationTimers.values()) {
        window.clearTimeout(timerId)
      }
      pendingInvalidationTimers.clear()
    }
  }, [isAuthenticated, projectId, queryClient])
}
