import { describe, expect, it } from 'vitest'
import { normalizeChatSessionActivities, normalizeChatSessionActivity } from './chat'

describe('chat activity normalization', () => {
  it('fills in safe defaults for malformed activity payloads', () => {
    expect(normalizeChatSessionActivity({ kind: 'status', timestampUtc: '2026-04-03T00:00:00Z' }, 2)).toEqual({
      id: 'status-2026-04-03T00:00:00Z-2',
      kind: 'status',
      message: 'Session update',
      timestampUtc: '2026-04-03T00:00:00Z',
      toolName: null,
      succeeded: null,
    })
  })

  it('normalizes non-array activity collections to an empty list', () => {
    expect(normalizeChatSessionActivities(null)).toEqual([])
    expect(normalizeChatSessionActivities({})).toEqual([])
  })
})
