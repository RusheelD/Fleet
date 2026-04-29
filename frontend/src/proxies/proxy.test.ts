import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, get, getApiErrorMessage, post, postForm, setTokenGetter } from './proxy'

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

describe('request helpers', () => {
  afterEach(() => {
    setTokenGetter(() => Promise.resolve(undefined))
    vi.unstubAllGlobals()
  })

  it('merges custom headers with auth and json content type for JSON requests', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true })))
    vi.stubGlobal('fetch', fetchMock)
    setTokenGetter(() => Promise.resolve('access-token'))

    await post('/api/example', { value: 1 }, {
      headers: {
        'X-Request-Id': 'request-1',
      },
    })

    const init = fetchMock.mock.calls[0][1] as RequestInit
    const headers = init.headers as Headers
    expect(headers.get('Authorization')).toBe('Bearer access-token')
    expect(headers.get('Content-Type')).toBe('application/json')
    expect(headers.get('X-Request-Id')).toBe('request-1')
  })

  it('preserves caller authorization headers instead of overwriting them', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true })))
    vi.stubGlobal('fetch', fetchMock)
    setTokenGetter(() => Promise.resolve('access-token'))

    await post('/api/example', { value: 1 }, {
      headers: {
        Authorization: 'Bearer caller-token',
      },
    })

    const init = fetchMock.mock.calls[0][1] as RequestInit
    const headers = init.headers as Headers
    expect(headers.get('Authorization')).toBe('Bearer caller-token')
  })

  it('adds auth to form posts without setting content type', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true })))
    vi.stubGlobal('fetch', fetchMock)
    setTokenGetter(() => Promise.resolve('access-token'))

    await postForm('/api/upload', new FormData(), {
      headers: {
        'X-Upload-Id': 'upload-1',
      },
    })

    const init = fetchMock.mock.calls[0][1] as RequestInit
    const headers = init.headers as Headers
    expect(headers.get('Authorization')).toBe('Bearer access-token')
    expect(headers.get('Content-Type')).toBeNull()
    expect(headers.get('X-Upload-Id')).toBe('upload-1')
  })

  it('returns undefined for successful empty responses', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(get('/api/empty')).resolves.toBeUndefined()
  })

  it('preserves non-json error response messages', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('plain failure', { status: 502 }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(get('/api/failure')).rejects.toMatchObject({
      status: 502,
      body: 'plain failure',
    })
  })
})
