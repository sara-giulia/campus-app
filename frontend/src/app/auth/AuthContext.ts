import { createContext, useContext } from 'react'
import type { CurrentUser } from '../../api/jwt'

export interface AuthState {
  // null when nobody is logged in
  user: CurrentUser | null
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)

// The logged-in user and login/logout, from any page: const { user, logout } = useAuth()
export function useAuth(): AuthState {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>.')
  return context
}
