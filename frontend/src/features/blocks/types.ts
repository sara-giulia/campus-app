// Shapes of GET /members/me/blocked (src/CampusMarket.Api/Features/Blocks/GetMyBlocked).

export interface BlockedMember {
  memberId: string
  name: string
  blockedAt: string
}

export interface GetMyBlockedResponse {
  items: BlockedMember[]
}
