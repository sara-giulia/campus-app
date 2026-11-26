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
import { loadServerPhotos, syncPhotos, type PhotoEntry } from './photoEntries'
import type { ListingDetail, ListingStatus } from './types'
import './listings.css'
import { useT } from '../../i18n/LanguageContext'
import { translate } from '../../i18n/store'

// PUT /api/v1/listings/{id}. The seller comes from the token; the handler
// refuses the change for anyone but the owner (Listing.NotOwner) and for sold
// listings (Listing.Sold). The field errors come from Listing.UpdateDetails.
// The photos are saved with the form: the removed ones are deleted, the new ones are uploaded one by one,
// then the order is sent (PUT /listings/{id}/photos/order). A draft also has "Publică".

const ACTIONS: ListingFormAction[] = [{ intent: 'save', label: 'listings.action.saveChanges', submittingLabel: 'listings.action.saving' }]
const DRAFT_ACTIONS: ListingFormAction[] = [...ACTIONS, { intent: 'publish', label: 'listings.action.publish', submittingLabel: 'listings.action.publishing' }]

export function EditListingPage() {
  const t = useT()
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
          setLoadError(translate('common.unexpectedError'))
        }
      })
      .finally(() => setLoading(false))
    return () => controller.abort()
  }, [id])

  async function handleSubmit(values: ListingFormValues, intent: ListingFormIntent): Promise<ListingFormSubmitResult> {
    if (!id) return { ok: false, formError: t('listings.error.NotFound') }
    try {
      await api.put<void>(`/listings/${id}`, values)

      const uploadError = await syncPhotos(id, photos, serverIds, setPhotos)
      if (uploadError) {
        return { ok: false, formError: t('listings.edit.savedButUpload', { error: uploadError }) }
      }

      if (intent === 'publish') await api.post<void>(`/listings/${id}/publish`)

      navigate('/my/listings')
      return { ok: true }
    } catch (err) {
      if (err instanceof ApiError) {
        const placed = placeListingError(err)
        return { ok: false, fieldErrors: placed.fields, formError: placed.form }
      }
      return { ok: false, formError: t('common.unexpectedError') }
    }
  }

  if (loading) return <Loading text={t('listings.edit.loading')} />

  if (loadError || !initialValues) {
    return (
      <div className="listings-page">
        <PageHeader eyebrow={t('listings.edit.eyebrow')} title={t('listings.edit.title')} />
        <Alert tone="error">{loadError ?? t('listings.error.NotFound')}</Alert>
        <div>
          <ButtonLink to="/my/listings" variant="secondary">
            {t('listings.edit.back')}
          </ButtonLink>
        </div>
      </div>
    )
  }

  return (
    <div className="listings-page">
      <PageHeader eyebrow={t('listings.edit.eyebrow')} title={t('listings.edit.title')} />

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
