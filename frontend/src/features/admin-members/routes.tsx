import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { AdminMembersPage } from './AdminMembersPage'

export const adminMembersRoutes: RouteObject[] = [
  { path: 'admin/members', element: <RequireAuth role="Admin"><AdminMembersPage /></RequireAuth> },
]
