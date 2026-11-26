// Shapes of the notifications API. Mirrors
// src/CampusMarket.Api/Features/Notifications/GetMyNotifications/GetMyNotificationsResponse.cs.

export type NotificationType =
  | 'ReservationCreated'
  | 'ListingSold'
  | 'ReservationExpired'
  | 'ListingExpired'
  | 'ReviewReceived'
  | 'OfferReceived'
  | 'OfferCountered'
  | 'OfferAccepted'
  | 'OfferRejected'

export interface NotificationItem {
  id: string
  type: NotificationType
  message: string
  listingId: string | null
  createdAt: string
  readAt: string | null
}

export interface GetMyNotificationsResponse {
  items: NotificationItem[]
  count: number
  totalCount: number
  unreadCount: number
  page: number
  pageSize: number
}
