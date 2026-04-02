import type { ChatMessageData } from '../../models'

const SERVER_ECHO_CLOCK_SKEW_MS = 10_000

export function normalizeMessageContent(value: string): string {
    return value.replace(/\s+/g, ' ').trim().toLowerCase()
}

function parseTimestamp(value: string): number | null {
    const parsed = Date.parse(value)
    return Number.isFinite(parsed) ? parsed : null
}

function isOptimisticMessageConfirmed(
    optimisticMessage: ChatMessageData,
    serverMessages: ChatMessageData[],
): boolean {
    if (optimisticMessage.role !== 'user') {
        return serverMessages.some((serverMessage) => serverMessage.id === optimisticMessage.id)
    }

    const optimisticTimestamp = parseTimestamp(optimisticMessage.timestamp)
    const optimisticContent = normalizeMessageContent(optimisticMessage.content)

    return serverMessages.some((serverMessage) => {
        if (serverMessage.role !== 'user') {
            return false
        }

        if (serverMessage.id === optimisticMessage.id) {
            return true
        }

        if (normalizeMessageContent(serverMessage.content) !== optimisticContent) {
            return false
        }

        const serverTimestamp = parseTimestamp(serverMessage.timestamp)
        if (optimisticTimestamp === null || serverTimestamp === null) {
            return false
        }

        return serverTimestamp >= optimisticTimestamp - SERVER_ECHO_CLOCK_SKEW_MS
    })
}

export function filterPendingOptimisticMessages(
    optimisticMessages: ChatMessageData[],
    serverMessages: ChatMessageData[],
): ChatMessageData[] {
    return optimisticMessages.filter(
        (optimisticMessage) => !isOptimisticMessageConfirmed(optimisticMessage, serverMessages),
    )
}

export function reconcileDisplayMessages(
    serverMessages: ChatMessageData[],
    optimisticMessages: ChatMessageData[],
): ChatMessageData[] {
    const pendingOptimisticMessages = filterPendingOptimisticMessages(optimisticMessages, serverMessages)
    return [...serverMessages, ...pendingOptimisticMessages]
}
