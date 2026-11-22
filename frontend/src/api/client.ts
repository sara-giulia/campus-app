import { API_PREFIX, API_URL } from './config'
import { ApiError, networkError, toApiError } from './errors'
import { getTokens, setTokens, type Tokens } from './tokens'

// The one place that talks to the API. Every page uses api.get/post/put/delete:
// - adds "Authorization: Bearer <token>" when the user is logged in;
// - refreshes the access token a bit before it expires (it lives 15 minutes);
// - if the API still answers 401, refreshes once and repeats the request;
// - if the refresh fails too, logs the user out (the tokens are cleared, AuthProvider sees it);
// - turns error answers into ApiError { status, code, message }.

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE'

export interface RequestOptions {
  body?: unknown
  // false for login/register/refresh: no token, no refresh on 401.
  auth?: boolean
  signal?: AbortSignal
}

// Refresh this long before the token expires, so a request does not leave with a token
// that expires on the way.
const REFRESH_MARGIN_MS = 30_000

let refreshInFlight: Promise<boolean> | null = null

function expiresSoon(tokens: Tokens): boolean {
  return new Date(tokens.expiresAt).getTime() - Date.now() < REFRESH_MARGIN_MS
}

// Several requests can get a 401 at the same time; they all wait for the same refresh,
// because the API gives a new refresh token each time and the old one stops working.
export function refreshTokens(): Promise<boolean> {
  refreshInFlight ??= doRefresh().finally(() => {
    refreshInFlight = null
  })
  return refreshInFlight
}

async function doRefresh(): Promise<boolean> {
  const current = getTokens()
  if (!current) return false

  let response: Response
  try {
    response = await fetch(`${API_URL}${API_PREFIX}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: current.refreshToken }),
    })
  } catch {
    // API unreachable: keep the tokens, the user is not logged out because of a network error.
    return false
  }

  if (!response.ok) {
    setTokens(null)
    return false
  }

  setTokens((await response.json()) as Tokens)
  return true
}

async function send(method: Method, path: string, options: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  // A FormData body (file upload) sets its own multipart Content-Type with the boundary.
  const isForm = options.body instanceof FormData
  if (options.body !== undefined && !isForm) headers['Content-Type'] = 'application/json'
  let body: BodyInit | undefined
  if (isForm) body = options.body as FormData
  else if (options.body !== undefined) body = JSON.stringify(options.body)

  if (options.auth !== false) {
    const tokens = getTokens()
    if (tokens) headers.Authorization = `Bearer ${tokens.token}`
  }

  try {
    return await fetch(`${API_URL}${API_PREFIX}${path}`, {
      method,
      headers,
      body,
      signal: options.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw networkError()
  }
}

export async function request<T>(method: Method, path: string, options: RequestOptions = {}): Promise<T> {
  const useAuth = options.auth !== false

  if (useAuth) {
    const tokens = getTokens()
    if (tokens && expiresSoon(tokens)) await refreshTokens()
  }

  let response = await send(method, path, options)

  if (response.status === 401 && useAuth && getTokens()) {
    if (await refreshTokens()) response = await send(method, path, options)
  }

  if (!response.ok) throw await toApiError(response)

  // 201 without a body, 204 No Content
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string, options?: RequestOptions) => request<T>('GET', path, options),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) => request<T>('POST', path, { ...options, body }),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) => request<T>('PUT', path, { ...options, body }),
  delete: <T>(path: string, options?: RequestOptions) => request<T>('DELETE', path, options),
}

export { ApiError }
