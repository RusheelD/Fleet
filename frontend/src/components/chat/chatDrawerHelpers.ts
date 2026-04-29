import type { ChatGenerationState, ChatMessageData } from '../../models'

const DEFAULT_GENERATE_MESSAGE =
    'Generate work-items based on provided context. If context is limited, make reasonable assumptions and produce a best-effort initial backlog draft.'

export interface SessionOptimisticOptions {
    optimisticGeneratingSessionIds: string[]
    isCancelingSession: boolean
    isDynamicIterationSession?: boolean
}

export function canSubmitChatMessage(
    userContent: string,
    generateWorkItems: boolean,
    isDynamicIterationSend: boolean,
): boolean {
    const hasContent = userContent.trim().length > 0
    if (isDynamicIterationSend) {
        return hasContent
    }

    return hasContent || generateWorkItems
}

export function resolveContentToSend(
    userContent: string,
    generateWorkItems: boolean,
    isDynamicIterationSend = false,
): string {
    if (generateWorkItems && !isDynamicIterationSend && !userContent) {
        return DEFAULT_GENERATE_MESSAGE
    }

    return userContent
}

export function resolveActiveChatSessionId<TSession extends { id: string; isActive: boolean }>(
    sessions: TSession[],
    currentActiveSessionId: string | undefined,
): string | undefined {
    if (sessions.length === 0) {
        return undefined
    }

    if (currentActiveSessionId && sessions.some((session) => session.id === currentActiveSessionId)) {
        return currentActiveSessionId
    }

    return sessions.find((session) => session.isActive)?.id ?? sessions[0]?.id
}

export function resolveServerMessagesForActiveSession(
    sessionMessages: ChatMessageData[] | undefined,
    chatDataMessages: ChatMessageData[] | undefined,
    activeSessionId: string | undefined,
    chatDataActiveSessionId: string | undefined,
): ChatMessageData[] {
    if (sessionMessages) {
        return sessionMessages
    }

    if (!activeSessionId || activeSessionId === chatDataActiveSessionId) {
        return chatDataMessages ?? []
    }

    return []
}

export function shouldShowChatLoading(
    loadingChat: boolean,
    displayMessages: ChatMessageData[],
): boolean {
    return loadingChat && displayMessages.length === 0
}

export function applySessionOptimisticState<
    TSession extends {
        id: string
        isGenerating: boolean
        generationState: ChatGenerationState
        generationStatus: string | null
    },
>(
    session: TSession,
    options: SessionOptimisticOptions,
): TSession {
    const isOptimisticGenerating = options.optimisticGeneratingSessionIds.includes(session.id)
    const isCancelingSession = options.isCancelingSession

    let generationState = session.generationState
    let generationStatus = session.generationStatus
    let isGenerating = session.isGenerating

    if (isOptimisticGenerating && !session.isGenerating) {
        isGenerating = true
        generationState = 'running'
        generationStatus = session.generationStatus
            ?? (options.isDynamicIterationSession ? 'Preparing dynamic iteration...' : 'Preparing work-item generation...')
    }

    if (isCancelingSession) {
        isGenerating = true
        generationState = 'canceling'
        generationStatus = 'Canceling generation...'
    }

    return {
        ...session,
        isGenerating,
        generationState,
        generationStatus,
    }
}
