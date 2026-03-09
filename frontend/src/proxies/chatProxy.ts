import { get, post, put, del, postForm } from './'
import type { ChatData, ChatMessageData, ChatSessionData, SendMessageResponse, ChatAttachment } from '../models'

const GLOBAL_CHAT_BASE = '/api/chat'

function isProjectScoped(projectId?: string): projectId is string {
  return typeof projectId === 'string' && projectId.trim().length > 0
}

function chatBase(projectId?: string): string {
  return isProjectScoped(projectId) ? `/api/projects/${projectId}/chat` : GLOBAL_CHAT_BASE
}

export function buildChatDataPath(projectId?: string): string {
  return chatBase(projectId)
}

export function buildChatMessagesPath(projectId: string | undefined, sessionId: string): string {
  return `${chatBase(projectId)}/sessions/${sessionId}/messages`
}

export function buildChatSessionsPath(projectId?: string): string {
  return `${chatBase(projectId)}/sessions`
}

export function buildChatAttachmentsPath(projectId: string | undefined, sessionId: string): string {
  return `${chatBase(projectId)}/sessions/${sessionId}/attachments`
}

export function buildDeleteAttachmentPath(projectId: string | undefined, sessionId: string, attachmentId: string): string {
  return `${buildChatAttachmentsPath(projectId, sessionId)}/${attachmentId}`
}

export function buildDeleteSessionPath(projectId: string | undefined, sessionId: string): string {
  return `${buildChatSessionsPath(projectId)}/${sessionId}`
}

export function buildRenameSessionPath(projectId: string | undefined, sessionId: string): string {
  return `${buildChatSessionsPath(projectId)}/${sessionId}`
}

export function getChatData(projectId?: string): Promise<ChatData> {
  return get<ChatData>(buildChatDataPath(projectId))
}

export function getMessages(projectId: string | undefined, sessionId: string): Promise<ChatMessageData[]> {
  return get<ChatMessageData[]>(buildChatMessagesPath(projectId, sessionId))
}

export function createChatSession(projectId: string | undefined, title: string): Promise<ChatSessionData> {
  return post<ChatSessionData>(buildChatSessionsPath(projectId), { title })
}

export function sendChatMessage(projectId: string | undefined, sessionId: string, content: string, generateWorkItems = false): Promise<SendMessageResponse> {
  return post<SendMessageResponse>(buildChatMessagesPath(projectId, sessionId), { content, generateWorkItems })
}

export function getAttachments(projectId: string | undefined, sessionId: string): Promise<ChatAttachment[]> {
  return get<ChatAttachment[]>(buildChatAttachmentsPath(projectId, sessionId))
}

export function uploadAttachment(projectId: string | undefined, sessionId: string, file: File): Promise<ChatAttachment> {
  const formData = new FormData()
  formData.append('file', file)
  return postForm<ChatAttachment>(buildChatAttachmentsPath(projectId, sessionId), formData)
}

export function deleteAttachment(projectId: string | undefined, sessionId: string, attachmentId: string): Promise<void> {
  return del<void>(buildDeleteAttachmentPath(projectId, sessionId, attachmentId))
}

export function deleteChatSession(projectId: string | undefined, sessionId: string): Promise<void> {
  return del<void>(buildDeleteSessionPath(projectId, sessionId))
}

export function renameChatSession(projectId: string | undefined, sessionId: string, title: string): Promise<void> {
  return put<void>(buildRenameSessionPath(projectId, sessionId), { title })
}
