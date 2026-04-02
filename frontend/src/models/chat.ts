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
