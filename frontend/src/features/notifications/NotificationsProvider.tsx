import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useLocation } from 'react-router'
import { api } from '../../api/client'
import { useAuth } from '../../app/auth/AuthContext'
import { isAbort } from '../../app/errorMessage'
import { NotificationsContext, type NotificationsState } from './NotificationsContext'
import type { GetMyNotificationsResponse } from './types'

// Keep the badge quiet: never refetch faster than this, no matter how many route changes happen.
const REFRESH_THROTTLE_MS = 60_000

// Fetches the unread count only, by asking for the smallest page the API allows.
// Shared between the Layout badge and the NotificationsPage so one place keeps the number in sync.
export function NotificationsProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const location = useLocation()
  const [unreadCount, setUnreadCount] = useState<number | null>(null)
  const lastFetchAtRef = useRef<number>(0)
  const controllerRef = useRef<AbortController | null>(null)

  const doFetch = useCallback(() => {
    // Nothing to ask for when nobody is logged in. The badge reads a null count as "not logged in"
    // through the memoised value below, so we only cancel in-flight work here and return.
    if (!user) {
      controllerRef.current?.abort()
      return
    }

    // Cancel any in-flight call: a route change during a slow request must not overwrite a fresher one.
    controllerRef.current?.abort()
    const controller = new AbortController()
    controllerRef.current = controller
    lastFetchAtRef.current = Date.now()

    api
      .get<GetMyNotificationsResponse>('/members/me/notifications?page=1&pageSize=1', { signal: controller.signal })
      .then((data) => {
        setUnreadCount(data.unreadCount)
      })
      .catch((err: unknown) => {
        if (isAbort(err)) return
        // Leave the previous number in place on a transient error; the badge never has to be exact.
      })
  }, [user])

  const refresh = useCallback(() => {
    // "refresh" is the explicit call from the page (after marking read); skip the throttle.
    lastFetchAtRef.current = 0
    doFetch()
  }, [doFetch])

  // On login/logout and on every navigation, but no faster than once per REFRESH_THROTTLE_MS.
  useEffect(() => {
    if (!user) return
    const elapsed = Date.now() - lastFetchAtRef.current
    if (elapsed >= REFRESH_THROTTLE_MS || lastFetchAtRef.current === 0) {
      doFetch()
    }
  }, [user, location.pathname, doFetch])

  // Cancel on unmount.
  useEffect(() => () => controllerRef.current?.abort(), [])

  // When nobody is logged in the internal state is stale; expose null so the badge stays hidden.
  const value = useMemo<NotificationsState>(
    () => ({ unreadCount: user ? unreadCount : null, refresh }),
    [user, unreadCount, refresh],
  )

  return <NotificationsContext.Provider value={value}>{children}</NotificationsContext.Provider>
}
