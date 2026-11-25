import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Alert, ButtonLink, Card, Loading, PageHeader } from '../../app/ui'
import { placeListingError } from './errors'
import {
  ListingForm,
  type ListingFormAction,
  type ListingFormInitialValues,
  type ListingFormIntent,
  type ListingFormSubmitResult,
  type ListingFormValues,
} from './ListingForm'
import { loadServerPhotos, uploadPending, type PhotoEntry } from './photoEntries'
import type { ListingDetail, ListingStatus } from './types'
import './listings.css'

// PUT /api/v1/listings/{id}. The seller comes from the token; the handler
// refuses the change for anyone but the owner (Listing.NotOwner) and for sold
// listings (Listing.Sold). The field errors come from Listing.UpdateDetails.
// The photos are saved with the form: the removed ones are deleted, the new ones are uploaded one by one,
// then the order is sent (PUT /listings/{id}/photos/order). A draft also has "Publică".

const ACTIONS: ListingFormAction[] = [{ intent: 'save', label: 'Salvează modificările', submittingLabel: 'Se salvează…' }]
const DRAFT_ACTIONS: ListingFormAction[] = [...ACTIONS, { intent: 'publish', label: 'Publică', submittingLabel: 'Se publică…' }]

export function EditListingPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const [initialValues, setInitialValues] = useState<ListingFormInitialValues | null>(null)
  const [status, setStatus] = useState<ListingStatus | null>(null)
  const [photos, setPhotos] = useState<PhotoEntry[]>([])
  // The photos the server has, in its order: what was removed or moved is found by comparing with it.
  const serverIds = useRef<string[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    const controller = new AbortController()
    Promise.all([api.get<ListingDetail>(`/listings/${id}`, { signal: controller.signal }), loadServerPhotos(id, controller.signal)])
      .then(([data, serverPhotos]) => {
        setStatus(data.status)
        serverIds.current = serverPhotos.flatMap((p) => (p.kind === 'server' ? [p.id] : []))
        setPhotos(serverPhotos)
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

  async function handleSubmit(values: ListingFormValues, intent: ListingFormIntent): Promise<ListingFormSubmitResult> {
    if (!id) return { ok: false, formError: 'Anunțul nu a fost găsit.' }
    try {
      await api.put<void>(`/listings/${id}`, values)

      // 1. the photos that were removed
      const kept = new Set(photos.flatMap((p) => (p.kind === 'server' ? [p.id] : [])))
      for (const photoId of serverIds.current.filter((known) => !kept.has(known))) {
        await api.delete(`/listings/${id}/photos/${photoId}`)
        serverIds.current = serverIds.current.filter((known) => known !== photoId)
      }

      // 2. the new photos, one by one, in order
      // (the server puts each new photo last, so the known ids stay in the order of the server)
      const { photos: uploaded, error: uploadError } = await uploadPending(id, photos, (next) => {
        setPhotos(next)
        const known = new Set(serverIds.current)
        serverIds.current = [...serverIds.current, ...next.flatMap((p) => (p.kind === 'server' && !known.has(p.id) ? [p.id] : []))]
      })
      if (uploadError) {
        return { ok: false, formError: `Modificările au fost salvate, dar ${uploadError} Apasă din nou ca să continui.` }
      }

      // 3. the order, when it is not the one of the server
      const order = uploaded.flatMap((p) => (p.kind === 'server' ? [p.id] : []))
      if (order.length > 1 && order.some((photoId, i) => photoId !== serverIds.current[i])) {
        await api.put(`/listings/${id}/photos/order`, { photoIds: order })
      }

      if (intent === 'publish') await api.post<void>(`/listings/${id}/publish`)

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
          actions={status === 'Draft' ? DRAFT_ACTIONS : ACTIONS}
          photos={photos}
          onPhotosChange={setPhotos}
          cancelTo="/my/listings"
        />
      </Card>
    </div>
  )
}
