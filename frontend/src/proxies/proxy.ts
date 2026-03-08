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

/**
 * Module-level token getter. Set by the auth provider at startup
 * so that proxy functions can acquire MSAL access tokens outside React.
 */
let tokenGetter: (() => Promise<string | undefined>) | undefined
const configuredApiOrigin = (import.meta.env.VITE_API_ORIGIN ?? '')
  .trim()
  .replace(/\/+$/, '')

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
export async function post<T>(url: string, body?: unknown): Promise<T> {
  const response = await fetch(resolveRequestUrl(url), {
    method: 'POST',
    headers: await buildHeaders(),
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  return handleResponse<T>(response)
}

/** HTTP PUT */
export async function put<T>(url: string, body?: unknown): Promise<T> {
  const response = await fetch(resolveRequestUrl(url), {
    method: 'PUT',
    headers: await buildHeaders(),
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  return handleResponse<T>(response)
}

/** HTTP DELETE */
export async function del<T>(url: string): Promise<T> {
  const response = await fetch(resolveRequestUrl(url), {
    method: 'DELETE',
    headers: await buildHeaders(),
  })
  return handleResponse<T>(response)
}

/** HTTP POST with FormData (multipart/form-data for file uploads) */
export async function postForm<T>(url: string, formData: FormData): Promise<T> {
  // Don't set Content-Type — browser generates the boundary automatically
  const headers: HeadersInit = {}
  const token = tokenGetter ? await tokenGetter() : undefined
  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }
  const response = await fetch(resolveRequestUrl(url), {
    method: 'POST',
    headers,
    body: formData,
  })
  return handleResponse<T>(response)
}
