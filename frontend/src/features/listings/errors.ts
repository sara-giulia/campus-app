import type { ApiError } from '../../api/errors'
import { translate } from '../../i18n/store'

// Maps API error codes to field-level and form-level messages for the listing
// forms. Codes come from Listing.Create / UpdateDetails in the domain and from
// the suggestion endpoints. Anything not placed on a field becomes a top-of-form
// alert (same pattern as RegisterPage / MyProfilePage).

export interface FieldErrors {
  title?: string
  description?: string
  price?: string
  category?: string
  condition?: string
}

export interface PlacedError {
  fields: FieldErrors
  form: string | null
}

export function placeListingError(error: ApiError): PlacedError {
  switch (error.code) {
    case 'Listing.TitleEmpty':
      return { fields: { title: translate('listings.error.TitleEmpty') }, form: null }
    case 'Listing.TitleTooLong':
      return { fields: { title: translate('listings.error.TitleTooLong') }, form: null }
    case 'Listing.DescriptionTooLong':
      return { fields: { description: translate('listings.error.DescriptionTooLong') }, form: null }
    case 'Listing.CategoryInvalid':
      return { fields: { category: translate('listings.error.CategoryInvalid') }, form: null }
    case 'Listing.CategoryInactive':
      return { fields: { category: translate('listings.error.CategoryInactive') }, form: null }
    case 'Listing.CategoryRequired':
      return { fields: { category: translate('listings.error.CategoryRequired') }, form: null }
    case 'Listing.ConditionRequired':
      return { fields: { condition: translate('listings.error.ConditionRequired') }, form: null }
    case 'Listing.ConditionInvalid':
      return { fields: { condition: translate('listings.error.ConditionInvalid') }, form: null }
    case 'Money.Negative':
    case 'Money.TooManyDecimals':
    case 'Money.OutOfRange':
    case 'Money.Invalid':
      return { fields: { price: translate('listings.error.Money') }, form: null }
    case 'Listing.NotExpired':
      return { fields: {}, form: translate('listings.error.NotExpired') }
    case 'Listing.Sold':
      return { fields: {}, form: translate('listings.error.Sold') }
    case 'Listing.NotFound':
      return { fields: {}, form: translate('listings.error.NotFound') }
    case 'Listing.NotDraft':
      return { fields: {}, form: translate('listings.error.NotDraft') }
    case 'Listing.NotOwner':
      return { fields: {}, form: translate('listings.error.NotOwner') }
    case 'Network.Unreachable':
      return { fields: {}, form: translate('common.networkUnreachable') }
    default:
      return { fields: {}, form: error.message }
  }
}

export function placeReservationError(error: ApiError): string {
  switch (error.code) {
    case 'Reservation.NotFound':
      return translate('listings.error.ReservationNotFound')
    case 'Listing.NotFound':
      return translate('listings.error.NotFound')
    case 'Reservation.NotAllowed':
      return translate('listings.error.ReservationNotAllowed')
    case 'Reservation.NotActive':
      return translate('listings.error.ReservationNotActive')
    case 'Reservation.Expired':
      return translate('listings.error.ReservationExpired')
    case 'Listing.NotOwner':
      return translate('listings.error.NotSeller')
    case 'Network.Unreachable':
      return translate('common.networkUnreachable')
    default:
      return error.message
  }
}
