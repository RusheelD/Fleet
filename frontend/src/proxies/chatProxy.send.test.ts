import { beforeEach, describe, expect, it, vi } from 'vitest'

const { postMock } = vi.hoisted(() => ({
  postMock: vi.fn(),
}))

vi.mock('./proxy', () => ({
  get: vi.fn(),
  put: vi.fn(),
  del: vi.fn(),
  postForm: vi.fn(),
  post: postMock,
}))

import { sendChatMessage } from './chatProxy'

describe('sendChatMessage', () => {
  beforeEach(() => {
    postMock.mockReset()
    postMock.mockResolvedValue({ sessionId: 'sess-9', assistantMessage: null, toolEvents: [], error: null, isDeferred: false })
  })

  it('posts generate payload to the project-scoped endpoint', async () => {
    await sendChatMessage('proj-1', 'sess-9', { content: 'Generate now', generateWorkItems: true })

    expect(postMock).toHaveBeenCalledTimes(1)
    expect(postMock.mock.calls[0]?.[0]).toBe('/api/projects/proj-1/chat/sessions/sess-9/messages')
    expect(postMock.mock.calls[0]?.[1]).toEqual({ content: 'Generate now', generateWorkItems: true })
    expect(postMock.mock.calls[0]?.[2]).toEqual(expect.objectContaining({ signal: expect.any(AbortSignal) }))
  })

  it('posts normal payload to the global endpoint when project scope is missing', async () => {
    await sendChatMessage(undefined, 'sess-9', { content: 'Hello', generateWorkItems: false })

    expect(postMock).toHaveBeenCalledTimes(1)
    expect(postMock.mock.calls[0]?.[0]).toBe('/api/chat/sessions/sess-9/messages')
    expect(postMock.mock.calls[0]?.[1]).toEqual({ content: 'Hello', generateWorkItems: false })
  })
})
