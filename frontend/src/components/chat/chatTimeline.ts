import type { ChatMessageData } from '../../models'
import { normalizeChatSessionActivity, type ChatSessionActivity } from '../../models/chat'

export interface ChatThinkingGroup {
    id: string
    activities: ChatSessionActivity[]
    startedAtUtc: string
    endedAtUtc: string
    state: 'thinking' | 'thought'
    hasAssistantResponse: boolean
}

export type ChatTimelineItem =
    | {
        type: 'message'
        id: string
        message: ChatMessageData
    }
    | {
        type: 'thinking'
        id: string
        group: ChatThinkingGroup
    }

interface BuildChatTimelineOptions {
    isBusy: boolean
    statusMessage?: string | null
    currentTimestampUtc?: string
}

type TimelineEntry =
    | {
        type: 'message'
        sourceOrder: number
        sortTime: number | null
        precedence: number
        message: ChatMessageData
    }
    | {
        type: 'activity'
        sourceOrder: number
        sortTime: number | null
        precedence: number
        activity: ChatSessionActivity
    }

export function buildChatTimeline(
    messages: ChatMessageData[],
    activities: ChatSessionActivity[],
    options: BuildChatTimelineOptions,
): ChatTimelineItem[] {
    const currentTimestampUtc = options.currentTimestampUtc ?? new Date().toISOString()
    const normalizedActivities = activities.map((activity, index) => normalizeChatSessionActivity(activity, index))
    const timelineEntries: TimelineEntry[] = [
        ...messages.map((message, index) => ({
            type: 'message' as const,
            sourceOrder: index,
            sortTime: parseTimestamp(message.timestamp),
            precedence: getMessagePrecedence(message),
            message,
        })),
        ...normalizedActivities.map((activity, index) => ({
            type: 'activity' as const,
            sourceOrder: messages.length + index,
            sortTime: parseTimestamp(activity.timestampUtc),
            precedence: 1,
            activity,
        })),
    ].sort(compareTimelineEntries)

    const timeline: ChatTimelineItem[] = []
    let pendingActivities: ChatSessionActivity[] = []

    const flushPendingActivities = (assistantMessage?: ChatMessageData) => {
        if (pendingActivities.length === 0) {
            return
        }

        const firstActivity = pendingActivities[0]
        const lastActivity = pendingActivities[pendingActivities.length - 1]
        const hasAssistantResponse = Boolean(assistantMessage && assistantMessage.role === 'assistant')

        timeline.push({
            type: 'thinking',
            id: buildThinkingGroupId(pendingActivities, assistantMessage),
            group: {
                id: buildThinkingGroupId(pendingActivities, assistantMessage),
                activities: pendingActivities,
                startedAtUtc: firstActivity?.timestampUtc || currentTimestampUtc,
                endedAtUtc: hasAssistantResponse
                    ? assistantMessage?.timestamp || lastActivity?.timestampUtc || currentTimestampUtc
                    : lastActivity?.timestampUtc || currentTimestampUtc,
                state: hasAssistantResponse || !options.isBusy ? 'thought' : 'thinking',
                hasAssistantResponse,
            },
        })

        pendingActivities = []
    }

    for (const entry of timelineEntries) {
        if (entry.type === 'activity') {
            pendingActivities.push(entry.activity)
            continue
        }

        flushPendingActivities(entry.message.role === 'assistant' ? entry.message : undefined)
        timeline.push({
            type: 'message',
            id: entry.message.id,
            message: entry.message,
        })
    }

    flushPendingActivities()

    const lastTimelineItem = timeline[timeline.length - 1]
    if (options.isBusy && (!lastTimelineItem || lastTimelineItem.type === 'message')) {
        const pendingActivity: ChatSessionActivity = {
            id: `pending-activity-${currentTimestampUtc}`,
            kind: 'status',
            message: options.statusMessage?.trim() || 'Fleet AI is thinking...',
            timestampUtc: currentTimestampUtc,
        }

        timeline.push({
            type: 'thinking',
            id: `pending-thinking-${currentTimestampUtc}`,
            group: {
                id: `pending-thinking-${currentTimestampUtc}`,
                activities: [pendingActivity],
                startedAtUtc: currentTimestampUtc,
                endedAtUtc: currentTimestampUtc,
                state: 'thinking',
                hasAssistantResponse: false,
            },
        })
    }

    return timeline
}

export function formatThinkingDuration(startedAtUtc: string, endedAtUtc: string): string {
    const startedMs = parseTimestamp(startedAtUtc)
    const endedMs = parseTimestamp(endedAtUtc)

    if (startedMs === null || endedMs === null) {
        return 'a moment'
    }

    const durationMs = Math.max(0, endedMs - startedMs)
    const totalSeconds = Math.floor(durationMs / 1000)

    if (totalSeconds <= 0) {
        return '<1s'
    }

    const hours = Math.floor(totalSeconds / 3600)
    const minutes = Math.floor((totalSeconds % 3600) / 60)
    const seconds = totalSeconds % 60

    if (hours > 0) {
        return `${hours}h ${minutes}m`
    }

    if (minutes > 0) {
        return `${minutes}m ${seconds}s`
    }

    return `${seconds}s`
}

function buildThinkingGroupId(
    activities: ChatSessionActivity[],
    assistantMessage?: ChatMessageData,
): string {
    const firstActivityId = activities[0]?.id ?? 'activity-start'
    const lastActivityId = activities[activities.length - 1]?.id ?? 'activity-end'
    return `thinking-${firstActivityId}-${lastActivityId}-${assistantMessage?.id ?? 'pending'}`
}

function compareTimelineEntries(a: TimelineEntry, b: TimelineEntry): number {
    if (a.sortTime !== null && b.sortTime !== null) {
        if (a.sortTime !== b.sortTime) {
            return a.sortTime - b.sortTime
        }

        if (a.precedence !== b.precedence) {
            return a.precedence - b.precedence
        }
    }

    return a.sourceOrder - b.sourceOrder
}

function getMessagePrecedence(message: ChatMessageData): number {
    return message.role === 'user' ? 0 : 2
}

function parseTimestamp(timestamp: string | null | undefined): number | null {
    if (!timestamp) {
        return null
    }

    const parsed = Date.parse(timestamp)
    return Number.isNaN(parsed) ? null : parsed
}
