import type { ApiError } from '../../api/errors'
import { translate } from '../../i18n/store'

// Maps the API error codes of PUT /api/v1/categories/{category} to field-level and form-level
// messages (same pattern as features/listings/errors.ts). The codes come from ListingCategory
// in the domain and from UpdateCategoryHandler.

export interface CategoryFieldErrors {
  displayName?: string
  description?: string
}

export interface PlacedCategoryError {
  fields: CategoryFieldErrors
  form: string | null
}

export function placeCategoryError(error: ApiError): PlacedCategoryError {
  switch (error.code) {
    case 'Category.DisplayNameEmpty':
      return { fields: { displayName: translate('adminCategories.error.DisplayNameEmpty') }, form: null }
    case 'Category.DisplayNameTooLong':
      return { fields: { displayName: translate('adminCategories.error.DisplayNameTooLong') }, form: null }
    case 'Category.DescriptionTooLong':
      return { fields: { description: translate('adminCategories.error.DescriptionTooLong') }, form: null }
    case 'Category.LastActive':
      return {
        fields: {},
        form: translate('adminCategories.error.LastActive'),
      }
    case 'Category.IsActiveRequired':
      return { fields: {}, form: translate('adminCategories.error.IsActiveRequired') }
    case 'Category.NotFound':
      return { fields: {}, form: translate('adminCategories.error.NotFound') }
    case 'Http.403':
      return { fields: {}, form: translate('adminCategories.error.Forbidden') }
    case 'Network.Unreachable':
      return { fields: {}, form: translate('common.networkUnreachable') }
    default:
      return { fields: {}, form: error.message }
  }
}
