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
  const response = await fetch(url, {
    method: 'GET',
    headers: await buildHeaders(),
  })
  return handleResponse<T>(response)
}

/** HTTP POST */
export async function post<T>(url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method: 'POST',
    headers: await buildHeaders(),
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  return handleResponse<T>(response)
}

/** HTTP PUT */
export async function put<T>(url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method: 'PUT',
    headers: await buildHeaders(),
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  return handleResponse<T>(response)
}

/** HTTP DELETE */
export async function del<T>(url: string): Promise<T> {
  const response = await fetch(url, {
    method: 'DELETE',
    headers: await buildHeaders(),
  })
  return handleResponse<T>(response)
}
