import type { ApiError } from '../../api/errors'

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
      return { fields: { title: 'Titlul este obligatoriu.' }, form: null }
    case 'Listing.TitleTooLong':
      return { fields: { title: 'Titlul poate avea maxim 100 de caractere.' }, form: null }
    case 'Listing.DescriptionTooLong':
      return { fields: { description: 'Descrierea poate avea maxim 1000 de caractere.' }, form: null }
    case 'Listing.CategoryInvalid':
      return { fields: { category: 'Alege o categorie validă.' }, form: null }
    case 'Listing.ConditionInvalid':
      return { fields: { condition: 'Alege o stare validă.' }, form: null }
    case 'Money.Negative':
    case 'Money.TooManyDecimals':
    case 'Money.OutOfRange':
    case 'Money.Invalid':
      return { fields: { price: 'Introdu un preț valid (număr pozitiv, maxim 2 zecimale).' }, form: null }
    case 'Listing.Sold':
      return { fields: {}, form: 'Un anunț vândut nu mai poate fi modificat.' }
    case 'Listing.NotFound':
      return { fields: {}, form: 'Anunțul nu a fost găsit.' }
    case 'Listing.NotOwner':
      return { fields: {}, form: 'Doar autorul anunțului poate face această acțiune.' }
    case 'Network.Unreachable':
      return { fields: {}, form: 'Serverul nu răspunde. Verifică dacă API-ul este pornit.' }
    default:
      return { fields: {}, form: error.message }
  }
}

export function placeReservationError(error: ApiError): string {
  switch (error.code) {
    case 'Reservation.NotFound':
      return 'Rezervarea nu a fost găsită.'
    case 'Listing.NotFound':
      return 'Anunțul nu a fost găsit.'
    case 'Reservation.NotAllowed':
      return 'Nu ai voie să faci această acțiune pe rezervarea aceasta.'
    case 'Reservation.NotActive':
      return 'Rezervarea nu mai este activă.'
    case 'Reservation.Expired':
      return 'Rezervarea a expirat.'
    case 'Listing.NotOwner':
      return 'Doar vânzătorul poate confirma vânzarea.'
    case 'Network.Unreachable':
      return 'Serverul nu răspunde. Verifică dacă API-ul este pornit.'
    default:
      return error.message
  }
}
