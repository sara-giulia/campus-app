import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { ForgotPasswordPage } from './ForgotPasswordPage'
import { LogoutPage } from './LogoutPage'
import { RegisterPage } from './RegisterPage'
import { ResetPasswordPage } from './ResetPasswordPage'

export const authRoutes: RouteObject[] = [
  { path: 'register', element: <RegisterPage /> },
  { path: 'forgot-password', element: <ForgotPasswordPage /> },
  { path: 'reset-password', element: <ResetPasswordPage /> },
  { path: 'logout', element: <RequireAuth><LogoutPage /></RequireAuth> },
]
