import type { NotificationItem } from './types'

// The API writes the notification texts in English, always in the same shapes (see
// ReserveListingHandler, ConfirmSaleHandler, ExpireReservationsHandler, ExpireListingsHandler, AddReviewHandler,
// and Features/Offers/OfferNotifications.cs). The page shows them in Romanian; an unexpected text is shown as it came.
const PATTERNS: { type: NotificationItem['type']; match: RegExp; text: (m: RegExpMatchArray) => string }[] = [
  { type: 'ReservationCreated', match: /^(.+) reserved (.+)$/, text: (m) => `${m[1]} a rezervat „${m[2]}”.` },
  { type: 'ListingSold', match: /^(.+) was sold to you$/, text: (m) => `Vânzarea pentru „${m[1]}” a fost confirmată. Anunțul e al tău.` },
  { type: 'ReservationExpired', match: /^Your reservation for (.+) expired$/, text: (m) => `Rezervarea ta pentru „${m[1]}” a expirat.` },
  { type: 'ReviewReceived', match: /^(.+) rated (.+) ([1-5])\/5$/, text: (m) => `${m[1]} ți-a lăsat o recenzie de ${m[3]} ${m[3] === '1' ? 'stea' : 'stele'} pentru „${m[2]}”.` },
  { type: 'ListingExpired', match: /^Your listing (.+) expired$/, text: (m) => `Anunțul tău „${m[1]}” a expirat și nu mai apare în căutare. Îl poți reînnoi din „Anunțurile mele”.` },
  { type: 'OfferReceived', match: /^(.+?) offered ([\d.]+) lei for (.+)$/, text: (m) => `${m[1]} ți-a oferit ${m[2]} lei pentru „${m[3]}”.` },
  { type: 'OfferCountered', match: /^(.+?) countered with ([\d.]+) lei for (.+)$/, text: (m) => `${m[1]} ți-a făcut o contraofertă de ${m[2]} lei pentru „${m[3]}”.` },
  { type: 'OfferAccepted', match: /^You accepted the offer of ([\d.]+) lei for (.+)$/, text: (m) => `Ai acceptat oferta de ${m[1]} lei pentru „${m[2]}”. Anunțul e rezervat la acest preț.` },
  { type: 'OfferAccepted', match: /^(.+?) accepted your offer of ([\d.]+) lei for (.+)$/, text: (m) => `${m[1]} ți-a acceptat oferta de ${m[2]} lei pentru „${m[3]}”. Anunțul e rezervat la acest preț.` },
  { type: 'OfferRejected', match: /^The offer of ([\d.]+) lei for (.+) was closed: the listing was reserved by another buyer$/, text: (m) => `Oferta de ${m[1]} lei pentru „${m[2]}” s-a închis: anunțul a fost rezervat de alt cumpărător.` },
  { type: 'OfferRejected', match: /^(.+?) rejected your offer of ([\d.]+) lei for (.+)$/, text: (m) => `${m[1]} ți-a refuzat oferta de ${m[2]} lei pentru „${m[3]}”.` },
]

export function messageText(notification: NotificationItem): string {
  for (const pattern of PATTERNS) {
    if (pattern.type !== notification.type) continue
    const m = pattern.match.exec(notification.message)
    if (m) return pattern.text(m)
  }
  return notification.message
}
