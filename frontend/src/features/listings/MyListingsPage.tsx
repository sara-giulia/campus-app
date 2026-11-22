import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { useAuth } from '../../app/auth/AuthContext'
import { Alert, Badge, Button, ButtonLink, EmptyState, Loading, PageHeader, Pagination } from '../../app/ui'
import { placeListingError } from './errors'
import {
  CATEGORY_LABELS,
  CONDITION_LABELS,
  LISTING_STATUS_LABELS,
  type GetListingsResponse,
  type ListingStatus,
  type ListingSummary,
} from './types'
import './listings.css'

// GET /api/v1/listings only returns Active listings (see GetListingsHandler).
// There is no "my listings" endpoint, so we fetch Active listings and filter
// by the logged-in user's id on the client. Drafts, Reserved, Sold and Hidden
// listings of the current user are therefore not shown here — this is called
// out in the end-of-task notes and left for a dedicated API endpoint.

const PAGE_SIZE = 10
const FETCH_PAGE_SIZE = 100

function statusTone(status: ListingStatus): 'neutral' | 'success' | 'warning' | 'danger' | 'accent' {
  switch (status) {
    case 'Active':
      return 'success'
    case 'Reserved':
      return 'warning'
    case 'Sold':
      return 'accent'
    case 'Hidden':
      return 'danger'
    default:
      return 'neutral'
  }
}

export function MyListingsPage() {
  const { user } = useAuth()
  const [items, setItems] = useState<ListingSummary[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [rowError, setRowError] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)

  useEffect(() => {
    if (!user) return
    const controller = new AbortController()
    api
      .get<GetListingsResponse>(`/listings?pageSize=${FETCH_PAGE_SIZE}`, { signal: controller.signal })
      .then((data) => {
        setItems(data.items.filter((item) => item.sellerId === user.id))
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
      const data = await api.get<GetListingsResponse>(`/listings?pageSize=${FETCH_PAGE_SIZE}`)
      setItems(data.items.filter((item) => item.sellerId === user.id))
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
        lead="Doar anunțurile active apar aici."
        actions={<ButtonLink to="/listings/new">Publică anunț</ButtonLink>}
      />

      {formError && <Alert tone="error">{formError}</Alert>}

      {all.length === 0 ? (
        <EmptyState
          title="Nu ai încă niciun anunț activ."
          action={<ButtonLink to="/listings/new">Publică primul anunț</ButtonLink>}
        >
          Anunțurile tale publice vor apărea aici.
        </EmptyState>
      ) : (
        <div className="listings-list">
          {visible.map((listing) => (
            <article key={listing.id} className="listing-row">
              <div>
                <h2 className="listing-row__title">{listing.title}</h2>
                <div className="listing-row__meta">
                  <Badge tone={statusTone(listing.status)}>{LISTING_STATUS_LABELS[listing.status]}</Badge>
                  <span>{CATEGORY_LABELS[listing.category]}</span>
                  <span>·</span>
                  <span>{CONDITION_LABELS[listing.condition]}</span>
                  <span>·</span>
                  <span className="listing-row__price">{listing.price} RON</span>
                </div>
              </div>
              <div className="listing-row__actions">
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
