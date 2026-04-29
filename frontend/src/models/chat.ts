export type ChatGenerationState =
  | 'idle'
  | 'running'
  | 'canceling'
  | 'completed'
  | 'failed'
  | 'canceled'
  | 'interrupted'

export interface ChatSessionActivity {
  id: string
  kind: 'status' | 'tool' | 'error'
  message: string
  timestampUtc: string
  state?: 'created' | 'queued' | 'running' | 'failed' | null
  toolName?: string | null
  succeeded?: boolean | null
}

export type ChatDynamicStrategy = 'balanced' | 'parallel' | 'sequential'

export interface ChatDynamicOptions {
  enabled: boolean
  branchName?: string | null
  strategy?: ChatDynamicStrategy | null
}

export interface ChatDynamicPolicy {
  autoStartLimit?: number | null
}

export interface ChatMessageData {
  id: string
  role: 'user' | 'assistant'
  content: string
  timestamp: string
  attachments?: ChatAttachment[]
}

export interface ChatSessionData {
  id: string
  title: string
  lastMessage: string
  timestamp: string
  isActive: boolean
  isGenerating: boolean
  generationState: ChatGenerationState
  generationStatus: string | null
  generationUpdatedAtUtc: string | null
  dynamicOptions?: ChatDynamicOptions | null
  dynamicPolicy?: ChatDynamicPolicy | null
  recentActivity: ChatSessionActivity[]
}

export interface ChatData {
  sessions: ChatSessionData[]
  messages: ChatMessageData[]
  suggestions: string[]
}

export interface ToolEvent {
  toolName: string
  argumentsJson: string
  result: string
}

export interface SendMessageResponse {
  sessionId: string
  assistantMessage: ChatMessageData | null
  toolEvents: ToolEvent[]
  error: string | null
  isDeferred: boolean
  session?: ChatSessionData | null
}

export interface ChatAttachment {
  id: string
  fileName: string
  contentLength: number
  uploadedAt: string
  contentType: string
  contentUrl: string
  markdownReference: string
  isImage: boolean
}

function normalizeActivityKind(kind: unknown): ChatSessionActivity['kind'] {
  return kind === 'tool' || kind === 'error' ? kind : 'status'
}

export function normalizeChatSessionActivity(
  activity: unknown,
  fallbackIndex = 0,
): ChatSessionActivity {
  const source = (activity && typeof activity === 'object') ? activity as Partial<ChatSessionActivity> : {}
  const kind = normalizeActivityKind(source.kind)
  const message = typeof source.message === 'string' && source.message.trim().length > 0
    ? source.message
    : 'Session update'
  const timestampUtc = typeof source.timestampUtc === 'string' ? source.timestampUtc : ''
  const toolName = typeof source.toolName === 'string' ? source.toolName : null
  const succeeded = typeof source.succeeded === 'boolean' ? source.succeeded : null
  const state = source.state === 'created'
    || source.state === 'queued'
    || source.state === 'running'
    || source.state === 'failed'
    ? source.state
    : null
  const id = typeof source.id === 'string' && source.id.trim().length > 0
    ? source.id
    : `${kind}-${timestampUtc || 'unknown'}-${fallbackIndex}`

  return {
    id,
    kind,
    message,
    timestampUtc,
    state,
    toolName,
    succeeded,
  }
}

export function normalizeChatSessionActivities(activities: unknown): ChatSessionActivity[] {
  if (!Array.isArray(activities)) {
    return []
  }

  return activities.map((activity, index) => normalizeChatSessionActivity(activity, index))
}
