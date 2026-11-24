import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { AdminCategoriesPage } from './AdminCategoriesPage'

export const adminCategoriesRoutes: RouteObject[] = [
  { path: 'admin/categories', element: <RequireAuth role="Admin"><AdminCategoriesPage /></RequireAuth> },
]
