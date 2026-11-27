// Shapes of GET /api/v1/moderation/members. Mirrors
// src/CampusMarket.Api/Features/Moderation/GetReportedMembers/GetReportedMembersResponse.cs.

export interface ReportedMemberReport {
  id: string
  reporterId: string
  reason: string
  // Set when the report was sent from a conversation between the two members.
  conversationId: string | null
  createdAt: string
}

export interface ReportedMemberItem {
  memberId: string
  name: string
  email: string
  // Only the pending reports; a member is in the list only while they have some.
  reportCount: number
  lastReportedAt: string
  // Their listings an Admin already hid.
  hiddenListingsCount: number
  isSuspended: boolean
  // null with isSuspended = a permanent suspension.
  suspendedUntil: string | null
  // Newest first.
  reports: ReportedMemberReport[]
}

export interface GetReportedMembersResponse {
  items: ReportedMemberItem[]
  count: number
  totalCount: number
  page: number
  pageSize: number
}

// Body of POST /moderation/members/{id}/suspend. until: an ISO moment in UTC; null = permanent.
export interface SuspendMemberRequest {
  reason: string
  until: string | null
}
