import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { ReportedListingsPage } from './ReportedListingsPage'

export const moderationRoutes: RouteObject[] = [
  { path: 'admin/moderation', element: <RequireAuth role="Admin"><ReportedListingsPage /></RequireAuth> },
]
