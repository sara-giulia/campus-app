import { useId, useState, type ChangeEvent, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Alert, Button, Card, PageHeader, TextField } from '../../app/ui'
import { placeListingError, type FieldErrors } from './errors'
import {
  CATEGORIES,
  CATEGORY_LABELS,
  CONDITIONS,
  CONDITION_LABELS,
  type Category,
  type Condition,
  type CreateListingResponse,
  type SuggestCategoryResponse,
  type SuggestPriceResponse,
} from './types'
import './listings.css'

// POST /api/v1/listings needs Title, Category, Condition, Price and an optional
// Description. The seller comes from the JWT. On success we immediately publish
// the draft so the listing shows up in /my/listings (GET /listings only returns
// Active listings) and navigate there.

function parsePrice(input: string): number | null {
  if (input.trim() === '') return null
  const value = Number(input.replace(',', '.'))
  return Number.isFinite(value) ? value : null
}

export function CreateListingPage() {
  const navigate = useNavigate()

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState<Category>('Textbook')
  const [condition, setCondition] = useState<Condition>('Good')
  const [price, setPrice] = useState('')

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  // ML suggestions. Each kept separately so one failure does not hide the other.
  const [suggestedPrice, setSuggestedPrice] = useState<number | null>(null)
  const [suggestingPrice, setSuggestingPrice] = useState(false)
  const [priceSuggestionError, setPriceSuggestionError] = useState<string | null>(null)

  const [suggestedCategory, setSuggestedCategory] = useState<{ category: Category | null; confidence: number } | null>(null)
  const [suggestingCategory, setSuggestingCategory] = useState(false)
  const [categorySuggestionError, setCategorySuggestionError] = useState<string | null>(null)

  const descriptionId = useId()
  const categoryId = useId()
  const conditionId = useId()

  async function handleSuggestPrice() {
    setPriceSuggestionError(null)
    setSuggestingPrice(true)
    try {
      const data = await api.post<SuggestPriceResponse>('/listings/price-suggestion', {
        title: title.trim(),
        category,
        condition,
        description: description.trim() === '' ? null : description.trim(),
      })
      setSuggestedPrice(data.suggestedPrice)
    } catch (err) {
      if (err instanceof ApiError) {
        setPriceSuggestionError(placeListingError(err).form ?? err.message)
      } else {
        setPriceSuggestionError('Sugestia de preț nu a putut fi calculată.')
      }
    } finally {
      setSuggestingPrice(false)
    }
  }

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

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFieldErrors({})
    setFormError(null)

    const priceValue = parsePrice(price)
    if (priceValue === null) {
      setFieldErrors({ price: 'Introdu un preț valid (număr pozitiv, maxim 2 zecimale).' })
      return
    }

    setSubmitting(true)
    try {
      const created = await api.post<CreateListingResponse>('/listings', {
        title: title.trim(),
        category,
        condition,
        price: priceValue,
        description: description.trim() === '' ? null : description.trim(),
      })
      // Create leaves the listing in Draft; publish it so it is visible in the
      // listings grid and in /my/listings (which only sees Active listings).
      try {
        await api.post<void>(`/listings/${created.id}/publish`)
      } catch (publishError) {
        // A publish error still leaves the listing created as a draft. We show
        // the error and keep the user on the form so they can retry or go to
        // "my listings" by other means.
        if (publishError instanceof ApiError) {
          setFormError(
            `Anunțul a fost creat, dar publicarea a eșuat: ${placeListingError(publishError).form ?? publishError.message}`,
          )
        } else {
          setFormError('Anunțul a fost creat, dar publicarea a eșuat.')
        }
        setSubmitting(false)
        return
      }
      navigate('/my/listings')
    } catch (err) {
      if (err instanceof ApiError) {
        const placed = placeListingError(err)
        setFieldErrors(placed.fields)
        setFormError(placed.form)
      } else {
        setFormError('A apărut o eroare neașteptată.')
      }
      setSubmitting(false)
    }
  }

  const canSuggest = title.trim().length > 0

  return (
    <div className="listings-page">
      <PageHeader eyebrow="Anunțuri" title="Publică un anunț" lead="Completează detaliile anunțului. Îți putem sugera un preț sau o categorie." />

      <Card className="listings-card">
        <form onSubmit={handleSubmit} className="listings-form" noValidate>
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
                value={category}
                onChange={(e: ChangeEvent<HTMLSelectElement>) => setCategory(e.target.value as Category)}
                aria-invalid={fieldErrors.category ? true : undefined}
              >
                {CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {CATEGORY_LABELS[c]}
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
                value={condition}
                onChange={(e: ChangeEvent<HTMLSelectElement>) => setCondition(e.target.value as Condition)}
                aria-invalid={fieldErrors.condition ? true : undefined}
              >
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

          <div className="listings-form__suggestions">
            <div className="listings-form__suggestion">
              <span className="listings-form__suggestion-label">Sugestie preț:</span>
              <span>
                {suggestedPrice !== null
                  ? `${suggestedPrice} RON`
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
                  disabled={!canSuggest || suggestingPrice}
                  loading={suggestingPrice}
                  onClick={() => {
                    void handleSuggestPrice()
                  }}
                >
                  Sugerează preț
                </Button>
              </div>
            </div>

            <div className="listings-form__suggestion">
              <span className="listings-form__suggestion-label">Sugestie categorie:</span>
              <span>
                {suggestedCategory
                  ? suggestedCategory.category
                    ? `${CATEGORY_LABELS[suggestedCategory.category]} (încredere ${(suggestedCategory.confidence * 100).toFixed(0)}%)`
                    : `Nicio categorie sigură (încredere ${(suggestedCategory.confidence * 100).toFixed(0)}%)`
                  : categorySuggestionError ?? 'Nicio sugestie încă.'}
              </span>
              <div style={{ display: 'flex', gap: 8 }}>
                {suggestedCategory?.category && (
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    onClick={() => {
                      if (suggestedCategory.category) setCategory(suggestedCategory.category)
                    }}
                  >
                    Folosește „{CATEGORY_LABELS[suggestedCategory.category]}"
                  </Button>
                )}
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={!canSuggest || suggestingCategory}
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

          {formError && <Alert tone="error">{formError}</Alert>}

          <div className="listings-form__actions">
            <Button type="submit" loading={submitting}>
              {submitting ? 'Se publică…' : 'Publică anunțul'}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  )
}
