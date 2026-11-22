import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Badge, Button, EmptyState, Loading, PageHeader, Pagination } from '../../app/ui'
import { useNotifications } from './NotificationsContext'
import { formatAbsoluteDateTime, formatRelativeTime } from './relativeTime'
import type { GetMyNotificationsResponse, NotificationItem } from './types'
import './notifications.css'

const PAGE_SIZE = 10

// Short Romanian labels for the API's notification types, used as the eyebrow on each item.
const TYPE_LABELS: Record<NotificationItem['type'], string> = {
  ReservationCreated: 'Rezervare nouă',
  ListingSold: 'Anunț vândut',
  ReservationExpired: 'Rezervare expirată',
}

// The newest-first notifications of the logged-in member, with mark-one-as-read,
// mark-all-as-read, pagination and a link to the related listing when the API gave one.
export function NotificationsPage() {
  const { refresh: refreshBadge } = useNotifications()

  const [page, setPage] = useState(1)
  const [data, setData] = useState<GetMyNotificationsResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [markingId, setMarkingId] = useState<string | null>(null)
  const [markingAll, setMarkingAll] = useState(false)
  // Bumped after a change to re-load the current page.
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetMyNotificationsResponse>(`/members/me/notifications?page=${page}&pageSize=${PAGE_SIZE}`, { signal: controller.signal })
      .then((result) => {
        setData(result)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(errorMessage(err))
      })
    return () => controller.abort()
  }, [page, version])

  async function markOne(id: string) {
    setMarkingId(id)
    try {
      await api.post<void>(`/members/me/notifications/${id}/read`)
      setVersion((v) => v + 1)
      refreshBadge()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setMarkingId(null)
    }
  }

  async function markAll() {
    if (!data || data.unreadCount === 0) return
    setMarkingAll(true)
    try {
      await api.post<void>('/members/me/notifications/read-all')
      setVersion((v) => v + 1)
      refreshBadge()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setMarkingAll(false)
    }
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  return (
    <div className="notifications">
      <PageHeader eyebrow="Contul meu" title="Notificări" lead="Ce s-a întâmplat cu anunțurile și rezervările tale." />

      {error && <Alert tone="error">{error}</Alert>}
      {!error && data === null && <Loading text="Se încarcă notificările…" />}

      {data !== null && data.totalCount === 0 && <EmptyState title="Nu ai notificări.">Îți dăm de știre aici când cineva îți rezervă un anunț sau când o rezervare expiră.</EmptyState>}

      {data !== null && data.totalCount > 0 && (
        <>
          <div className="notifications__toolbar">
            <span className="notifications__count">
              {data.unreadCount > 0 ? `${data.unreadCount} necitite` : 'Toate citite'}
            </span>
            <Button variant="secondary" size="sm" onClick={() => void markAll()} loading={markingAll} disabled={data.unreadCount === 0}>
              Marchează toate ca citite
            </Button>
          </div>

          <ul className="notif-list">
            {data.items.map((n) => (
              <li key={n.id} className={n.readAt ? 'notif-item' : 'notif-item notif-item--unread'}>
                <div className="notif-item__body">
                  <div className="notif-item__top">
                    <Badge tone={n.readAt ? 'neutral' : 'accent'}>{TYPE_LABELS[n.type]}</Badge>
                    <time className="notif-item__time" dateTime={n.createdAt} title={formatAbsoluteDateTime(n.createdAt)}>
                      {formatRelativeTime(n.createdAt)}
                    </time>
                  </div>
                  <p className="notif-item__message">{n.message}</p>
                  {n.listingId && (
                    <p className="notif-item__link">
                      <Link to={`/listings/${n.listingId}`}>Vezi anunțul</Link>
                    </p>
                  )}
                </div>
                {!n.readAt && (
                  <div className="notif-item__actions">
                    <Button variant="ghost" size="sm" onClick={() => void markOne(n.id)} loading={markingId === n.id}>
                      Marchează ca citită
                    </Button>
                  </div>
                )}
              </li>
            ))}
          </ul>

          <Pagination page={page} totalPages={totalPages} onChange={setPage} />
        </>
      )}
    </div>
  )
}
