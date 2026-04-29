import { describe, expect, it } from 'vitest'
import {
  buildCancelGenerationPath,
  buildChatAttachmentsPath,
  buildChatDataPath,
  buildChatMessagesPath,
  buildDeleteAttachmentPath,
  buildDeleteSessionPath,
  buildRenameSessionPath,
  buildUpdateSessionDynamicIterationPath,
} from './chatProxy'

describe('chat proxy scoped paths', () => {
  it('builds project-scoped chat endpoints when project id is present', () => {
    expect(buildChatDataPath('proj-1')).toBe('/api/projects/proj-1/chat')
    expect(buildChatMessagesPath('proj-1', 'sess-9')).toBe('/api/projects/proj-1/chat/sessions/sess-9/messages')
    expect(buildChatAttachmentsPath('proj-1', 'sess-9')).toBe('/api/projects/proj-1/chat/sessions/sess-9/attachments')
  })

  it('builds global chat endpoints when project id is missing', () => {
    expect(buildChatDataPath()).toBe('/api/chat')
    expect(buildChatMessagesPath(undefined, 'sess-9')).toBe('/api/chat/sessions/sess-9/messages')
    expect(buildChatAttachmentsPath(undefined, 'sess-9')).toBe('/api/chat/sessions/sess-9/attachments')
  })

  it('builds delete endpoints for both scopes', () => {
    expect(buildDeleteAttachmentPath('proj-1', 'sess-9', 'att-2'))
      .toBe('/api/projects/proj-1/chat/sessions/sess-9/attachments/att-2')
    expect(buildDeleteSessionPath('proj-1', 'sess-9'))
      .toBe('/api/projects/proj-1/chat/sessions/sess-9')

    expect(buildDeleteAttachmentPath(undefined, 'sess-9', 'att-2'))
      .toBe('/api/chat/sessions/sess-9/attachments/att-2')
    expect(buildDeleteSessionPath(undefined, 'sess-9'))
      .toBe('/api/chat/sessions/sess-9')
  })

  it('builds rename endpoints for both scopes', () => {
    expect(buildRenameSessionPath('proj-1', 'sess-9'))
      .toBe('/api/projects/proj-1/chat/sessions/sess-9')
    expect(buildRenameSessionPath(undefined, 'sess-9'))
      .toBe('/api/chat/sessions/sess-9')
  })

  it('builds cancel-generation endpoints for both scopes', () => {
    expect(buildCancelGenerationPath('proj-1', 'sess-9'))
      .toBe('/api/projects/proj-1/chat/sessions/sess-9/cancel-generation')
    expect(buildCancelGenerationPath(undefined, 'sess-9'))
      .toBe('/api/chat/sessions/sess-9/cancel-generation')
  })

  it('builds dynamic iteration settings endpoints for both scopes', () => {
    expect(buildUpdateSessionDynamicIterationPath('proj-1', 'sess-9'))
      .toBe('/api/projects/proj-1/chat/sessions/sess-9/dynamic-iteration')
    expect(buildUpdateSessionDynamicIterationPath(undefined, 'sess-9'))
      .toBe('/api/chat/sessions/sess-9/dynamic-iteration')
  })
})
