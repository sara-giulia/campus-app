import { ApiError } from '../../api/errors'
import { errorMessage } from '../../app/errorMessage'

// Romanian text for the errors GET /api/v1/admin/ml/price-accuracy can give (the handler itself
// never fails: only auth and network errors reach the page).
export function priceAccuracyError(error: unknown): string {
  if (error instanceof ApiError) {
    switch (error.code) {
      case 'Http.401':
        return 'Sesiunea a expirat. Autentifică-te din nou.'
      case 'Http.403':
        return 'Doar un administrator poate vedea acuratețea prețurilor.'
      case 'Network.Unreachable':
        return 'Serverul nu răspunde. Verifică dacă API-ul este pornit.'
    }
  }
  return errorMessage(error)
}
