import { describe, expect, it } from 'vitest'
import type { ChatMessageData } from '../../models'
import { filterPendingOptimisticMessages, reconcileDisplayMessages } from './chatMessageReconciliation'

function createMessage(overrides: Partial<ChatMessageData>): ChatMessageData {
    return {
        id: overrides.id ?? 'message-id',
        role: overrides.role ?? 'user',
        content: overrides.content ?? 'Hello Fleet',
        timestamp: overrides.timestamp ?? '2026-04-01T20:00:00.000Z',
        attachments: overrides.attachments,
    }
}

describe('chat message reconciliation', () => {
    it('keeps a repeated optimistic user message visible until a fresh server echo arrives', () => {
        const oldServerMessage = createMessage({
            id: 'server-old',
            content: 'Run tests',
            timestamp: '2026-04-01T19:58:00.000Z',
        })
        const optimisticMessage = createMessage({
            id: 'optimistic-new',
            content: 'Run tests',
            timestamp: '2026-04-01T20:00:00.000Z',
        })

        const displayMessages = reconcileDisplayMessages([oldServerMessage], [optimisticMessage])

        expect(displayMessages).toHaveLength(2)
        expect(displayMessages[1]?.id).toBe('optimistic-new')
    })

    it('drops an optimistic message once the server has echoed the same content back', () => {
        const optimisticMessage = createMessage({
            id: 'optimistic-new',
            content: 'Run tests',
            timestamp: '2026-04-01T20:00:00.000Z',
        })
        const freshServerMessage = createMessage({
            id: 'server-new',
            content: 'Run tests',
            timestamp: '2026-04-01T20:00:03.000Z',
        })

        const pendingMessages = filterPendingOptimisticMessages([optimisticMessage], [freshServerMessage])

        expect(pendingMessages).toHaveLength(0)
    })

    it('does not treat assistant messages as user-message echoes', () => {
        const optimisticMessage = createMessage({
            id: 'optimistic-user',
            role: 'user',
            content: 'Summarize this file',
            timestamp: '2026-04-01T20:00:00.000Z',
        })
        const assistantMessage = createMessage({
            id: 'assistant-reply',
            role: 'assistant',
            content: 'Summarize this file',
            timestamp: '2026-04-01T20:00:02.000Z',
        })

        const pendingMessages = filterPendingOptimisticMessages([optimisticMessage], [assistantMessage])

        expect(pendingMessages).toHaveLength(1)
        expect(pendingMessages[0]?.id).toBe('optimistic-user')
    })
})
