import { errorMessage } from '../../app/errorMessage'
import { ApiError } from '../../api/errors'
import { hasKey, translate } from '../../i18n/store'

// Text for the Offer.* codes (Domain/Offers/Offer.cs and the Features/Offers handlers), in the current
// language: the dictionaries have one "offers.error.<code>" key per code. Any other code goes through
// the shared errorMessage.
export function offerErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const key = `offers.error.${error.code}`
    if (hasKey(key)) return translate(key)
  }
  return errorMessage(error)
}
