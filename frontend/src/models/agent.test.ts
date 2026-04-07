import { describe, expect, it } from 'vitest'
import { compareLogEntriesByTime, normalizeLogEntry, normalizeLogEntries } from './agent'

describe('agent log helpers', () => {
  it('normalizes incomplete log entries safely', () => {
    expect(normalizeLogEntry(undefined)).toEqual({
      time: '',
      agent: 'System',
      level: 'info',
      message: '',
      isDetailed: false,
      executionId: null,
    })
  })

  it('sorts logs safely even when timestamps are missing', () => {
    const logs = normalizeLogEntries([
      { message: 'missing timestamp' },
      { time: '2026-04-07T10:00:00.000Z', message: 'valid timestamp' },
    ]).sort(compareLogEntriesByTime)

    expect(logs).toHaveLength(2)
    expect(logs[0].message).toBe('missing timestamp')
    expect(logs[1].message).toBe('valid timestamp')
  })
})
