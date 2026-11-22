import type { RouteObject } from 'react-router'
import { SellerProfilePage } from './SellerProfilePage'

// Public: anyone can see a seller's page.
export const reviewsRoutes: RouteObject[] = [
  { path: 'members/:id', element: <SellerProfilePage /> },
]
