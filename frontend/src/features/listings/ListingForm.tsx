import { useEffect, useId, useRef, useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react'
import { ApiError } from '../../api/errors'
import { api } from '../../api/client'
import { Alert, Button, ButtonLink, TextField } from '../../app/ui'
import { useCategories } from './categories'
import type { PhotoEntry } from './photoEntries'
import { PhotoPicker } from './PhotoPicker'
import { placeListingError, type FieldErrors } from './errors'
import {
  CONDITIONS,
  CONDITION_LABELS,
  type Category,
  type Condition,
  type SuggestCategoryResponse,
  type SuggestPriceResponse,
} from './types'

export interface ListingFormValues {
  title: string
  description: string | null
  category: Category
  condition: Condition
  price: number
}

// What a submit button asks for: "save" keeps (or makes) the listing as it is, "publish" also publishes it.
export type ListingFormIntent = 'save' | 'publish'

export interface ListingFormAction {
  intent: ListingFormIntent
  label: string
  submittingLabel: string
}

export type ListingFormSubmitResult =
  | { ok: true }
  | { ok: false; fieldErrors?: FieldErrors; formError?: string | null }

export interface ListingFormInitialValues {
  title?: string
  description?: string
  category?: Category
  condition?: Condition
  price?: string
}

interface ListingFormProps {
  initialValues?: ListingFormInitialValues
  onSubmit: (values: ListingFormValues, intent: ListingFormIntent) => Promise<ListingFormSubmitResult>
  // The buttons, the last one is the main one.
  actions: ListingFormAction[]
  photos: PhotoEntry[]
  onPhotosChange: (photos: PhotoEntry[]) => void
  showSuggestions?: boolean
  cancelTo?: string
  cancelLabel?: string
}

// MAE (lei) of the price regression model, used for the ± margin around the
// suggested price. No API endpoint exposes the model metadata, so the value
// lives here as a single constant — refresh it when the model is retrained.
// Source: ml/models/price-regression.json (testMetrics.mae), rounded to whole lei.
const PRICE_MAE_LEI = 269

const PRICE_SUGGESTION_DEBOUNCE_MS = 300

function parsePrice(input: string): number | null {
  if (input.trim() === '') return null
  const value = Number(input.replace(',', '.'))
  return Number.isFinite(value) ? value : null
}

function priceRange(suggestion: number): { low: number; high: number } {
  return {
    low: Math.max(0, Math.round(suggestion - PRICE_MAE_LEI)),
    high: Math.max(0, Math.round(suggestion + PRICE_MAE_LEI)),
  }
}

export function ListingForm({
  initialValues,
  onSubmit,
  actions,
  photos,
  onPhotosChange,
  showSuggestions = false,
  cancelTo,
  cancelLabel,
}: ListingFormProps): ReactNode {
  const [title, setTitle] = useState(initialValues?.title ?? '')
  const [description, setDescription] = useState(initialValues?.description ?? '')
  // null until the user picks one: both are required.
  const [category, setCategory] = useState<Category | null>(initialValues?.category ?? null)
  const [condition, setCondition] = useState<Condition | null>(initialValues?.condition ?? null)
  const [price, setPrice] = useState(initialValues?.price ?? '')

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState<ListingFormIntent | null>(null)

  const [suggestedPrice, setSuggestedPrice] = useState<number | null>(null)
  const [suggestingPrice, setSuggestingPrice] = useState(false)
  const [priceSuggestionError, setPriceSuggestionError] = useState<string | null>(null)

  const [suggestedCategory, setSuggestedCategory] = useState<{ category: Category | null; confidence: number } | null>(null)
  const [suggestingCategory, setSuggestingCategory] = useState(false)
  const [categorySuggestionError, setCategorySuggestionError] = useState<string | null>(null)

  // Only the active categories can be chosen. When editing a listing that is already in a deactivated
  // category, that one category stays in the list as the current value, so the form still works;
  // other listings cannot be moved into it, because it is not offered to them.
  const { categories, labelOf } = useCategories()
  const currentCategory = initialValues?.category
  const categoryOptions = categories.filter((c) => c.isActive || c.key === currentCategory)
  const canUseCategory = (key: Category) => categoryOptions.some((c) => c.key === key)

  const descriptionId = useId()
  const categoryId = useId()
  const conditionId = useId()

  // Latest title/description, read by the debounced auto-suggest without
  // re-triggering it on every keystroke.
  const titleRef = useRef(title)
  titleRef.current = title
  const descriptionRef = useRef(description)
  descriptionRef.current = description
  const priceRequestRef = useRef<AbortController | null>(null)

  async function runPriceSuggestion(categoryKey: Category, conditionValue: Condition): Promise<void> {
    const trimmedTitle = titleRef.current.trim()
    if (trimmedTitle.length === 0) return
    const trimmedDescription = descriptionRef.current.trim()

    priceRequestRef.current?.abort()
    const controller = new AbortController()
    priceRequestRef.current = controller

    setPriceSuggestionError(null)
    setSuggestingPrice(true)
    try {
      const data = await api.post<SuggestPriceResponse>(
        '/listings/price-suggestion',
        {
          title: trimmedTitle,
          category: categoryKey,
          condition: conditionValue,
          description: trimmedDescription === '' ? null : trimmedDescription,
        },
        { signal: controller.signal },
      )
      if (controller.signal.aborted) return
      setSuggestedPrice(data.suggestedPrice)
    } catch (err) {
      if (controller.signal.aborted) return
      if (err instanceof DOMException && err.name === 'AbortError') return
      if (err instanceof ApiError) {
        setPriceSuggestionError(placeListingError(err).form ?? err.message)
      } else {
        setPriceSuggestionError('Sugestia de preț nu a putut fi calculată.')
      }
    } finally {
      if (priceRequestRef.current === controller) {
        setSuggestingPrice(false)
        priceRequestRef.current = null
      }
    }
  }

  // Auto-suggest when category or condition changes (once both are chosen). Debounced so a run of changes only triggers the last one; the
  // AbortController in runPriceSuggestion cancels any in-flight request.
  useEffect(() => {
    if (!showSuggestions || category === null || condition === null) return
    const timer = setTimeout(() => {
      void runPriceSuggestion(category, condition)
    }, PRICE_SUGGESTION_DEBOUNCE_MS)
    return () => {
      clearTimeout(timer)
    }
    // runPriceSuggestion reads title/description from refs, so it doesn't need to be a dep.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [showSuggestions, category, condition])

  async function handleSuggestCategory() {
    setCategorySuggestionError(null)
    setSuggestingCategory(true)
    try {
      const data = await api.post<SuggestCategoryResponse>('/listings/category-suggestion', {
        title: title.trim(),
        description: description.trim() === '' ? null : description.trim(),
      })
      setSuggestedCategory({ category: data.category, confidence: data.confidence })
    } catch (err) {
      if (err instanceof ApiError) {
        setCategorySuggestionError(placeListingError(err).form ?? err.message)
      } else {
        setCategorySuggestionError('Sugestia de categorie nu a putut fi calculată.')
      }
    } finally {
      setSuggestingCategory(false)
    }
  }

  async function submit(intent: ListingFormIntent) {
    setFieldErrors({})
    setFormError(null)

    const errors: FieldErrors = {}
    if (category === null) errors.category = 'Alege categoria.'
    if (condition === null) errors.condition = 'Alege starea.'
    const priceValue = parsePrice(price)
    if (priceValue === null) errors.price = 'Introdu un preț valid (număr pozitiv, maxim 2 zecimale).'
    if (category === null || condition === null || priceValue === null) {
      setFieldErrors(errors)
      return
    }

    setSubmitting(intent)
    const result = await onSubmit(
      {
        title: title.trim(),
        description: description.trim() === '' ? null : description.trim(),
        category,
        condition,
        price: priceValue,
      },
      intent,
    )
    // On success the page navigates away; on failure the form stays, with the photos it has.
    if (!result.ok) {
      setFieldErrors(result.fieldErrors ?? {})
      setFormError(result.formError ?? null)
    }
    setSubmitting(null)
  }

  const titleReady = title.trim().length > 0
  const canSuggestPrice = titleReady && category !== null && condition !== null
  const canSuggestCategory = titleReady

  const suggestionRange = suggestedPrice !== null ? priceRange(suggestedPrice) : null

  return (
    <form onSubmit={(e: FormEvent<HTMLFormElement>) => { e.preventDefault() }} className="listings-form" noValidate>
      <TextField
        label="Titlu"
        value={title}
        onChange={(e: ChangeEvent<HTMLInputElement>) => setTitle(e.target.value)}
        error={fieldErrors.title}
        maxLength={100}
        required
        autoFocus
      />

      <div className={fieldErrors.description ? 'field field--error' : 'field'}>
        <label className="field__label" htmlFor={descriptionId}>Descriere</label>
        <textarea
          id={descriptionId}
          className="listings-form__textarea"
          value={description}
          onChange={(e: ChangeEvent<HTMLTextAreaElement>) => setDescription(e.target.value)}
          maxLength={1000}
          aria-invalid={fieldErrors.description ? true : undefined}
          aria-describedby={fieldErrors.description ? `${descriptionId}-error` : undefined}
        />
        {!fieldErrors.description && <span className="field__hint">Opțional. Maxim 1000 de caractere.</span>}
        {fieldErrors.description && (
          <span id={`${descriptionId}-error`} className="field__error">
            {fieldErrors.description}
          </span>
        )}
      </div>

      <div className="listings-form__row">
        <div className={fieldErrors.category ? 'field field--error' : 'field'}>
          <label className="field__label" htmlFor={categoryId}>Categorie</label>
          <select
            id={categoryId}
            className="listings-form__select"
            value={category ?? ''}
            onChange={(e: ChangeEvent<HTMLSelectElement>) => {
              setCategory(e.target.value as Category)
              setFieldErrors((prev) => ({ ...prev, category: undefined }))
            }}
            aria-invalid={fieldErrors.category ? true : undefined}
            required
          >
            <option value="" disabled>
              Alege categoria
            </option>
            {categoryOptions.map((c) => (
              <option key={c.key} value={c.key}>
                {c.isActive ? c.displayName : `${c.displayName} (inactivă)`}
              </option>
            ))}
          </select>
          {fieldErrors.category && <span className="field__error">{fieldErrors.category}</span>}
        </div>

        <div className={fieldErrors.condition ? 'field field--error' : 'field'}>
          <label className="field__label" htmlFor={conditionId}>Stare</label>
          <select
            id={conditionId}
            className="listings-form__select"
            value={condition ?? ''}
            onChange={(e: ChangeEvent<HTMLSelectElement>) => {
              setCondition(e.target.value as Condition)
              setFieldErrors((prev) => ({ ...prev, condition: undefined }))
            }}
            aria-invalid={fieldErrors.condition ? true : undefined}
            required
          >
            <option value="" disabled>
              Alege starea
            </option>
            {CONDITIONS.map((c) => (
              <option key={c} value={c}>
                {CONDITION_LABELS[c]}
              </option>
            ))}
          </select>
          {fieldErrors.condition && <span className="field__error">{fieldErrors.condition}</span>}
        </div>
      </div>

      <TextField
        label="Preț (RON)"
        type="number"
        inputMode="decimal"
        step="0.01"
        min="0"
        value={price}
        onChange={(e: ChangeEvent<HTMLInputElement>) => setPrice(e.target.value)}
        error={fieldErrors.price}
        required
      />

      <PhotoPicker photos={photos} onChange={onPhotosChange} disabled={submitting !== null} />

      {showSuggestions && (
        <div className="listings-form__suggestions">
          <div className="listings-form__suggestion">
            <span>
              {suggestionRange !== null && suggestedPrice !== null
                ? `Preț orientativ: ${suggestionRange.low} – ${suggestionRange.high} RON`
                : priceSuggestionError
                  ? priceSuggestionError
                  : 'Nicio sugestie încă.'}
            </span>
            <div style={{ display: 'flex', gap: 8 }}>
              {suggestedPrice !== null && (
                <Button
                  type="button"
                  variant="secondary"
                  size="sm"
                  onClick={() => {
                    setPrice(String(suggestedPrice))
                  }}
                >
                  Folosește {suggestedPrice} RON
                </Button>
              )}
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={!canSuggestPrice || suggestingPrice}
                loading={suggestingPrice}
                onClick={() => {
                  if (category !== null && condition !== null) void runPriceSuggestion(category, condition)
                }}
              >
                Sugerează preț
              </Button>
            </div>
          </div>
          <span className="field__hint">Estimare automată; prețul îl alegi tu.</span>

          <div className="listings-form__suggestion">
            <span className="listings-form__suggestion-label">Sugestie categorie:</span>
            <span>
              {suggestedCategory
                ? suggestedCategory.category
                  ? `${labelOf(suggestedCategory.category)}${canUseCategory(suggestedCategory.category) ? '' : ' (inactivă)'} (încredere ${(suggestedCategory.confidence * 100).toFixed(0)}%)`
                  : `Nicio categorie sigură (încredere ${(suggestedCategory.confidence * 100).toFixed(0)}%)`
                : categorySuggestionError ?? 'Nicio sugestie încă.'}
            </span>
            <div style={{ display: 'flex', gap: 8 }}>
              {suggestedCategory?.category && canUseCategory(suggestedCategory.category) && (
                <Button
                  type="button"
                  variant="secondary"
                  size="sm"
                  onClick={() => {
                    if (suggestedCategory.category) {
                      setCategory(suggestedCategory.category)
                      setFieldErrors((prev) => ({ ...prev, category: undefined }))
                    }
                  }}
                >
                  Folosește „{labelOf(suggestedCategory.category)}"
                </Button>
              )}
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={!canSuggestCategory || suggestingCategory}
                loading={suggestingCategory}
                onClick={() => {
                  void handleSuggestCategory()
                }}
              >
                Sugerează categorie
              </Button>
            </div>
          </div>
        </div>
      )}

      {formError && <Alert tone="error">{formError}</Alert>}

      <div className="listings-form__actions">
        {cancelTo && (
          <ButtonLink to={cancelTo} variant="secondary">
            {cancelLabel ?? 'Anulează'}
          </ButtonLink>
        )}
        {actions.map((action, index) => (
          <Button
            key={action.intent}
            variant={index === actions.length - 1 ? 'primary' : 'secondary'}
            loading={submitting === action.intent}
            disabled={submitting !== null}
            onClick={() => {
              void submit(action.intent)
            }}
          >
            {submitting === action.intent ? action.submittingLabel : action.label}
          </Button>
        ))}
      </div>
    </form>
  )
}
