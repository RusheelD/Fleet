export interface ChatMessageData {
  id: string
  role: 'user' | 'assistant'
  content: string
  timestamp: string
}

export interface ChatSessionData {
  id: string
  title: string
  lastMessage: string
  timestamp: string
  isActive: boolean
}
