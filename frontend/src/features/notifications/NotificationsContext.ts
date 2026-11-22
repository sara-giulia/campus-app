import { createContext, useContext } from 'react'

export interface NotificationsState {
  // The latest unread count the API answered with, or null while we have not asked yet.
  unreadCount: number | null
  // Ask the API for the fresh count; the call is awaited inside the provider so the
  // caller may ignore the returned promise (it is a thin wrapper on the throttled fetch).
  refresh: () => void
}

export const NotificationsContext = createContext<NotificationsState | null>(null)

// The unread count and a "refresh" from any component: const { unreadCount, refresh } = useNotifications()
export function useNotifications(): NotificationsState {
  const context = useContext(NotificationsContext)
  if (!context) throw new Error('useNotifications must be used inside <NotificationsProvider>.')
  return context
}
