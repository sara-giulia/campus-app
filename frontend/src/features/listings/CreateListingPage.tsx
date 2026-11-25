import { useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Card, PageHeader } from '../../app/ui'
import { placeListingError } from './errors'
import {
  ListingForm,
  type ListingFormAction,
  type ListingFormIntent,
  type ListingFormSubmitResult,
  type ListingFormValues,
} from './ListingForm'
import { syncPhotos, type PhotoEntry } from './photoEntries'
import type { CreateListingResponse } from './types'
import './listings.css'

// POST /api/v1/listings creates a draft (Title, Category, Condition and Price are required, the seller
// comes from the JWT). Then the draft is synced with the form (removed photos deleted, missing ones uploaded one by one,
// order sent) and only when all of that worked, "Publică" publishes the draft.
// If a step fails, the id of the draft is kept: the next try updates that same draft, uploads only the
// photos that are not up yet and goes on. A second listing is never created.

const ACTIONS: ListingFormAction[] = [
  { intent: 'save', label: 'Salvează ca ciornă', submittingLabel: 'Se salvează…' },
  { intent: 'publish', label: 'Publică', submittingLabel: 'Se publică…' },
]

export function CreateListingPage() {
  const navigate = useNavigate()
  const [photos, setPhotos] = useState<PhotoEntry[]>([])
  const draftId = useRef<string | null>(null)
  // The photos the draft has on the server, in its order.
  const serverIds = useRef<string[]>([])

  async function handleSubmit(values: ListingFormValues, intent: ListingFormIntent): Promise<ListingFormSubmitResult> {
    try {
      if (draftId.current === null) {
        const created = await api.post<CreateListingResponse>('/listings', values)
        draftId.current = created.id
      } else {
        await api.put<void>(`/listings/${draftId.current}`, values)
      }
      const listingId = draftId.current

      const uploadError = await syncPhotos(listingId, photos, serverIds, setPhotos)
      if (uploadError) {
        return {
          ok: false,
          formError: `Anunțul a fost salvat ca ciornă, dar ${uploadError} Apasă din nou ca să continui cu aceeași ciornă.`,
        }
      }

      if (intent === 'publish') {
        try {
          await api.post<void>(`/listings/${listingId}/publish`)
        } catch (publishError) {
          if (publishError instanceof ApiError) {
            // E.g. Listing.CategoryInactive: the category was deactivated meanwhile.
            const placed = placeListingError(publishError)
            return {
              ok: false,
              fieldErrors: placed.fields,
              formError: `Anunțul a fost salvat ca ciornă, dar publicarea a eșuat: ${placed.form ?? placed.fields.category ?? publishError.message}`,
            }
          }
          return { ok: false, formError: 'Anunțul a fost salvat ca ciornă, dar publicarea a eșuat.' }
        }
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
      <PageHeader eyebrow="Anunțuri" title="Publică un anunț" lead="Completează detaliile anunțului și adaugă poze. Îți putem sugera un preț sau o categorie." />

      <Card className="listings-card">
        <ListingForm
          onSubmit={handleSubmit}
          actions={ACTIONS}
          photos={photos}
          onPhotosChange={setPhotos}
          showSuggestions
        />
      </Card>
    </div>
  )
}
