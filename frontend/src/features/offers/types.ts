import { translate } from '../../i18n/store'
// Shapes of the price offers (src/CampusMarket.Api/Features/Offers). They come with the conversation
// (GET /conversations/{id} → offers) and from the offer endpoints.

export type OfferStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Countered' | 'Withdrawn'

export interface Offer {
  id: string
  listingId: string
  buyerId: string
  sellerId: string
  // Who proposed this amount: the buyer, or the seller for a counter-offer.
  proposedById: string
  amount: number
  status: OfferStatus
  // The offer this one answers (a counter-offer); null for the buyer's first offer.
  parentOfferId: string | null
  createdAt: string
  respondedAt: string | null
}

// A getter per status, so the label follows the current language.
export const OFFER_STATUS_LABELS: Record<OfferStatus, string> = {
  get Pending() {
    return translate('offers.status.Pending')
  },
  get Accepted() {
    return translate('offers.status.Accepted')
  },
  get Rejected() {
    return translate('offers.status.Rejected')
  },
  get Countered() {
    return translate('offers.status.Countered')
  },
  get Withdrawn() {
    return translate('offers.status.Withdrawn')
  },
}
