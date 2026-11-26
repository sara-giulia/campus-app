import { errorMessage } from '../../app/errorMessage'
import { ApiError } from '../../api/errors'

// Romanian text for the Offer.* codes (Domain/Offers/Offer.cs and the Features/Offers handlers).
// Any other code goes through the shared errorMessage.
const MESSAGES: Record<string, string> = {
  'Offer.NotFound': 'Oferta nu mai există.',
  'Offer.OwnListing': 'Nu poți face o ofertă la propriul anunț.',
  'Offer.ListingNotActive': 'Anunțul nu mai este activ, așa că nu mai primește oferte.',
  'Offer.AmountInvalid': 'Suma oferită trebuie să fie mai mare decât 0.',
  'Offer.AmountTooHigh': 'Suma oferită este prea mare.',
  'Offer.AlreadyPending': 'Există deja o ofertă în așteptare pentru acest anunț.',
  'Offer.NotPending': 'Oferta a primit deja un răspuns.',
  'Offer.NotRecipient': 'Doar celălalt participant poate răspunde la această ofertă.',
  'Offer.NotParticipant': 'Nu faci parte din această negociere.',
  'Offer.NotBuyer': 'Doar cumpărătorul își poate retrage oferta.',
  'Offer.NotOwnOffer': 'Poți retrage doar oferta ta. Contraoferta vânzătorului o poți refuza.',
  'Offer.BuyerReservationLimit': 'Cumpărătorul are deja numărul maxim de rezervări active.',
}

export function offerErrorMessage(error: unknown): string {
  if (error instanceof ApiError && MESSAGES[error.code]) return MESSAGES[error.code]
  return errorMessage(error)
}
