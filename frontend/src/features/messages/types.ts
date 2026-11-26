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
  lastMessageAt: string
  messageCount: number
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
}
