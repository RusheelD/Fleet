import { get, post } from './'
import type { ChatData, ChatMessageData, ChatSessionData } from '../models'

export function getChatData(projectId: string): Promise<ChatData> {
  return get<ChatData>(`/api/projects/${projectId}/chat`)
}

export function getMessages(projectId: string, sessionId: string): Promise<ChatMessageData[]> {
  return get<ChatMessageData[]>(`/api/projects/${projectId}/chat/sessions/${sessionId}/messages`)
}

export function createChatSession(projectId: string, title: string): Promise<ChatSessionData> {
  return post<ChatSessionData>(`/api/projects/${projectId}/chat/sessions`, { title })
}

export function sendChatMessage(projectId: string, sessionId: string, content: string): Promise<ChatMessageData> {
  return post<ChatMessageData>(`/api/projects/${projectId}/chat/sessions/${sessionId}/messages`, { content })
}
