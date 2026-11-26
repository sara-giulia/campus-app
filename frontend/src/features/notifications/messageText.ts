import { translate } from '../../i18n/store'
import type { NotificationItem } from './types'

// The API writes the notification texts in English, always in the same shapes (see
// ReserveListingHandler, ConfirmSaleHandler, ExpireReservationsHandler, ExpireListingsHandler, AddReviewHandler,
// and Features/Offers/OfferNotifications.cs). The page shows them in the current language (the texts are in
// the dictionaries, "notifications.msg.*"); an unexpected text is shown as it came.
const PATTERNS: { type: NotificationItem['type']; match: RegExp; text: (m: RegExpMatchArray) => string }[] = [
  { type: 'ReservationCreated', match: /^(.+) reserved (.+)$/, text: (m) => translate('notifications.msg.reservationCreated', { name: m[1], title: m[2] }) },
  { type: 'ListingSold', match: /^(.+) was sold to you$/, text: (m) => translate('notifications.msg.listingSold', { title: m[1] }) },
  { type: 'ReservationExpired', match: /^Your reservation for (.+) expired$/, text: (m) => translate('notifications.msg.reservationExpired', { title: m[1] }) },
  { type: 'ReviewReceived', match: /^(.+) rated (.+) ([1-5])\/5$/, text: (m) => translate(m[3] === '1' ? 'notifications.msg.reviewOne' : 'notifications.msg.reviewMany', { name: m[1], title: m[2], stars: m[3] }) },
  { type: 'ListingExpired', match: /^Your listing (.+) expired$/, text: (m) => translate('notifications.msg.listingExpired', { title: m[1] }) },
  { type: 'OfferReceived', match: /^(.+?) offered ([\d.]+) lei for (.+)$/, text: (m) => translate('notifications.msg.offerReceived', { name: m[1], amount: m[2], title: m[3] }) },
  { type: 'OfferCountered', match: /^(.+?) countered with ([\d.]+) lei for (.+)$/, text: (m) => translate('notifications.msg.offerCountered', { name: m[1], amount: m[2], title: m[3] }) },
  { type: 'OfferAccepted', match: /^You accepted the offer of ([\d.]+) lei for (.+)$/, text: (m) => translate('notifications.msg.offerAcceptedByYou', { amount: m[1], title: m[2] }) },
  { type: 'OfferAccepted', match: /^(.+?) accepted your offer of ([\d.]+) lei for (.+)$/, text: (m) => translate('notifications.msg.offerAcceptedByOther', { name: m[1], amount: m[2], title: m[3] }) },
  { type: 'OfferRejected', match: /^The offer of ([\d.]+) lei for (.+) was closed: the listing was reserved by another buyer$/, text: (m) => translate('notifications.msg.offerClosed', { amount: m[1], title: m[2] }) },
  { type: 'OfferRejected', match: /^(.+?) rejected your offer of ([\d.]+) lei for (.+)$/, text: (m) => translate('notifications.msg.offerRejected', { name: m[1], amount: m[2], title: m[3] }) },
]

export function messageText(notification: NotificationItem): string {
  for (const pattern of PATTERNS) {
    if (pattern.type !== notification.type) continue
    const m = pattern.match.exec(notification.message)
    if (m) return pattern.text(m)
  }
  return notification.message
}
