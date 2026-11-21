import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import type { Role } from '../../api/jwt'
import { useAuth } from './AuthContext'

// Wrap a page that needs login: <RequireAuth><MyPage /></RequireAuth>
// or an Admin page: <RequireAuth role="Admin">...</RequireAuth>.
// Not logged in → /login (and back here after login). Wrong role → home page.
export function RequireAuth({ children, role }: { children: ReactNode; role?: Role }) {
  const { user } = useAuth()
  const location = useLocation()

  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  if (role && user.role !== role) return <Navigate to="/" replace />

  return children
}
