import { ApiError } from '../api/errors'
import { hasKey, translate } from '../i18n/store'

// Text for the error codes the pages can get, in the current language: the dictionaries
// have one "error.<code>" key per code (see i18n/ro.ts and en.ts).
// Anything else falls back to the API's own message.
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const key = `error.${error.code}`
    return hasKey(key) ? translate(key) : error.message
  }
  return translate('common.unexpectedError')
}

export function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}
