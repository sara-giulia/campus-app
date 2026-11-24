import { createContext, useContext, useEffect, useRef } from 'react'
import type { LiveEvent } from './liveEvents'

export type LiveEventListener = (event: LiveEvent) => void

export interface LiveEventsState {
  // True while the stream is open. Pages that used to ask again every few seconds only do that
  // when it is false (the API is unreachable or the browser cannot keep the connection).
  connected: boolean
  subscribe: (listener: LiveEventListener) => () => void
}

export const LiveEventsContext = createContext<LiveEventsState | null>(null)

export function useLiveEventsState(): LiveEventsState {
  const context = useContext(LiveEventsContext)
  if (!context) throw new Error('useLiveEvents must be used inside <LiveEventsProvider>.')
  return context
}

// Calls onEvent for every live event while the component is on the page. The latest onEvent is
// used, so it can read the current state without subscribing again on every render.
export function useLiveEvents(onEvent: LiveEventListener): boolean {
  const { connected, subscribe } = useLiveEventsState()
  const handler = useRef(onEvent)

  useEffect(() => {
    handler.current = onEvent
  })

  useEffect(() => subscribe((event) => handler.current(event)), [subscribe])

  return connected
}
