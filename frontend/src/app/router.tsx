import { createBrowserRouter, type RouteObject } from 'react-router'
import { authRoutes } from '../features/auth/routes'
import { favoritesRoutes } from '../features/favorites/routes'
import { listingsRoutes } from '../features/listings/routes'
import { messagesRoutes } from '../features/messages/routes'
import { moderationRoutes } from '../features/moderation/routes'
import { notificationsRoutes } from '../features/notifications/routes'
import { profileRoutes } from '../features/profile/routes'
import { reviewsRoutes } from '../features/reviews/routes'
import { adminCategoriesRoutes } from '../features/admin-categories/routes'
import { adminMlRoutes } from '../features/admin-ml/routes'
import { Layout } from './Layout'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { ErrorPage } from './pages/ErrorPage'

// Every feature keeps its pages in frontend/src/features/<feature>/ and exports
// its routes from a routes.tsx file, for example:
//   export const reservationsRoutes: RouteObject[] = [
//     { path: 'reservations', element: <RequireAuth><MyReservationsPage /></RequireAuth> },
//   ]
// Then add it to featureRoutes below: one import and one line, nothing else changes.
const featureRoutes: RouteObject[] = [
  ...authRoutes,
  ...profileRoutes,
  ...listingsRoutes,
  ...messagesRoutes,
  ...favoritesRoutes,
  ...reviewsRoutes,
  ...moderationRoutes,
  ...adminCategoriesRoutes,
  ...adminMlRoutes,
  ...notificationsRoutes,
]

export const router = createBrowserRouter([
  {
    path: '/',
    element: <Layout />,
    errorElement: <ErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'login', element: <LoginPage /> },
      ...featureRoutes,
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
