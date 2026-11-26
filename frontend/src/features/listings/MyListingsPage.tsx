import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { useAuth } from '../../app/auth/AuthContext'
import { Alert, Badge, Button, ButtonLink, EmptyState, Loading, PageHeader, Pagination } from '../../app/ui'
import { placeListingError } from './errors'
import { useCategories } from './categories'
import {
  CONDITION_LABELS,
  LISTING_STATUS_LABELS,
  type ListingStatus,
  type ListingSummary,
} from './types'
import './listings.css'

const PAGE_SIZE = 10

// GET /members/me/listings also sends expiresAt (null for a draft that was never published) and
// reservedPrice: what the buyer of the active reservation pays (an accepted offer's price, or the
// listing's price), null without an active reservation.
type MyListingItem = ListingSummary & { expiresAt: string | null; reservedPrice: number | null }
type MyListingsResponse = { items: MyListingItem[]; count: number }

const DAY_MS = 24 * 60 * 60 * 1000

// "Expiră în X zile" for an active listing; the last day reads "Expiră azi".
function expiryText(expiresAt: string): string {
  const days = Math.ceil((new Date(expiresAt).getTime() - Date.now()) / DAY_MS)
  if (days <= 0) return 'Expiră azi'
  if (days === 1) return 'Expiră mâine'
  return `Expiră în ${days} ${days < 20 ? 'zile' : 'de zile'}`
}

function statusTone(status: ListingStatus): 'neutral' | 'success' | 'warning' | 'danger' | 'accent' {
  switch (status) {
    case 'Active':
      return 'success'
    case 'Reserved':
      return 'warning'
    case 'Sold':
      return 'accent'
    case 'Hidden':
    case 'Expired':
      return 'danger'
    default:
      return 'neutral'
  }
}

