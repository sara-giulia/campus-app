import { createContext, useContext } from 'react'

export interface UnreadMessagesState {
  // The messages from the other members not read yet, or null while we have not asked (or nobody is logged in).
  unreadMessages: number | null
  // Ask the API again now (after the conversation page marked a conversation read).
  refresh: () => void
}

export const UnreadMessagesContext = createContext<UnreadMessagesState | null>(null)

// The red badge on "Mesaje" and the conversation page share the number: const { unreadMessages, refresh } = useUnreadMessages()
export function useUnreadMessages(): UnreadMessagesState {
  const context = useContext(UnreadMessagesContext)
  if (!context) throw new Error('useUnreadMessages must be used inside <UnreadMessagesProvider>.')
  return context
}
