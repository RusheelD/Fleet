import type { ChatGenerationState, ChatMessageData } from '../../models'

const DEFAULT_GENERATE_MESSAGE =
    'Generate work-items based on provided context. If context is limited, make reasonable assumptions and produce a best-effort initial backlog draft.'
const MAX_PROMPT_STARTERS = 5

const GLOBAL_PROMPT_STARTERS = [
    'Show what needs attention across my workspace',
    'Find recent project activity',
    'Help me choose the next task',
]

const PROJECT_PROMPT_STARTERS = [
    'Summarize this project',
    'Find the riskiest open work',
    'Draft work items for the next milestone',
    'Turn this request into a plan',
]

const DYNAMIC_ITERATION_PROMPT_STARTERS = [
    'Fix the failing build',
    'Implement the next ready work item',
    'Review this branch for regressions',
    'Refactor this flow without changing behavior',
]

export interface SessionOptimisticOptions {
    optimisticGeneratingSessionIds: string[]
    isCancelingSession: boolean
    isDynamicIterationSession?: boolean
}

export interface ChatModeOptions {
    allowGenerateWorkItems: boolean
    dynamicIterationActive: boolean
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

export function shouldShowPromptStarters(
    loadingChat: boolean,
    hasChatLoadError: boolean,
    displayMessages: ChatMessageData[],
): boolean {
    return !loadingChat && !hasChatLoadError && displayMessages.length === 0
}

export function resolveChatInputPlaceholder(options: ChatModeOptions): string {
    if (options.dynamicIterationActive) {
        return 'Ask Fleet to make a code change on this branch...'
    }

    if (options.allowGenerateWorkItems) {
        return 'Ask about this project, or describe work for Fleet to plan...'
    }

    return 'Ask Fleet across your workspace...'
}

export function resolveChatPromptStarters(
    suggestions: string[] | undefined,
    options: ChatModeOptions,
): string[] {
    const fallbackSuggestions = options.dynamicIterationActive
        ? DYNAMIC_ITERATION_PROMPT_STARTERS
        : options.allowGenerateWorkItems
            ? PROJECT_PROMPT_STARTERS
            : GLOBAL_PROMPT_STARTERS
    const orderedSuggestions = options.dynamicIterationActive
        ? [...fallbackSuggestions, ...(suggestions ?? [])]
        : [...(suggestions ?? []), ...fallbackSuggestions]
    const seen = new Set<string>()
    const starters: string[] = []

    for (const suggestion of orderedSuggestions) {
        const normalized = suggestion.trim()
        const key = normalized.toLocaleLowerCase()
        if (!normalized || seen.has(key)) {
            continue
        }

        seen.add(key)
        starters.push(normalized)
        if (starters.length >= MAX_PROMPT_STARTERS) {
            break
        }
    }

    return starters
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
