export type ServerEventConnectionState = 'connecting' | 'live' | 'reconnecting'

export interface ServerEventConnectionDetail {
    projectId?: string | null
    state: ServerEventConnectionState
    updatedAtUtc: string
}

const GLOBAL_SERVER_EVENT_CONNECTION_KEY = '__global__'
const connectionCache = new Map<string, ServerEventConnectionDetail>()

export function normalizeServerEventProjectId(projectId?: string | null): string | null {
    if (typeof projectId !== 'string') {
        return null
    }

    const trimmed = projectId.trim()
    return trimmed.length > 0 ? trimmed : null
}

export function buildServerEventConnectionDetail(
    projectId?: string | null,
    state: ServerEventConnectionState = 'connecting',
    updatedAtUtc: string = new Date().toISOString(),
): ServerEventConnectionDetail {
    return {
        projectId: normalizeServerEventProjectId(projectId),
        state,
        updatedAtUtc,
    }
}

export function getServerEventConnectionCacheKey(projectId?: string | null): string {
    return normalizeServerEventProjectId(projectId) ?? GLOBAL_SERVER_EVENT_CONNECTION_KEY
}

export function getCachedServerEventConnection(projectId?: string | null): ServerEventConnectionDetail {
    return connectionCache.get(getServerEventConnectionCacheKey(projectId))
        ?? buildServerEventConnectionDetail(projectId)
}

export function cacheServerEventConnection(detail: ServerEventConnectionDetail): ServerEventConnectionDetail {
    const normalized = buildServerEventConnectionDetail(detail.projectId, detail.state, detail.updatedAtUtc)
    connectionCache.set(getServerEventConnectionCacheKey(normalized.projectId), normalized)
    return normalized
}

export function resolveConnectionAwarePollingInterval(
    state: ServerEventConnectionState,
    fallbackInterval: number | false,
    liveInterval: number | false = false,
): number | false {
    return state === 'live' ? liveInterval : fallbackInterval
}
