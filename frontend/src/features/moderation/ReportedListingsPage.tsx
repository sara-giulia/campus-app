import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Badge, Button, EmptyState, Loading, PageHeader, Pagination } from '../../app/ui'
import { formatDateTime } from '../listings/publicTypes'
import { LISTING_STATUS_LABELS, type ListingStatus } from '../listings/types'
import '../listings/listings.css'
import './moderation.css'

interface ReportedListingItem {
  listingId: string
  title: string
  sellerId: string
  status: ListingStatus
  reportCount: number
  flaggedAsSpam: boolean
  spamProbability: number | null
  lastReportedAt: string
  reasons: string[]
}

interface GetReportedListingsResponse {
  items: ReportedListingItem[]
  count: number
  totalCount: number
  page: number
  pageSize: number
}

const PAGE_SIZE = 20

// Admin: listings reported by members or flagged by the spam model when they were published.
// Hiding one takes it out of the search; hidden listings leave this list.
export function ReportedListingsPage() {
  const [page, setPage] = useState(1)
  const [data, setData] = useState<GetReportedListingsResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [hiding, setHiding] = useState<string | null>(null)

  // Bumped after a change, to load the page again.
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetReportedListingsResponse>(`/moderation/listings?page=${page}&pageSize=${PAGE_SIZE}`, { signal: controller.signal })
      .then((result) => {
        setData(result)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(errorMessage(err))
      })
    return () => controller.abort()
  }, [page, version])

  async function hide(item: ReportedListingItem) {
    if (!window.confirm(`Ascunzi anunțul „${item.title}”? Nu va mai apărea în căutare.`)) return
    setHiding(item.listingId)
    try {
      await api.post(`/moderation/listings/${item.listingId}/hide`)
      if (data && data.items.length === 1 && page > 1) setPage(page - 1)
      else setVersion((v) => v + 1)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setHiding(null)
    }
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  return (
    <div className="listings-page moderation">
      <PageHeader eyebrow="Administrare" title="Anunțuri raportate" lead="Raportate de membri sau marcate de modelul de spam la publicare." />
      {error && <Alert tone="error">{error}</Alert>}
      {!error && data === null && <Loading text="Se încarcă rapoartele…" />}
      {data !== null && data.totalCount === 0 && <EmptyState title="Niciun anunț raportat.">Când cineva raportează un anunț, apare aici.</EmptyState>}
      {data !== null && data.totalCount > 0 && (
        <>
          <div className="listings-list">
            {data.items.map((item) => (
              <article key={item.listingId} className="listing-row">
                <div className="moderation__body">
                  <h2 className="listing-row__title">
                    <Link to={`/listings/${item.listingId}`}>{item.title}</Link>
                  </h2>
                  <div className="listing-row__meta">
                    <Badge>{LISTING_STATUS_LABELS[item.status]}</Badge>
                    <span>{item.reportCount === 1 ? '1 raportare' : `${item.reportCount} raportări`}</span>
                    {item.flaggedAsSpam && (
                      <Badge tone="danger">
                        Spam {item.spamProbability === null ? '' : `${Math.round(item.spamProbability * 100)}%`}
                      </Badge>
                    )}
                    <span>ultima: {formatDateTime(item.lastReportedAt)}</span>
                  </div>
                  <ul className="moderation__reasons">
                    {groupReasons(item.reasons).map(([reason, times]) => (
                      <li key={reason}>
                        {reason}
                        {times > 1 && <span className="moderation__times"> (×{times})</span>}
                      </li>
                    ))}
                  </ul>
                </div>
                <div className="listing-row__actions">
                  <Button variant="danger" size="sm" onClick={() => hide(item)} loading={hiding === item.listingId}>
                    Ascunde
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

// The same reason given by several members is shown once, with how many times (newest first, as the API sends them).
function groupReasons(reasons: string[]): [string, number][] {
  const counts = new Map<string, number>()
  for (const reason of reasons) counts.set(reason, (counts.get(reason) ?? 0) + 1)
  return [...counts.entries()]
}
