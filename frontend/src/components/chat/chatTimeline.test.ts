import { describe, expect, it } from 'vitest'
import type { ChatMessageData } from '../../models'
import type { ChatSessionActivity } from '../../models/chat'
import { buildChatTimeline } from './chatTimeline'

function createMessage(overrides: Partial<ChatMessageData>): ChatMessageData {
    return {
        id: overrides.id ?? 'message-id',
        role: overrides.role ?? 'user',
        content: overrides.content ?? 'Hello Fleet',
        timestamp: overrides.timestamp ?? '2026-04-03T10:00:00.000Z',
        attachments: overrides.attachments,
    }
}

function createActivity(overrides: Partial<ChatSessionActivity>): ChatSessionActivity {
    return {
        id: overrides.id ?? 'activity-id',
        kind: overrides.kind ?? 'tool',
        message: overrides.message ?? 'Running tool',
        timestampUtc: overrides.timestampUtc ?? '2026-04-03T10:01:00.000Z',
        toolName: overrides.toolName,
        succeeded: overrides.succeeded,
    }
}

describe('chat timeline', () => {
    it('weaves tool activity between user and assistant messages', () => {
        const timeline = buildChatTimeline(
            [
                createMessage({
                    id: 'user-1',
                    role: 'user',
                    timestamp: '2026-04-03T10:00:00.000Z',
                }),
                createMessage({
                    id: 'assistant-1',
                    role: 'assistant',
                    timestamp: '2026-04-03T10:05:00.000Z',
                }),
            ],
            [
                createActivity({
                    id: 'activity-1',
                    timestampUtc: '2026-04-03T10:01:00.000Z',
                }),
                createActivity({
                    id: 'activity-2',
                    timestampUtc: '2026-04-03T10:03:00.000Z',
                    message: 'Finished tool',
                }),
            ],
            {
                isBusy: false,
            },
        )

        expect(timeline.map((item) => item.type)).toEqual(['message', 'thinking', 'message'])
        expect(timeline[1]?.type).toBe('thinking')
        if (timeline[1]?.type !== 'thinking') {
            throw new Error('expected thinking group')
        }

        expect(timeline[1].group.state).toBe('thought')
        expect(timeline[1].group.activities.map((activity) => activity.id)).toEqual(['activity-1', 'activity-2'])
        expect(timeline[1].group.hasAssistantResponse).toBe(true)
    })

    it('appends a pending thinking block after the latest user message when generation is still busy', () => {
        const timeline = buildChatTimeline(
            [
                createMessage({
                    id: 'user-1',
                    role: 'user',
                    timestamp: '2026-04-03T10:00:00.000Z',
                }),
                createMessage({
                    id: 'assistant-1',
                    role: 'assistant',
                    timestamp: '2026-04-03T10:01:00.000Z',
                }),
                createMessage({
                    id: 'user-2',
                    role: 'user',
                    timestamp: '2026-04-03T10:05:00.000Z',
                }),
            ],
            [
                createActivity({
                    id: 'activity-1',
                    timestampUtc: '2026-04-03T10:00:30.000Z',
                }),
            ],
            {
                isBusy: true,
                statusMessage: 'Queued work-item generation...',
                currentTimestampUtc: '2026-04-03T10:05:05.000Z',
            },
        )

        expect(timeline.map((item) => item.type)).toEqual(['message', 'thinking', 'message', 'message', 'thinking'])
        expect(timeline[4]?.type).toBe('thinking')
        if (timeline[4]?.type !== 'thinking') {
            throw new Error('expected pending thinking group')
        }

        expect(timeline[4].group.state).toBe('thinking')
        expect(timeline[4].group.activities[0]?.message).toBe('Queued work-item generation...')
    })

    it('keeps thinking group ids stable as more tool activity arrives', () => {
        const firstTimeline = buildChatTimeline(
            [
                createMessage({
                    id: 'user-1',
                    role: 'user',
                    timestamp: '2026-04-03T10:00:00.000Z',
                }),
            ],
            [
                createActivity({
                    id: 'activity-1',
                    timestampUtc: '2026-04-03T10:01:00.000Z',
                }),
            ],
            {
                isBusy: true,
            },
        )
        const secondTimeline = buildChatTimeline(
            [
                createMessage({
                    id: 'user-1',
                    role: 'user',
                    timestamp: '2026-04-03T10:00:00.000Z',
                }),
            ],
            [
                createActivity({
                    id: 'activity-1',
                    timestampUtc: '2026-04-03T10:01:00.000Z',
                }),
                createActivity({
                    id: 'activity-2',
                    timestampUtc: '2026-04-03T10:02:00.000Z',
                }),
            ],
            {
                isBusy: true,
            },
        )

        const firstThinking = firstTimeline.find((item) => item.type === 'thinking')
        const secondThinking = secondTimeline.find((item) => item.type === 'thinking')

        expect(firstThinking?.id).toBe(secondThinking?.id)
    })

    it('keeps the pending thinking id stable across rerenders', () => {
        const messages = [
            createMessage({
                id: 'user-1',
                role: 'user' as const,
                timestamp: '2026-04-03T10:00:00.000Z',
            }),
        ]

        const firstTimeline = buildChatTimeline(messages, [], {
            isBusy: true,
            currentTimestampUtc: '2026-04-03T10:00:05.000Z',
        })
        const secondTimeline = buildChatTimeline(messages, [], {
            isBusy: true,
            currentTimestampUtc: '2026-04-03T10:00:06.000Z',
        })

        expect(firstTimeline[1]?.id).toBe(secondTimeline[1]?.id)
    })
})
