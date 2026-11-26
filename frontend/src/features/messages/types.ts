import type { Offer } from '../offers/types'

// Shapes of the conversation endpoints (src/CampusMarket.Api/Features/Conversations).

export interface MyConversationItem {
  id: string
  listingId: string
  listingTitle: string
  otherMemberId: string
  otherMemberName: string
  lastMessage: string
  lastMessageSenderId: string
  // The spam check flagged the last message (see ConversationMessage.isSuspicious).
  lastMessageIsSuspicious: boolean
  lastMessageAt: string
  messageCount: number
  // The messages of the other member I have not read yet (0 = read).
  unreadCount: number
}

export interface GetMyConversationsResponse {
  items: MyConversationItem[]
  count: number
}

export interface ConversationMessage {
  id: string
  number: number
  senderId: string
  text: string
  sentAt: string
  // The spam check flagged it (a link, money or card data asked for…): shown with a warning.
  isSuspicious: boolean
}

export interface ConversationDetail {
  id: string
  listingId: string
  listingTitle: string
  sellerId: string
  sellerName: string
  buyerId: string
  buyerName: string
  createdAt: string
  messages: ConversationMessage[]
  // The price offers between the two members on this listing, the oldest first.
  offers: Offer[]
  // The messages of the other member not read yet; the page marks them read when it is above 0.
  unreadCount: number
}

// GET /members/me/conversations/unread: for the red badge on "Mesaje".
export interface UnreadMessages {
  unreadMessages: number
  unreadConversations: number
}
