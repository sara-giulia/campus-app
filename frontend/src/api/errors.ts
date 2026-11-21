// The API answers errors in two shapes:
// - handler errors: { code, message, type }            (400, 401 on login, 403, 404, 409)
// - ProblemDetails: { title, detail, status, code, ... } (401/403 without a body, 404 route, 429, 500)
// ApiError turns both into { status, code, message }, so pages only check error.code.
export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

interface ErrorBody {
  code?: string
  message?: string
  title?: string
  detail?: string
}

export async function toApiError(response: Response): Promise<ApiError> {
  let body: ErrorBody = {}
  try {
    body = (await response.json()) as ErrorBody
  } catch {
    // Empty or non-JSON body: keep only the status.
  }

  const code = body.code ?? `Http.${response.status}`
  const message = body.message ?? body.detail ?? body.title ?? `Request failed with status ${response.status}.`
  return new ApiError(response.status, code, message)
}

// When the API cannot be reached at all (not started, wrong URL, CORS refused).
export function networkError(): ApiError {
  return new ApiError(0, 'Network.Unreachable', 'The server cannot be reached. Is the API running?')
}
