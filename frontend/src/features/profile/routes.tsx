import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { MyProfilePage } from './MyProfilePage'

export const profileRoutes: RouteObject[] = [
  { path: 'profile', element: <RequireAuth><MyProfilePage /></RequireAuth> },
]
