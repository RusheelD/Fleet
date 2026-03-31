import { describe, expect, it } from 'vitest'
import { ApiError, getApiErrorMessage } from './proxy'

describe('getApiErrorMessage', () => {
  it('prefers ProblemDetails detail values from ApiError bodies', () => {
    const error = new ApiError(409, {
      title: 'Conflict',
      detail: 'GitHub connection is no longer valid. Please re-link your GitHub account.',
    })

    expect(getApiErrorMessage(error, 'Fallback message.'))
      .toBe('GitHub connection is no longer valid. Please re-link your GitHub account.')
  })

  it('falls back to the provided message when the ApiError body has no readable detail', () => {
    const error = new ApiError(500, { traceId: 'abc123' })

    expect(getApiErrorMessage(error, 'Unable to load repositories.'))
      .toBe('Unable to load repositories. (HTTP 500)')
  })
})
