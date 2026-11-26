import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Alert, Badge, Button, EmptyState, Loading, PageHeader } from '../../app/ui'
import { formatDate as formatLocalDate, formatRon } from '../../i18n/format'
import { ReviewForm } from '../reviews/ReviewForm'
import { placeReservationError } from './errors'
import {
  RESERVATION_STATUS_LABELS,
  type GetMyReservationsResponse,
  type MyReservationItem,
  type ReservationStatus,
} from './types'
import './listings.css'
import { useT } from '../../i18n/LanguageContext'
import { translate } from '../../i18n/store'

// GET /api/v1/members/me/reservations returns the reservations the current user
// MADE (buyer side). There is no endpoint that lists the reservations received
// on the current user's own listings, so this page is buyer-side only; the
// seller-side "confirm sale" / "cancel buyer's reservation" flow is called out
// as a gap in the end-of-task notes.

function reservationTone(status: ReservationStatus): 'neutral' | 'success' | 'warning' | 'danger' | 'accent' {
  switch (status) {
    case 'Active':
      return 'success'
    case 'Completed':
      return 'accent'
    case 'Expired':
      return 'warning'
    case 'Cancelled':
      return 'danger'
    default:
      return 'neutral'
  }
}

function formatDate(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime())
    ? iso
    : formatLocalDate(date, { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export function MyReservationsPage() {
  const t = useT()
  const [items, setItems] = useState<MyReservationItem[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [cancellingId, setCancellingId] = useState<string | null>(null)
  const [rowError, setRowError] = useState<Record<string, string | null>>({})
  // The completed purchase whose review form is open, if any.
  const [reviewingId, setReviewingId] = useState<string | null>(null)

  function markReviewed(reservationId: string) {
    setReviewingId(null)
    setItems((prev) => prev?.map((r) => (r.reservationId === reservationId ? { ...r, reviewed: true } : r)) ?? null)
  }

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetMyReservationsResponse>('/members/me/reservations', { signal: controller.signal })
      .then((data) => setItems(data.items))
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        if (err instanceof ApiError) {
          setLoadError(err.code === 'Network.Unreachable' ? translate('common.networkUnreachable') : err.message)
        } else {
          setLoadError(translate('common.unexpectedError'))
        }
      })
    return () => controller.abort()
  }, [])

  async function reload() {
    try {
      const data = await api.get<GetMyReservationsResponse>('/members/me/reservations')
      setItems(data.items)
    } catch (err) {
      if (err instanceof ApiError) {
        setLoadError(err.code === 'Network.Unreachable' ? translate('common.networkUnreachable') : err.message)
      } else {
        setLoadError(translate('common.unexpectedError'))
      }
    }
  }

  async function handleCancel(reservation: MyReservationItem) {
    if (!window.confirm(t('myReservations.confirmCancel', { title: reservation.listingTitle }))) return
    setCancellingId(reservation.reservationId)
    setRowError((prev) => ({ ...prev, [reservation.reservationId]: null }))
    try {
      await api.delete<void>(`/listings/${reservation.listingId}/reservations/${reservation.reservationId}`)
      await reload()
    } catch (err) {
      if (err instanceof ApiError) {
        setRowError((prev) => ({ ...prev, [reservation.reservationId]: placeReservationError(err) }))
      } else {
        setRowError((prev) => ({ ...prev, [reservation.reservationId]: translate('common.unexpectedError') }))
      }
    } finally {
      setCancellingId(null)
    }
  }

  if (items === null && loadError === null) return <Loading text={t('myReservations.loading')} />

  if (loadError) {
    return (
      <div className="listings-page">
        <PageHeader eyebrow={t('myListings.eyebrow')} title={t('myReservations.title')} />
        <Alert tone="error">{loadError}</Alert>
      </div>
    )
  }

  const all = items ?? []

  return (
    <div className="listings-page">
      <PageHeader
        eyebrow={t('myListings.eyebrow')}
        title={t('myReservations.title')}
        lead={t('myReservations.lead')}
      />

      {all.length === 0 ? (
        <EmptyState title={t('myReservations.emptyTitle')}>{t('myReservations.emptyText')}</EmptyState>
      ) : (
        <div className="listings-list">
          {all.map((reservation) => (
            <article key={reservation.reservationId} className="listing-row">
              <div>
                <h2 className="listing-row__title">{reservation.listingTitle}</h2>
                <div className="listing-row__meta">
                  <Badge tone={reservationTone(reservation.status)}>{RESERVATION_STATUS_LABELS[reservation.status]}</Badge>
                  {reservation.agreedPrice !== null && reservation.agreedPrice !== reservation.price ? (
                    <>
                      <span className="listing-row__price">{formatRon(reservation.agreedPrice)}</span>
                      <span>
                        {t('myReservations.negotiated', { price: formatRon(reservation.price) })}
                      </span>
                    </>
                  ) : (
                    <span className="listing-row__price">{formatRon(reservation.price)}</span>
                  )}
                  <span>·</span>
                  <span>{t('myReservations.reservedOn', { date: formatDate(reservation.createdAt) })}</span>
                  <span>·</span>
                  <span>{t('myReservations.expiresOn', { date: formatDate(reservation.expiresAt) })}</span>
                </div>
              </div>
              <div className="listing-row__actions">
                {reservation.status === 'Active' && (
                  <Button
                    variant="danger"
                    size="sm"
                    disabled={cancellingId === reservation.reservationId}
                    loading={cancellingId === reservation.reservationId}
                    onClick={() => {
                      void handleCancel(reservation)
                    }}
                  >
                    {cancellingId === reservation.reservationId ? t('myReservations.cancelling') : t('myReservations.cancel')}
                  </Button>
                )}
                {reservation.status === 'Completed' && !reservation.reviewed && reviewingId !== reservation.reservationId && (
                  <Button variant="secondary" size="sm" onClick={() => setReviewingId(reservation.reservationId)}>
                    {t('myReservations.review')}
                  </Button>
                )}
                {reservation.status === 'Completed' && reservation.reviewed && <Badge tone="neutral">{t('myReservations.reviewSent')}</Badge>}
              </div>
              {reviewingId === reservation.reservationId && (
                <div className="listing-row__inline-error">
                  <ReviewForm
                    listingId={reservation.listingId}
                    listingTitle={reservation.listingTitle}
                    onCancel={() => setReviewingId(null)}
                    onReviewed={() => markReviewed(reservation.reservationId)}
                  />
                </div>
              )}
              {rowError[reservation.reservationId] && (
                <div className="listing-row__inline-error">
                  <Alert tone="error">{rowError[reservation.reservationId]}</Alert>
                </div>
              )}
            </article>
          ))}
        </div>
      )}
    </div>
  )
}
