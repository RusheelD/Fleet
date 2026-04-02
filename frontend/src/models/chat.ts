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
  toolName?: string | null
  succeeded?: boolean | null
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
}

export interface ChatAttachment {
  id: string
  fileName: string
  contentLength: number
  uploadedAt: string
}
