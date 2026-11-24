import type { RouteObject } from 'react-router'
import { RequireAuth } from '../../app/auth/RequireAuth'
import { BrowsePage } from './BrowsePage'
import { CreateListingPage } from './CreateListingPage'
import { EditListingPage } from './EditListingPage'
import { ListingPage } from './ListingPage'
import { MyListingsPage } from './MyListingsPage'
import { MyReservationsPage } from './MyReservationsPage'

export const listingsRoutes: RouteObject[] = [
  { path: 'listings/new', element: <RequireAuth><CreateListingPage /></RequireAuth> },
  { path: 'my/listings', element: <RequireAuth><MyListingsPage /></RequireAuth> },
  { path: 'my/listings/:id/edit', element: <RequireAuth><EditListingPage /></RequireAuth> },
  { path: 'my/reservations', element: <RequireAuth><MyReservationsPage /></RequireAuth> },
  { path: 'listings', element: <BrowsePage /> },
  { path: 'listings/:id', element: <ListingPage /> },
]
