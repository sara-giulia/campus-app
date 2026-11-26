import { ApiError } from '../api/errors'

// Romanian text for the error codes the browse, listing, messages and moderation pages can get.
// Anything else falls back to the API's own message.
const MESSAGES: Record<string, string> = {
  'Network.Unreachable': 'Serverul nu răspunde. Încearcă din nou peste puțin timp.',
  'Listing.NotFound': 'Anunțul nu există sau a fost șters.',
  'Listing.NotActive': 'Anunțul nu mai poate fi rezervat.',
  'Reservation.OwnListing': 'Nu îți poți rezerva propriul anunț.',
  'Favorite.OwnListing': 'Nu îți poți salva propriul anunț la favorite.',
  'Favorite.ListingNotAvailable': 'Anunțul nu mai e disponibil.',
  'Favorite.AlreadyExists': 'Anunțul e deja la favorite.',
  'Report.OwnListing': 'Nu îți poți raporta propriul anunț.',
  'Report.AlreadyExists': 'Ai raportat deja acest anunț.',
  'Report.ListingNotAvailable': 'Anunțul nu mai poate fi raportat.',
  'Report.ReasonEmpty': 'Scrie motivul raportării.',
  'Report.ReasonTooLong': 'Motivul e prea lung.',
  'Reservation.LimitReached': 'Ai atins numărul maxim de rezervări active.',
  'Listing.AlreadyHidden': 'Anunțul e deja ascuns.',
  'Listing.CannotHide': 'Doar anunțurile active sau rezervate pot fi ascunse.',
  'Conversation.OwnListing': 'Nu îți poți scrie singur despre propriul anunț.',
  'Conversation.ListingNotAvailable': 'Anunțul nu mai e disponibil pentru o conversație nouă.',
  'Conversation.NotParticipant': 'Nu faci parte din această conversație.',
  'Conversation.NotFound': 'Conversația nu există.',
  'Message.Empty': 'Scrie un mesaj înainte să-l trimiți.',
  'Message.TooLong': 'Mesajul poate avea cel mult 1000 de caractere.',
  'Photo.NotSeller': 'Doar vânzătorul poate schimba pozele.',
  'Photo.LimitReached': 'Un anunț poate avea cel mult 5 poze.',
  'Photo.Empty': 'Alege o poză.',
  'Photo.TooLarge': 'Poza poate avea cel mult 2 MB.',
  'Photo.StorageFailed': 'Poza nu a putut fi salvată pe server. Încearcă din nou peste puțin timp.',
  'Photo.OrderMismatch': 'Lista de poze s-a schimbat între timp. Reîncarcă pagina și încearcă din nou.',
  'Photo.InvalidFormat': 'Sunt acceptate doar poze JPEG, PNG sau WebP.',
  'Listings.InvalidPriceRange': 'Prețul minim nu poate fi mai mare decât cel maxim.',
  'Listings.InvalidSort': 'Ordinea aleasă nu e validă.',
  'Member.NotFound': 'Membrul nu există.',
  'Listing.CategoryInactive': 'Categoria anunțului nu mai este activă.',
  'Listing.NotExpired': 'Doar un anunț expirat poate fi reînnoit.',
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return MESSAGES[error.code] ?? error.message
  return 'A apărut o eroare neașteptată.'
}

export function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}
