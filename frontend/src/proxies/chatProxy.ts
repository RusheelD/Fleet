import { get, post, del, postForm } from './'
import type { ChatData, ChatMessageData, ChatSessionData, SendMessageResponse, ChatAttachment } from '../models'

export function getChatData(projectId: string): Promise<ChatData> {
  return get<ChatData>(`/api/projects/${projectId}/chat`)
}

export function getMessages(projectId: string, sessionId: string): Promise<ChatMessageData[]> {
  return get<ChatMessageData[]>(`/api/projects/${projectId}/chat/sessions/${sessionId}/messages`)
}

export function createChatSession(projectId: string, title: string): Promise<ChatSessionData> {
  return post<ChatSessionData>(`/api/projects/${projectId}/chat/sessions`, { title })
}

export function sendChatMessage(projectId: string, sessionId: string, content: string, generateWorkItems = false): Promise<SendMessageResponse> {
  return post<SendMessageResponse>(`/api/projects/${projectId}/chat/sessions/${sessionId}/messages`, { content, generateWorkItems })
}

export function getAttachments(projectId: string, sessionId: string): Promise<ChatAttachment[]> {
  return get<ChatAttachment[]>(`/api/projects/${projectId}/chat/sessions/${sessionId}/attachments`)
}

export function uploadAttachment(projectId: string, sessionId: string, file: File): Promise<ChatAttachment> {
  const formData = new FormData()
  formData.append('file', file)
  return postForm<ChatAttachment>(`/api/projects/${projectId}/chat/sessions/${sessionId}/attachments`, formData)
}

export function deleteAttachment(projectId: string, sessionId: string, attachmentId: string): Promise<void> {
  return del<void>(`/api/projects/${projectId}/chat/sessions/${sessionId}/attachments/${attachmentId}`)
}

export function deleteChatSession(projectId: string, sessionId: string): Promise<void> {
  return del<void>(`/api/projects/${projectId}/chat/sessions/${sessionId}`)
}

