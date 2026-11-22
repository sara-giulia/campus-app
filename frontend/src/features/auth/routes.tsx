import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { LogoutPage } from './LogoutPage'
import { RegisterPage } from './RegisterPage'

export const authRoutes: RouteObject[] = [
  { path: 'register', element: <RegisterPage /> },
  { path: 'logout', element: <RequireAuth><LogoutPage /></RequireAuth> },
]
