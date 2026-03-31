/** Base API error with status code and parsed body */
export class ApiError extends Error {
  status: number
  body: unknown

  constructor(status: number, body: unknown) {
    super(`API error ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }
}

function extractApiErrorMessage(body: unknown): string | undefined {
  if (typeof body === 'string') {
    const normalized = body.trim()
    return normalized.length > 0 ? normalized : undefined
  }

  if (!body || typeof body !== 'object') {
    return undefined
  }

  const record = body as Record<string, unknown>
  for (const key of ['detail', 'message', 'error_description', 'error']) {
    const value = record[key]
    if (typeof value === 'string' && value.trim().length > 0) {
      return value.trim()
    }
  }

  return undefined
}

export function getApiErrorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (error instanceof ApiError) {
    return extractApiErrorMessage(error.body) ?? `${fallback} (HTTP ${error.status})`
  }

  if (error instanceof Error) {
    const normalized = error.message.trim()
    return normalized.length > 0 ? normalized : fallback
  }

  if (typeof error === 'string') {
    const normalized = error.trim()
    return normalized.length > 0 ? normalized : fallback
  }

  return fallback
}

/**
 * Module-level token getter. Set by the auth provider at startup
 * so that proxy functions can acquire MSAL access tokens outside React.
 */
let tokenGetter: (() => Promise<string | undefined>) | undefined
const configuredApiOrigin = normalizeApiOrigin(import.meta.env.VITE_API_ORIGIN)

function normalizeApiOrigin(rawOrigin?: string): string {
  const normalized = (rawOrigin ?? '')
    .trim()
    .replace(/\/+$/, '')

  if (!normalized) {
    return ''
  }

  // In production we default to same-origin /api calls.
  // Ignore localhost overrides to avoid baking local targets into deployed builds.
  if (import.meta.env.PROD) {
    try {
      const parsed = new URL(normalized)
      const host = parsed.hostname.toLowerCase()
      if (host === 'localhost' || host === '127.0.0.1') {
        return ''
      }
    } catch {
      return ''
    }
  }

  return normalized
}

function resolveRequestUrl(url: string): string {
  if (!configuredApiOrigin || /^https?:\/\//i.test(url)) {
    return url
  }

  if (url.startsWith('/')) {
    return `${configuredApiOrigin}${url}`
  }

  return `${configuredApiOrigin}/${url}`
}

export function setTokenGetter(getter: () => Promise<string | undefined>): void {
  tokenGetter = getter
}

async function buildHeaders(): Promise<HeadersInit> {
  const headers: HeadersInit = { 'Content-Type': 'application/json' }
  const token = tokenGetter ? await tokenGetter() : undefined
  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }
  return headers
}

export async function fetchWithAuth(url: string, init?: RequestInit): Promise<Response> {
  const resolvedUrl = resolveRequestUrl(url)
  const headers = new Headers(init?.headers ?? undefined)
  const token = tokenGetter ? await tokenGetter() : undefined
  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  return fetch(resolvedUrl, {
    ...init,
    headers,
  })
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let body: unknown
    try {
      body = await response.json()
    } catch {
      body = await response.text()
    }
    throw new ApiError(response.status, body)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

/** HTTP GET */
export async function get<T>(url: string): Promise<T> {
  const response = await fetch(resolveRequestUrl(url), {
    method: 'GET',
    headers: await buildHeaders(),
  })
  return handleResponse<T>(response)
}

/** HTTP POST */
export async function post<T>(url: string, body?: unknown, init?: RequestInit): Promise<T> {
  const response = await fetch(resolveRequestUrl(url), {
    ...init,
    method: 'POST',
    headers: init?.headers ?? await buildHeaders(),
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  return handleResponse<T>(response)
}

/** HTTP PUT */
export async function put<T>(url: string, body?: unknown, init?: RequestInit): Promise<T> {
  const response = await fetch(resolveRequestUrl(url), {
    ...init,
    method: 'PUT',
    headers: init?.headers ?? await buildHeaders(),
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  return handleResponse<T>(response)
}

/** HTTP DELETE */
export async function del<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(resolveRequestUrl(url), {
    ...init,
    method: 'DELETE',
    headers: init?.headers ?? await buildHeaders(),
  })
  return handleResponse<T>(response)
}

/** HTTP POST with FormData (multipart/form-data for file uploads) */
export async function postForm<T>(url: string, formData: FormData, init?: RequestInit): Promise<T> {
  // Don't set Content-Type — browser generates the boundary automatically
  const headers: HeadersInit = {}
  const token = tokenGetter ? await tokenGetter() : undefined
  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }
  const response = await fetch(resolveRequestUrl(url), {
    ...init,
    method: 'POST',
    headers: init?.headers ?? headers,
    body: formData,
  })
  return handleResponse<T>(response)
}
