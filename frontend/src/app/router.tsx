import { createBrowserRouter, type RouteObject } from 'react-router'
import { Layout } from './Layout'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'

// Every feature keeps its pages in frontend/src/features/<feature>/ and exports
// its routes from a routes.tsx file, for example:
//   export const reservationsRoutes: RouteObject[] = [
//     { path: 'reservations', element: <RequireAuth><MyReservationsPage /></RequireAuth> },
//   ]
// Then add it to featureRoutes below: one import and one line, nothing else changes.
const featureRoutes: RouteObject[] = [
]

export const router = createBrowserRouter([
  {
    path: '/',
    element: <Layout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'login', element: <LoginPage /> },
      ...featureRoutes,
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
