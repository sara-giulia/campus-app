import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useAuth } from '../../app/auth/AuthContext'
import { isAbort } from '../../app/errorMessage'
import { LiveEventsContext, type LiveEventListener, type LiveEventsState } from './LiveEventsContext'
import { readLiveEvents } from './liveEvents'

// Wait before connecting again after the stream closed: 2 s, doubled each time up to 30 s, and
// back to 2 s once a connection worked.
const FIRST_RETRY_MS = 2000
const MAX_RETRY_MS = 30_000

// Keeps one stream open for the logged-in user and hands its events to every page that listens
// (the notifications badge, the conversation page, the messages list).
export function LiveEventsProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const [connected, setConnected] = useState(false)
  const listeners = useRef(new Set<LiveEventListener>())

  const subscribe = useCallback((listener: LiveEventListener) => {
    listeners.current.add(listener)
    return () => {
      listeners.current.delete(listener)
    }
  }, [])

  const userId = user?.id
  useEffect(() => {
    if (!userId) return
    const controller = new AbortController()
    void keepOpen(controller.signal, setConnected, (event) => {
      listeners.current.forEach((listener) => listener(event))
    })
    return () => {
      controller.abort()
      setConnected(false)
    }
  }, [userId])

  const value = useMemo<LiveEventsState>(() => ({ connected, subscribe }), [connected, subscribe])

  return <LiveEventsContext.Provider value={value}>{children}</LiveEventsContext.Provider>
}

// Connects, reads until the stream closes, waits, connects again, until the signal aborts
// (logout or the app closes).
async function keepOpen(
  signal: AbortSignal,
  setConnected: (connected: boolean) => void,
  onEvent: LiveEventListener,
): Promise<void> {
  let retryMs = FIRST_RETRY_MS
  while (!signal.aborted) {
    try {
      await readLiveEvents(() => {
        setConnected(true)
        retryMs = FIRST_RETRY_MS
      }, onEvent, signal)
    } catch (err) {
      if (isAbort(err)) return
      // API down or the token could not be refreshed: try again later.
    }
    setConnected(false)
    await wait(retryMs, signal)
    retryMs = Math.min(retryMs * 2, MAX_RETRY_MS)
  }
}

function wait(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    const timer = window.setTimeout(resolve, ms)
    signal.addEventListener('abort', () => {
      window.clearTimeout(timer)
      resolve()
    }, { once: true })
  })
}
