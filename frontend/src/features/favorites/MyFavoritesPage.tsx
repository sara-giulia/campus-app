import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Badge, Button, ButtonLink, EmptyState, Loading, PageHeader, Pagination } from '../../app/ui'
import { formatPrice, type GetMyFavoritesResponse } from '../listings/publicTypes'
import { useCategories } from '../listings/categories'
import { CONDITION_LABELS, LISTING_STATUS_LABELS } from '../listings/types'
import '../listings/listings.css'
import { useT } from '../../i18n/LanguageContext'

const PAGE_SIZE = 10

// The listings I saved, the last saved first. A listing that was reserved or sold since stays here
// with its status, so the member sees what happened to it.
export function MyFavoritesPage() {
  const t = useT()
  const { labelOf } = useCategories()
  const [page, setPage] = useState(1)
  const [data, setData] = useState<GetMyFavoritesResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [removing, setRemoving] = useState<string | null>(null)

  // Bumped after a change, to load the page again.
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetMyFavoritesResponse>(`/members/me/favorites?page=${page}&pageSize=${PAGE_SIZE}`, { signal: controller.signal })
      .then((result) => {
        setData(result)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(errorMessage(err))
      })
    return () => controller.abort()
  }, [page, version])

  async function remove(listingId: string) {
    setRemoving(listingId)
    try {
      await api.delete(`/members/me/favorites/${listingId}`)
      if (data && data.items.length === 1 && page > 1) setPage(page - 1)
      else setVersion((v) => v + 1)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setRemoving(null)
    }
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  return (
    <div className="listings-page">
      <PageHeader eyebrow={t('myListings.eyebrow')} title={t('favorites.title')} lead={t('favorites.lead')} />
      {error && <Alert tone="error">{error}</Alert>}
      {!error && data === null && <Loading text={t('favorites.loading')} />}
      {data !== null && data.totalCount === 0 && (
        <EmptyState title={t('favorites.emptyTitle')} action={<ButtonLink to="/listings">{t('messages.emptyAction')}</ButtonLink>}>
          {t('favorites.emptyText')}
        </EmptyState>
      )}
      {data !== null && data.totalCount > 0 && (
        <>
          <div className="listings-list">
            {data.items.map((f) => (
              <article key={f.listingId} className="listing-row">
                <div>
                  <h2 className="listing-row__title">
                    <Link to={`/listings/${f.listingId}`}>{f.title}</Link>
                  </h2>
                  <div className="listing-row__meta">
                    {f.status !== 'Active' && <Badge tone="warning">{LISTING_STATUS_LABELS[f.status]}</Badge>}
                    <span>{labelOf(f.category)}</span>
                    <span>·</span>
                    <span>{CONDITION_LABELS[f.condition]}</span>
                    <span>·</span>
                    <span className="listing-row__price">{formatPrice(f.price)}</span>
                  </div>
                </div>
                <div className="listing-row__actions">
                  <Button variant="ghost" size="sm" onClick={() => remove(f.listingId)} loading={removing === f.listingId}>
                    {t('favorites.remove')}
                  </Button>
                </div>
              </article>
            ))}
          </div>
          <Pagination page={page} totalPages={totalPages} onChange={setPage} />
        </>
      )}
    </div>
  )
}
