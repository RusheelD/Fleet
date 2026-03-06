import { describe, expect, it } from 'vitest'
import {
  buildChatAttachmentsPath,
  buildChatDataPath,
  buildChatMessagesPath,
  buildDeleteAttachmentPath,
  buildDeleteSessionPath,
} from './chatProxy'

describe('chat proxy scoped paths', () => {
  it('scopes chat endpoints by project and session ids', () => {
    expect(buildChatDataPath('proj-1')).toBe('/api/projects/proj-1/chat')
    expect(buildChatMessagesPath('proj-1', 'sess-9')).toBe('/api/projects/proj-1/chat/sessions/sess-9/messages')
    expect(buildChatAttachmentsPath('proj-1', 'sess-9')).toBe('/api/projects/proj-1/chat/sessions/sess-9/attachments')
  })

  it('builds scoped delete endpoints for sessions and attachments', () => {
    expect(buildDeleteAttachmentPath('proj-1', 'sess-9', 'att-2'))
      .toBe('/api/projects/proj-1/chat/sessions/sess-9/attachments/att-2')
    expect(buildDeleteSessionPath('proj-1', 'sess-9'))
      .toBe('/api/projects/proj-1/chat/sessions/sess-9')
  })
})