export function MyListingsPage() {
  const { labelOf } = useCategories()
  const { user } = useAuth()
  const [items, setItems] = useState<MyListingItem[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [publishingId, setPublishingId] = useState<string | null>(null)
  const [renewingId, setRenewingId] = useState<string | null>(null)
  const [rowError, setRowError] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)

  useEffect(() => {
    if (!user) return
    const controller = new AbortController()
    api
      .get<MyListingsResponse>('/members/me/listings', { signal: controller.signal })
      .then((data) => {
        setItems(data.items)
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        if (err instanceof ApiError) {
          setLoadError(err.code === 'Network.Unreachable' ? 'Serverul nu răspunde. Verifică dacă API-ul este pornit.' : err.message)
        } else {
          setLoadError('A apărut o eroare neașteptată.')
        }
      })
    return () => controller.abort()
  }, [user])

  async function reloadMine() {
    if (!user) return
    try {
      const data = await api.get<MyListingsResponse>('/members/me/listings')
      setItems(data.items)
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.code === 'Network.Unreachable' ? 'Serverul nu răspunde. Verifică dacă API-ul este pornit.' : err.message)
      } else {
        setFormError('A apărut o eroare neașteptată.')
      }
    }
  }

  async function handleDelete(listing: ListingSummary) {
    if (!window.confirm(`Ștergi anunțul „${listing.title}"?`)) return
    setDeletingId(listing.id)
    setRowError((prev) => ({ ...prev, [listing.id]: null }))
    setFormError(null)
    try {
      await api.delete<void>(`/listings/${listing.id}`)
      await reloadMine()
    } catch (err) {
      if (err instanceof ApiError) {
        const message = placeListingError(err).form ?? err.message
        setRowError((prev) => ({ ...prev, [listing.id]: message }))
      } else {
        setFormError('A apărut o eroare neașteptată.')
      }
    } finally {
      setDeletingId(null)
    }
  }

  async function handlePublish(listing: ListingSummary) {
    setPublishingId(listing.id)
    setRowError((prev) => ({ ...prev, [listing.id]: null }))
    setFormError(null)
    try {
      await api.post<void>(`/listings/${listing.id}/publish`)
      await reloadMine()
    } catch (err) {
      if (err instanceof ApiError) {
        const message =
          err.code === 'Listing.CategoryInactive'
            ? 'Categoria acestui anunț nu mai este activă, așa că nu poate fi publicat. Apasă „Editează” și alege altă categorie.'
            : (placeListingError(err).form ?? err.message)
        setRowError((prev) => ({ ...prev, [listing.id]: message }))
      } else {
        setFormError('A apărut o eroare neașteptată.')
      }
    } finally {
      setPublishingId(null)
    }
  }

  async function handleRenew(listing: ListingSummary) {
    setRenewingId(listing.id)
    setRowError((prev) => ({ ...prev, [listing.id]: null }))
    setFormError(null)
    try {
      await api.post<void>(`/listings/${listing.id}/renew`)
      await reloadMine()
    } catch (err) {
      if (err instanceof ApiError) {
        const message =
          err.code === 'Listing.CategoryInactive'
            ? 'Categoria acestui anunț nu mai este activă, așa că nu poate fi reînnoit. Apasă „Editează” și alege altă categorie.'
            : (placeListingError(err).form ?? err.message)
        setRowError((prev) => ({ ...prev, [listing.id]: message }))
      } else {
        setFormError('A apărut o eroare neașteptată.')
      }
    } finally {
      setRenewingId(null)
    }
  }

  if (items === null && loadError === null) return <Loading text="Se încarcă anunțurile tale…" />

  if (loadError) {
    return (
      <div className="listings-page">
        <PageHeader eyebrow="Contul meu" title="Anunțurile mele" />
        <Alert tone="error">{loadError}</Alert>
      </div>
    )
  }

  const all = items ?? []
  const totalPages = Math.max(1, Math.ceil(all.length / PAGE_SIZE))
  const safePage = Math.min(page, totalPages)
  const visible = all.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE)

  return (
    <div className="listings-page">
      <PageHeader
        eyebrow="Contul meu"
        title="Anunțurile mele"
        lead="Toate anunțurile tale, inclusiv ciornele, cele rezervate, cele vândute și cele expirate."
        actions={<ButtonLink to="/listings/new">Publică anunț</ButtonLink>}
      />

      {formError && <Alert tone="error">{formError}</Alert>}

      {all.length === 0 ? (
        <EmptyState
          title="Nu ai încă niciun anunț."
          action={<ButtonLink to="/listings/new">Publică primul anunț</ButtonLink>}
        >
          Anunțurile tale vor apărea aici.
        </EmptyState>
      ) : (
        <div className="listings-list">
          {visible.map((listing) => (
            <article key={listing.id} className="listing-row">
              <div>
                <h2 className="listing-row__title">{listing.title}</h2>
                <div className="listing-row__meta">
                  <Badge tone={statusTone(listing.status)}>{LISTING_STATUS_LABELS[listing.status]}</Badge>
                  <span>{labelOf(listing.category)}</span>
                  <span>·</span>
                  <span>{CONDITION_LABELS[listing.condition]}</span>
                  <span>·</span>
                  <span className="listing-row__price">{listing.price} RON</span>
                  {listing.status === 'Reserved' && listing.reservedPrice !== null && listing.reservedPrice !== listing.price && (
                    <>
                      <span>·</span>
                      <span>Rezervat la {listing.reservedPrice} RON</span>
                    </>
                  )}
                  {listing.status === 'Active' && listing.expiresAt && (
                    <>
                      <span>·</span>
                      <span>{expiryText(listing.expiresAt)}</span>
                    </>
                  )}
                </div>
              </div>
              <div className="listing-row__actions">
                {listing.status === 'Draft' && (
                  <Button
                    size="sm"
                    disabled={publishingId === listing.id || deletingId === listing.id}
                    loading={publishingId === listing.id}
                    onClick={() => {
                      void handlePublish(listing)
                    }}
                  >
                    {publishingId === listing.id ? 'Se publică…' : 'Publică'}
                  </Button>
                )}
                {listing.status === 'Expired' && (
                  <Button
                    size="sm"
                    disabled={renewingId === listing.id || deletingId === listing.id}
                    loading={renewingId === listing.id}
                    onClick={() => {
                      void handleRenew(listing)
                    }}
                  >
                    {renewingId === listing.id ? 'Se reînnoiește…' : 'Reînnoiește'}
                  </Button>
                )}
                <ButtonLink to={`/my/listings/${listing.id}/edit`} variant="secondary" size="sm">
                  Editează
                </ButtonLink>
                <Button
                  variant="danger"
                  size="sm"
                  disabled={deletingId === listing.id}
                  loading={deletingId === listing.id}
                  onClick={() => {
                    void handleDelete(listing)
                  }}
                >
                  {deletingId === listing.id ? 'Se șterge…' : 'Șterge'}
                </Button>
              </div>
              {rowError[listing.id] && (
                <div className="listing-row__inline-error">
                  <Alert tone="error">{rowError[listing.id]}</Alert>
                </div>
              )}
            </article>
          ))}

          <div className="listings-page__footer">
            <Pagination page={safePage} totalPages={totalPages} onChange={setPage} />
          </div>
        </div>
      )}
    </div>
  )
}
