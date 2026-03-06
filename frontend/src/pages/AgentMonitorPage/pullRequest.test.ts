import { describe, expect, it, vi } from 'vitest'
import { openPullRequest, openPullRequestDiff, toPullRequestDiffUrl } from './pullRequest'

describe('openPullRequest', () => {
  it('opens the real PR URL in a new tab when present', () => {
    const opener = vi.fn()
    const didOpen = openPullRequest('https://github.com/org/repo/pull/123', opener)

    expect(didOpen).toBe(true)
    expect(opener).toHaveBeenCalledWith('https://github.com/org/repo/pull/123', '_blank', 'noopener,noreferrer')
  })

  it('does not open anything when no URL is available', () => {
    const opener = vi.fn()
    const didOpen = openPullRequest(undefined, opener)

    expect(didOpen).toBe(false)
    expect(opener).not.toHaveBeenCalled()
  })
})

describe('toPullRequestDiffUrl', () => {
  it('returns the PR files URL', () => {
    expect(toPullRequestDiffUrl('https://github.com/org/repo/pull/123')).toBe('https://github.com/org/repo/pull/123/files')
  })
})

describe('openPullRequestDiff', () => {
  it('opens the PR files URL in a new tab when present', () => {
    const opener = vi.fn()
    const didOpen = openPullRequestDiff('https://github.com/org/repo/pull/123', opener)

    expect(didOpen).toBe(true)
    expect(opener).toHaveBeenCalledWith('https://github.com/org/repo/pull/123/files', '_blank', 'noopener,noreferrer')
  })
})
