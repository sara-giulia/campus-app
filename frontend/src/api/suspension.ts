import { setTokens } from './tokens'

// An Admin suspended the logged-in member. The API then answers 403 Auth.AccountSuspended on login,
// and 401 Auth.AccountSuspended on refresh and on any request that needs login with a token issued before.
// The client logs the user out (the tokens are cleared) and remembers why, so the login page can say it.

export const ACCOUNT_SUSPENDED = 'Auth.AccountSuspended'

let suspended = false
type Listener = () => void
const listeners = new Set<Listener>()

// Remembers the suspension before clearing the tokens, so the pages that react to the logout
// (RequireAuth sends to /login) already see it; the listeners (AccountSuspendedRedirect) run last,
// when the user is already logged out.
export function accountSuspended(): void {
  suspended = true
  setTokens(null)
  listeners.forEach((listener) => listener())
}

export function isAccountSuspended(): boolean {
  return suspended
}

// After a successful login the message is no longer needed.
export function clearAccountSuspended(): void {
  if (!suspended) return
  suspended = false
  listeners.forEach((listener) => listener())
}

export function onAccountSuspendedChange(listener: Listener): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

// True when an error answer carries the Auth.AccountSuspended code. Reads a copy of the body,
// so the answer can still be turned into an ApiError afterwards.
export async function isSuspendedResponse(response: Response): Promise<boolean> {
  try {
    const body = (await response.clone().json()) as { code?: string }
    return body.code === ACCOUNT_SUSPENDED
  } catch {
    return false
  }
}
