import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { NotificationsPage } from './NotificationsPage'

export const notificationsRoutes: RouteObject[] = [
  { path: 'notifications', element: <RequireAuth><NotificationsPage /></RequireAuth> },
]
