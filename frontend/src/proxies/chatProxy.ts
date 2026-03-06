import { get, post, del, postForm } from './'
import type { ChatData, ChatMessageData, ChatSessionData, SendMessageResponse, ChatAttachment } from '../models'

export function buildChatDataPath(projectId: string): string {
  return `/api/projects/${projectId}/chat`
}

export function buildChatMessagesPath(projectId: string, sessionId: string): string {
  return `/api/projects/${projectId}/chat/sessions/${sessionId}/messages`
}

export function buildChatSessionsPath(projectId: string): string {
  return `/api/projects/${projectId}/chat/sessions`
}

export function buildChatAttachmentsPath(projectId: string, sessionId: string): string {
  return `/api/projects/${projectId}/chat/sessions/${sessionId}/attachments`
}

export function buildDeleteAttachmentPath(projectId: string, sessionId: string, attachmentId: string): string {
  return `${buildChatAttachmentsPath(projectId, sessionId)}/${attachmentId}`
}

export function buildDeleteSessionPath(projectId: string, sessionId: string): string {
  return `${buildChatSessionsPath(projectId)}/${sessionId}`
}

export function getChatData(projectId: string): Promise<ChatData> {
  return get<ChatData>(buildChatDataPath(projectId))
}

export function getMessages(projectId: string, sessionId: string): Promise<ChatMessageData[]> {
  return get<ChatMessageData[]>(buildChatMessagesPath(projectId, sessionId))
}

export function createChatSession(projectId: string, title: string): Promise<ChatSessionData> {
  return post<ChatSessionData>(buildChatSessionsPath(projectId), { title })
}

export function sendChatMessage(projectId: string, sessionId: string, content: string, generateWorkItems = false): Promise<SendMessageResponse> {
  return post<SendMessageResponse>(buildChatMessagesPath(projectId, sessionId), { content, generateWorkItems })
}

export function getAttachments(projectId: string, sessionId: string): Promise<ChatAttachment[]> {
  return get<ChatAttachment[]>(buildChatAttachmentsPath(projectId, sessionId))
}

export function uploadAttachment(projectId: string, sessionId: string, file: File): Promise<ChatAttachment> {
  const formData = new FormData()
  formData.append('file', file)
  return postForm<ChatAttachment>(buildChatAttachmentsPath(projectId, sessionId), formData)
}

export function deleteAttachment(projectId: string, sessionId: string, attachmentId: string): Promise<void> {
  return del<void>(buildDeleteAttachmentPath(projectId, sessionId, attachmentId))
}

export function deleteChatSession(projectId: string, sessionId: string): Promise<void> {
  return del<void>(buildDeleteSessionPath(projectId, sessionId))
}

