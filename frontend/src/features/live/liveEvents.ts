import { openStream } from '../../api/client'

// One event from GET /api/v1/members/me/events (Server-Sent Events). It only says that something
// new is there; the page loads it again through the normal endpoints.
export interface LiveEvent {
  type: 'notification' | 'message' | 'ping'
  conversationId: string | null
}

// Opens the stream, calls onOpen once it is open, then onEvent for every event until the server
// or the network closes it, or the signal aborts. fetch instead of EventSource, because
// EventSource cannot send the Authorization header and the token must not go in the URL
// (it would end up in logs).
export async function readLiveEvents(
  onOpen: () => void,
  onEvent: (event: LiveEvent) => void,
  signal: AbortSignal,
): Promise<void> {
  const response = await openStream('/members/me/events', signal)
  if (!response.body) return
  onOpen()

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''
  for (;;) {
    const { value, done } = await reader.read()
    if (done) return
    buffer += value.replaceAll('\r\n', '\n')

    // An event ends with an empty line; keep the unfinished end for the next chunk.
    let end = buffer.indexOf('\n\n')
    while (end !== -1) {
      const event = parseEvent(buffer.slice(0, end))
      if (event) onEvent(event)
      buffer = buffer.slice(end + 2)
      end = buffer.indexOf('\n\n')
    }
  }
}

// "event: message\ndata: {...}" → the parsed data line; the API sends one data line per event.
export function parseEvent(block: string): LiveEvent | null {
  const data = block
    .split('\n')
    .find((line) => line.startsWith('data:'))
    ?.slice('data:'.length)
    .trim()
  if (!data) return null
  try {
    return JSON.parse(data) as LiveEvent
  } catch {
    return null
  }
}
