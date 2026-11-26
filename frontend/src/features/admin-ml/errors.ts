import { ApiError } from '../../api/errors'
import { errorMessage } from '../../app/errorMessage'
import { translate } from '../../i18n/store'

// Romanian text for the errors GET /api/v1/admin/ml/price-accuracy can give (the handler itself
// never fails: only auth and network errors reach the page).
export function priceAccuracyError(error: unknown): string {
  if (error instanceof ApiError) {
    switch (error.code) {
      case 'Http.401':
        return translate('priceAccuracy.error.Http.401')
      case 'Http.403':
        return translate('priceAccuracy.error.Http.403')
      case 'Network.Unreachable':
        return translate('common.networkUnreachable')
    }
  }
  return errorMessage(error)
}
