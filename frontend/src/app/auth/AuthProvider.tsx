import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { login as apiLogin, logout as apiLogout } from '../../api/auth'
import { readUser, type CurrentUser } from '../../api/jwt'
import { getTokens, onTokensChange } from '../../api/tokens'
import { AuthContext, type AuthState } from './AuthContext'

function userFromStorage(): CurrentUser | null {
  const tokens = getTokens()
  return tokens ? readUser(tokens.token) : null
}

// Knows who is logged in. It follows the stored tokens, so the user changes on login,
// on logout, when a refresh fails (the client clears the tokens) and in other tabs.
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(userFromStorage)

  useEffect(() => onTokensChange((tokens) => setUser(tokens ? readUser(tokens.token) : null)), [])

  const value = useMemo<AuthState>(() => ({ user, login: apiLogin, logout: apiLogout }), [user])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
