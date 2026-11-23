import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { PriceAccuracyPage } from './PriceAccuracyPage'

export const adminMlRoutes: RouteObject[] = [
  { path: 'admin/price-accuracy', element: <RequireAuth role="Admin"><PriceAccuracyPage /></RequireAuth> },
]
