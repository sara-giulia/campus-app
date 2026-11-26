import { api } from '../../api/client'

// A random id the browser makes up for itself, kept in localStorage: it only tells two visitors who are
// not logged in apart, so a listing counts one view per visitor and day. It holds no personal data.
const VISITOR_KEY = 'campusmarket.visitorId'

function visitorId(): string {
  try {
    const saved = localStorage.getItem(VISITOR_KEY)
    if (saved) return saved
    const fresh = crypto.randomUUID()
    localStorage.setItem(VISITOR_KEY, fresh)
    return fresh
  } catch {
    // localStorage blocked: a new id for this page only.
    return crypto.randomUUID()
  }
}

// POST /listings/{id}/views: fire and forget, a failed count must never disturb the page.
export function recordListingView(listingId: string): void {
  api.post<void>(`/listings/${listingId}/views`, undefined, { headers: { 'X-Visitor-Id': visitorId() } }).catch(() => {})
}
