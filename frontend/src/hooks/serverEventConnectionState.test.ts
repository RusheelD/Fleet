import { describe, expect, it } from 'vitest'
import {
    buildServerEventConnectionDetail,
    cacheServerEventConnection,
    getCachedServerEventConnection,
    getServerEventConnectionCacheKey,
    normalizeServerEventProjectId,
    resolveConnectionAwarePollingInterval,
} from './serverEventConnectionState'

describe('server event connection state helpers', () => {
    it('normalizes project identifiers for cache keys', () => {
        expect(normalizeServerEventProjectId(undefined)).toBeNull()
        expect(normalizeServerEventProjectId('   ')).toBeNull()
        expect(normalizeServerEventProjectId('  project-123  ')).toBe('project-123')
        expect(getServerEventConnectionCacheKey()).toBe('__global__')
        expect(getServerEventConnectionCacheKey(' project-123 ')).toBe('project-123')
    })

    it('returns connecting by default and reuses cached live state for later consumers', () => {
        expect(getCachedServerEventConnection('project-abc')).toMatchObject({
            projectId: 'project-abc',
            state: 'connecting',
        })

        const cached = cacheServerEventConnection(buildServerEventConnectionDetail(
            'project-abc',
            'live',
            '2026-04-07T11:00:00.000Z',
        ))

        expect(cached).toEqual({
            projectId: 'project-abc',
            state: 'live',
            updatedAtUtc: '2026-04-07T11:00:00.000Z',
        })

        expect(getCachedServerEventConnection(' project-abc ')).toEqual(cached)
    })

    it('uses safety-net polling while SSE is live and respects explicit overrides', () => {
        expect(resolveConnectionAwarePollingInterval('live', 8000)).toBe(30_000)
        expect(resolveConnectionAwarePollingInterval('connecting', 8000)).toBe(8000)
        expect(resolveConnectionAwarePollingInterval('reconnecting', 8000)).toBe(8000)
        expect(resolveConnectionAwarePollingInterval('live', 8000, 2000)).toBe(2000)
        expect(resolveConnectionAwarePollingInterval('live', 8000, false)).toBe(false)
    })
})
