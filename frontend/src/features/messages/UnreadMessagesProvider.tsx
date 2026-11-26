import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useLocation } from 'react-router'
import { api } from '../../api/client'
import { useAuth } from '../../app/auth/AuthContext'
import { isAbort } from '../../app/errorMessage'
import { useLiveEvents } from '../live/LiveEventsContext'
import type { UnreadMessages } from './types'
import { UnreadMessagesContext, type UnreadMessagesState } from './UnreadMessagesContext'

// Without the live stream the number is only asked again on navigation, at most once a minute.
const REFRESH_THROTTLE_MS = 60_000

// Keeps the number of unread messages for the badge on "Mesaje". A new message ("message") and a
// conversation read in another tab ("read") arrive on the live stream and load the number at once.
export function UnreadMessagesProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const location = useLocation()
  const [unreadMessages, setUnreadMessages] = useState<number | null>(null)
  const lastFetchAtRef = useRef(0)
  const controllerRef = useRef<AbortController | null>(null)

  const refresh = useCallback(() => {
    controllerRef.current?.abort()
    if (!user) return

    // A newer call wins: cancel the one still running.
    const controller = new AbortController()
    controllerRef.current = controller
    lastFetchAtRef.current = Date.now()

    api
      .get<UnreadMessages>('/members/me/conversations/unread', { signal: controller.signal })
      .then((data) => setUnreadMessages(data.unreadMessages))
      .catch((err: unknown) => {
        if (isAbort(err)) return
        // Keep the previous number on a transient error.
      })
  }, [user])

  useEffect(() => {
    if (!user) return
    if (lastFetchAtRef.current === 0 || Date.now() - lastFetchAtRef.current >= REFRESH_THROTTLE_MS) refresh()
  }, [user, location.pathname, refresh])

  useLiveEvents((event) => {
    if (event.type === 'message' || event.type === 'read') refresh()
  })

  useEffect(() => () => controllerRef.current?.abort(), [])

  const value = useMemo<UnreadMessagesState>(
    () => ({ unreadMessages: user ? unreadMessages : null, refresh }),
    [user, unreadMessages, refresh],
  )

  return <UnreadMessagesContext.Provider value={value}>{children}</UnreadMessagesContext.Provider>
}
