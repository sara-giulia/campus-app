import type { ApiError } from '../../api/errors'

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
      return { fields: { displayName: 'Numele afișat este obligatoriu.' }, form: null }
    case 'Category.DisplayNameTooLong':
      return { fields: { displayName: 'Numele afișat poate avea maxim 100 de caractere.' }, form: null }
    case 'Category.DescriptionTooLong':
      return { fields: { description: 'Descrierea poate avea maxim 500 de caractere.' }, form: null }
    case 'Category.LastActive':
      return {
        fields: {},
        form: 'Cel puțin o categorie trebuie să rămână activă. Activează altă categorie înainte să o dezactivezi pe aceasta.',
      }
    case 'Category.IsActiveRequired':
      return { fields: {}, form: 'Alege dacă această categorie este activă sau nu.' }
    case 'Category.NotFound':
      return { fields: {}, form: 'Categoria nu există.' }
    case 'Http.403':
      return { fields: {}, form: 'Doar un administrator poate modifica categoriile.' }
    case 'Network.Unreachable':
      return { fields: {}, form: 'Serverul nu răspunde. Verifică dacă API-ul este pornit.' }
    default:
      return { fields: {}, form: error.message }
  }
}
