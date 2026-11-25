import type { NotificationItem } from './types'

// The API writes the notification texts in English, always in the same four shapes (see
// ReserveListingHandler, ConfirmSaleHandler, ExpireReservationsHandler, ExpireListingsHandler). The page shows them in
// Romanian; an unexpected text is shown as it came.
const PATTERNS: { type: NotificationItem['type']; match: RegExp; text: (m: RegExpMatchArray) => string }[] = [
  { type: 'ReservationCreated', match: /^(.+) reserved (.+)$/, text: (m) => `${m[1]} a rezervat „${m[2]}”.` },
  { type: 'ListingSold', match: /^(.+) was sold to you$/, text: (m) => `Vânzarea pentru „${m[1]}” a fost confirmată. Anunțul e al tău.` },
  { type: 'ReservationExpired', match: /^Your reservation for (.+) expired$/, text: (m) => `Rezervarea ta pentru „${m[1]}” a expirat.` },
  { type: 'ListingExpired', match: /^Your listing (.+) expired$/, text: (m) => `Anunțul tău „${m[1]}” a expirat și nu mai apare în căutare. Îl poți reînnoi din „Anunțurile mele”.` },
]

export function messageText(notification: NotificationItem): string {
  for (const pattern of PATTERNS) {
    if (pattern.type !== notification.type) continue
    const m = pattern.match.exec(notification.message)
    if (m) return pattern.text(m)
  }
  return notification.message
}
