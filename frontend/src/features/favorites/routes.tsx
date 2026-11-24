import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { MyFavoritesPage } from './MyFavoritesPage'

export const favoritesRoutes: RouteObject[] = [
  { path: 'my/favorites', element: <RequireAuth><MyFavoritesPage /></RequireAuth> },
]
