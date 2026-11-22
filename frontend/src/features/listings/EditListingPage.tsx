import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Alert, ButtonLink, Card, Loading, PageHeader } from '../../app/ui'
import { placeListingError } from './errors'
import { ListingForm, type ListingFormInitialValues, type ListingFormSubmitResult, type ListingFormValues } from './ListingForm'
import type { ListingDetail } from './types'
import './listings.css'

// PUT /api/v1/listings/{id}. The seller comes from the token; the handler
// refuses the change for anyone but the owner (Listing.NotOwner) and for sold
// listings (Listing.Sold). The field errors come from Listing.UpdateDetails.

export function EditListingPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const [initialValues, setInitialValues] = useState<ListingFormInitialValues | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    const controller = new AbortController()
    api
      .get<ListingDetail>(`/listings/${id}`, { signal: controller.signal })
      .then((data) => {
        setInitialValues({
          title: data.title,
          description: data.description ?? '',
          category: data.category,
          condition: data.condition,
          price: String(data.price),
        })
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

  async function handleSubmit(values: ListingFormValues): Promise<ListingFormSubmitResult> {
    if (!id) return { ok: false, formError: 'Anunțul nu a fost găsit.' }
    try {
      await api.put<void>(`/listings/${id}`, values)
      navigate('/my/listings')
      return { ok: true }
    } catch (err) {
      if (err instanceof ApiError) {
        const placed = placeListingError(err)
        return { ok: false, fieldErrors: placed.fields, formError: placed.form }
      }
      return { ok: false, formError: 'A apărut o eroare neașteptată.' }
    }
  }

  if (loading) return <Loading text="Se încarcă anunțul…" />

  if (loadError || !initialValues) {
    return (
      <div className="listings-page">
        <PageHeader eyebrow="Anunțurile mele" title="Editează anunțul" />
        <Alert tone="error">{loadError ?? 'Anunțul nu a fost găsit.'}</Alert>
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
        <ListingForm
          initialValues={initialValues}
          onSubmit={handleSubmit}
          submitLabel="Salvează modificările"
          submittingLabel="Se salvează…"
          cancelTo="/my/listings"
        />
      </Card>
    </div>
  )
}
