// Keeps the tokens from POST /auth/login and POST /auth/refresh in localStorage,
// so the user stays logged in after a page refresh, and tells the rest of the app
// (AuthProvider) every time they change.

export interface Tokens {
  token: string
  expiresAt: string
  refreshToken: string
}

const STORAGE_KEY = 'campusmarket.tokens'

type Listener = (tokens: Tokens | null) => void
const listeners = new Set<Listener>()

export function getTokens(): Tokens | null {
  const raw = localStorage.getItem(STORAGE_KEY)
  if (!raw) return null
  try {
    const parsed = JSON.parse(raw) as Partial<Tokens>
    if (parsed.token && parsed.expiresAt && parsed.refreshToken) return parsed as Tokens
  } catch {
    // Broken value (edited by hand): treat it as logged out.
  }
  localStorage.removeItem(STORAGE_KEY)
  return null
}

export function setTokens(tokens: Tokens | null): void {
  if (tokens) localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens))
  else localStorage.removeItem(STORAGE_KEY)
  listeners.forEach((listener) => listener(tokens))
}

export function onTokensChange(listener: Listener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

// Login or logout in another tab of the same browser.
window.addEventListener('storage', (event) => {
  if (event.key === STORAGE_KEY) listeners.forEach((listener) => listener(getTokens()))
})
