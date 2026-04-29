import type { ChatDynamicOptions, ChatSessionData } from '../../models'

export function upsertOptimisticChatSession(
    current: ChatSessionData[],
    session: ChatSessionData,
): ChatSessionData[] {
    const existingIndex = current.findIndex((candidate) => candidate.id === session.id)
    if (existingIndex < 0) {
        return [session, ...current]
    }

    return current.map((candidate, index) => index === existingIndex ? session : candidate)
}

export function removeOptimisticSessionsPresentOnServer(
    optimisticSessions: ChatSessionData[],
    serverSessions: ChatSessionData[],
): ChatSessionData[] {
    const serverSessionIds = new Set(serverSessions.map((session) => session.id))
    return optimisticSessions.filter((session) => !serverSessionIds.has(session.id))
}

export function mergeOptimisticChatSessions(
    serverSessions: ChatSessionData[],
    optimisticSessions: ChatSessionData[],
): ChatSessionData[] {
    return [
        ...removeOptimisticSessionsPresentOnServer(optimisticSessions, serverSessions),
        ...serverSessions,
    ]
}

export function applyDynamicOptionsToChatSession(
    session: ChatSessionData,
    dynamicOptions?: ChatDynamicOptions,
): ChatSessionData {
    if (!dynamicOptions) {
        return session
    }

    if (!dynamicOptions.enabled) {
        return {
            ...session,
            isDynamicIterationEnabled: false,
            dynamicIterationBranch: null,
            dynamicIterationPolicyJson: null,
            dynamicOptions: {
                enabled: false,
                branchName: null,
                strategy: null,
            },
            dynamicPolicy: null,
        }
    }

    const strategy = dynamicOptions.strategy ?? 'balanced'
    const branchName = dynamicOptions.branchName?.trim() || null
    const dynamicIterationPolicyJson = JSON.stringify({ executionPolicy: strategy })

    return {
        ...session,
        isDynamicIterationEnabled: true,
        dynamicIterationBranch: branchName,
        dynamicIterationPolicyJson,
        dynamicOptions: {
            enabled: true,
            branchName,
            strategy,
        },
        dynamicPolicy: {
            executionPolicy: strategy,
        },
    }
}
