import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { ApiError, fetchWithAuth } from '../proxies'
import { useAuth } from './useAuthHook'

interface ServerEventMessage {
  eventName: string
  data: unknown
}

export const CHAT_TOOL_EVENT_WINDOW_EVENT = 'fleet:chat-tool-event'

export interface ChatToolEventPayload {
  projectId?: string | null
  sessionId: string
  toolName: string
  argumentsJson: string
  result: string
  succeeded: boolean
  timestampUtc: string
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

export function useServerEvents(projectId?: string) {
  const queryClient = useQueryClient()
  const { isAuthenticated } = useAuth()

  useEffect(() => {
    if (!isAuthenticated) {
      return
    }

    let cancelled = false
    let activeController: AbortController | null = null
    const lastInvalidationByTopic = new Map<string, number>()
    const pendingInvalidationTimers = new Map<string, number>()

    const getMinIntervalMs = (topic: string): number => {
      if (topic === 'agents.updated' || topic === 'logs.updated') {
        return 150
      }

      if (topic === 'work-items.updated') {
        return 75
      }

      return 100
    }

    const refreshQuery = (queryName: string) => {
      void queryClient.invalidateQueries({ queryKey: [queryName], refetchType: 'none' })
      // Force active views to re-poll immediately when a stream message arrives.
      void queryClient.refetchQueries({ queryKey: [queryName], type: 'active' })
    }

    const refreshMany = (queryNames: readonly string[]) => {
      for (const queryName of queryNames) {
        refreshQuery(queryName)
      }
    }

    const invalidateForTopicNow = (topic: string) => {
      if (topic === 'connected') {
        refreshMany(['executions', 'logs', 'work-items'])
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

      if (topic === 'logs.updated') {
        refreshQuery('logs')
        return
      }

      if (topic === 'agents.updated') {
        refreshMany(['executions', 'logs', 'work-items'])
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
        const controller = new AbortController()
        activeController = controller

        try {
          await streamServerEvents(
            streamPath,
            ({ eventName, data }) => {
              if (eventName === 'chat.tool-event' && typeof window !== 'undefined') {
                window.dispatchEvent(new CustomEvent<ChatToolEventPayload>(CHAT_TOOL_EVENT_WINDOW_EVENT, {
                  detail: data as ChatToolEventPayload,
                }))
              }

              scheduleInvalidateForTopic(eventName)
            },
            controller.signal,
          )
        } catch (error) {
          if (!cancelled) {
            console.warn('SSE stream disconnected; retrying.', error)
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
