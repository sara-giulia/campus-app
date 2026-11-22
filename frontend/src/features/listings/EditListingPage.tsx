import { useEffect, useId, useState, type ChangeEvent, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Alert, Button, ButtonLink, Card, Loading, PageHeader, TextField } from '../../app/ui'
import { placeListingError, type FieldErrors } from './errors'
import {
  CATEGORIES,
  CATEGORY_LABELS,
  CONDITIONS,
  CONDITION_LABELS,
  type Category,
  type Condition,
  type ListingDetail,
} from './types'
import './listings.css'

// PUT /api/v1/listings/{id}. The seller comes from the token; the handler
// refuses the change for anyone but the owner (Listing.NotOwner) and for sold
// listings (Listing.Sold). The field errors come from Listing.UpdateDetails.

function parsePrice(input: string): number | null {
  if (input.trim() === '') return null
  const value = Number(input.replace(',', '.'))
  return Number.isFinite(value) ? value : null
}

export function EditListingPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState<Category>('Textbook')
  const [condition, setCondition] = useState<Condition>('Good')
  const [price, setPrice] = useState('')

  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const descriptionId = useId()
  const categoryId = useId()
  const conditionId = useId()

  useEffect(() => {
    if (!id) return
    const controller = new AbortController()
    api
      .get<ListingDetail>(`/listings/${id}`, { signal: controller.signal })
      .then((data) => {
        setTitle(data.title)
        setDescription(data.description ?? '')
        setCategory(data.category)
        setCondition(data.condition)
        setPrice(String(data.price))
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        if (err instanceof ApiError) {
          setLoadError(placeListingError(err).form ?? err.message)
        } else {
          setLoadError('A apărut o eroare neașteptată.')
        }
      })
      .finally(() => setLoading(false))
    return () => controller.abort()
  }, [id])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!id) return
    setFieldErrors({})
    setFormError(null)

    const priceValue = parsePrice(price)
    if (priceValue === null) {
      setFieldErrors({ price: 'Introdu un preț valid (număr pozitiv, maxim 2 zecimale).' })
      return
    }

    setSubmitting(true)
    try {
      await api.put<void>(`/listings/${id}`, {
        title: title.trim(),
        category,
        condition,
        price: priceValue,
        description: description.trim() === '' ? null : description.trim(),
      })
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

  if (loading) return <Loading text="Se încarcă anunțul…" />

  if (loadError) {
    return (
      <div className="listings-page">
        <PageHeader eyebrow="Anunțurile mele" title="Editează anunțul" />
        <Alert tone="error">{loadError}</Alert>
        <div>
          <ButtonLink to="/my/listings" variant="secondary">
            Înapoi la anunțurile mele
          </ButtonLink>
        </div>
      </div>
    )
  }

  return (
    <div className="listings-page">
      <PageHeader eyebrow="Anunțurile mele" title="Editează anunțul" />

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

          {formError && <Alert tone="error">{formError}</Alert>}

          <div className="listings-form__actions">
            <ButtonLink to="/my/listings" variant="secondary">
              Anulează
            </ButtonLink>
            <Button type="submit" loading={submitting}>
              {submitting ? 'Se salvează…' : 'Salvează modificările'}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  )
}
