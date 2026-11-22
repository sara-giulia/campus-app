import { useNavigate } from 'react-router'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Card, PageHeader } from '../../app/ui'
import { placeListingError } from './errors'
import { ListingForm, type ListingFormSubmitResult, type ListingFormValues } from './ListingForm'
import type { CreateListingResponse } from './types'
import './listings.css'

// POST /api/v1/listings needs Title, Category, Condition, Price and an optional
// Description. The seller comes from the JWT. On success we immediately publish
// the draft so the listing shows up in /my/listings (GET /listings only returns
// Active listings) and navigate there.

export function CreateListingPage() {
  const navigate = useNavigate()

  async function handleSubmit(values: ListingFormValues): Promise<ListingFormSubmitResult> {
    try {
      const created = await api.post<CreateListingResponse>('/listings', values)
      try {
        await api.post<void>(`/listings/${created.id}/publish`)
      } catch (publishError) {
        // A publish error still leaves the listing created as a draft. We show
        // the error and keep the user on the form so they can retry or go to
        // "my listings" by other means.
        if (publishError instanceof ApiError) {
          return {
            ok: false,
            formError: `Anunțul a fost creat, dar publicarea a eșuat: ${placeListingError(publishError).form ?? publishError.message}`,
          }
        }
        return { ok: false, formError: 'Anunțul a fost creat, dar publicarea a eșuat.' }
      }
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

  return (
    <div className="listings-page">
      <PageHeader eyebrow="Anunțuri" title="Publică un anunț" lead="Completează detaliile anunțului. Îți putem sugera un preț sau o categorie." />

      <Card className="listings-card">
        <ListingForm
          onSubmit={handleSubmit}
          submitLabel="Publică anunțul"
          submittingLabel="Se publică…"
          showSuggestions
        />
      </Card>
    </div>
  )
}
