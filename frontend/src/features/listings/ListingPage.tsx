import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { api } from '../../api/client'
import { useAuth } from '../../app/auth/AuthContext'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Badge, Button, ButtonLink, Card, Loading } from '../../app/ui'
import { ListingGrid } from './ListingCard'
import {
  formatDate,
  formatDateTime,
  formatPrice,
  photoSrc,
  type GetListingPhotosResponse,
  type GetMyFavoritesResponse,
  type GetSimilarListingsResponse,
  type PhotoItem,
  type PublicProfile,
  type ReserveListingResponse,
  type SimilarListingItem,
} from './publicTypes'
import { useCategories } from './categories'
import { CONDITION_LABELS, LISTING_STATUS_LABELS, type ListingDetail } from './types'
import { recordListingView } from './views'
import './browse.css'
import { useT } from '../../i18n/LanguageContext'

interface MessageSent {
  conversationId: string
}

// One listing: photos, details, the seller, the actions of a buyer (favorite, reserve, write to the
// seller, report) or of the seller (photos, edit), and the similar listings.
export function ListingPage() {
  const { id = '' } = useParams()
  return <ListingView key={id} id={id} />
}

function ListingView({ id }: { id: string }) {
  const t = useT()
  const { labelOf } = useCategories()
  const { user } = useAuth()
  const navigate = useNavigate()

  const [listing, setListing] = useState<ListingDetail | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [photos, setPhotos] = useState<PhotoItem[]>([])
  const [current, setCurrent] = useState(0)
  const [seller, setSeller] = useState<PublicProfile | null>(null)
  const [similar, setSimilar] = useState<SimilarListingItem[]>([])
  const [favorite, setFavorite] = useState(false)

  const [notice, setNotice] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  const [message, setMessage] = useState('')
  const [reportOpen, setReportOpen] = useState(false)
  const [reason, setReason] = useState('')

  const isSeller = user !== null && listing !== null && user.id === listing.sellerId

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetListingPhotosResponse>(`/listings/${id}/photos`, { signal: controller.signal, auth: false })
      .then((data) => setPhotos(data.items))
      .catch(() => setPhotos([]))
    return () => controller.abort()
  }, [id])

  useEffect(() => {
    const controller = new AbortController()
    const signal = controller.signal

    api
      .get<ListingDetail>(`/listings/${id}`, { signal, auth: false })
      .then((data) => {
        setListing(data)
        api.get<PublicProfile>(`/members/${data.sellerId}/profile`, { signal, auth: false }).then(setSeller).catch(() => setSeller(null))
      })
      .catch((err: unknown) => {
        if (!isAbort(err)) setLoadError(errorMessage(err))
      })
    api
      .get<GetSimilarListingsResponse>(`/listings/${id}/similar`, { signal })
      // Four, one full row of the grid.
      .then((data) => setSimilar(data.items.slice(0, 4)))
      .catch(() => setSimilar([]))

    return () => controller.abort()
  }, [id])

  // One view per visit, counted by the API (unique per viewer and day; not the seller's). The listing
  // itself may come from the cache, so the page tells the API. Never blocks or shows an error.
  useEffect(() => {
    recordListingView(id)
  }, [id])

  // Is it in my favorites? There is no single-listing check, so look in the first 100 favorites.
  useEffect(() => {
    if (!user) return
    const controller = new AbortController()
    api
      .get<GetMyFavoritesResponse>('/members/me/favorites?pageSize=100', { signal: controller.signal })
      .then((data) => setFavorite(data.items.some((f) => f.listingId === id)))
      .catch(() => setFavorite(false))
    return () => controller.abort()
  }, [id, user])

  async function run(name: string, action: () => Promise<void>) {
    setBusy(name)
    setActionError(null)
    setNotice(null)
    try {
      await action()
    } catch (err) {
      setActionError(errorMessage(err))
    } finally {
      setBusy(null)
    }
  }

  const toggleFavorite = () =>
    run('favorite', async () => {
      if (favorite) await api.delete(`/members/me/favorites/${id}`)
      else await api.post(`/members/me/favorites/${id}`)
      setFavorite(!favorite)
    })

  const reserve = () =>
    run('reserve', async () => {
      const r = await api.post<ReserveListingResponse>(`/listings/${id}/reservations`)
      setListing(await api.get<ListingDetail>(`/listings/${id}`))
      setNotice(t('listingPage.reserved', { date: formatDateTime(r.expiresAt) }))
    })

  const sendMessage = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    return run('message', async () => {
      const sent = await api.post<MessageSent>(`/listings/${id}/conversations`, { text: message })
      navigate(`/messages/${sent.conversationId}`)
    })
  }

  const report = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    return run('report', async () => {
      await api.post(`/listings/${id}/reports`, { reason })
      setReportOpen(false)
      setReason('')
      setNotice(t('listingPage.reportThanks'))
    })
  }

  if (loadError) {
    return (
      <div className="listing-page">
        <Alert tone="error">{loadError}</Alert>
        <ButtonLink to="/listings" variant="secondary">
          {t('listingPage.backToListings')}
        </ButtonLink>
      </div>
    )
  }
  if (!listing) return <Loading text={t('listingPage.loading')} />

  const canBuy = listing.status === 'Active'
  const canWrite = listing.status === 'Active' || listing.status === 'Reserved'
  // A deleted photo can leave current past the end.
  const shown = Math.min(current, Math.max(0, photos.length - 1))
  const photo = photos[shown]

  return (
    <div className="listing-page">
      <Link to="/listings" className="listing-page__back">
        {t('listingPage.all')}
      </Link>

      <div className="listing-page__top">
        <div className="gallery">
          <div className={`gallery__main lcard__image--${listing.category.toLowerCase()}`}>
            {photo ? <img src={photoSrc(photo.url)} alt={listing.title} /> : <span>{labelOf(listing.category)}</span>}
            {photo?.isGenerated && (
              <span className="generated-badge" title={t('listingPage.generatedHint')}>
                {t('listings.card.generated')}
              </span>
            )}
          </div>
          {photos.length > 1 && (
            <div className="gallery__thumbs">
              {photos.map((p, i) => (
                <button key={p.id} type="button" className={i === shown ? 'gallery__thumb is-current' : 'gallery__thumb'} onClick={() => setCurrent(i)} aria-label={t('listingPage.photoNumber', { number: i + 1 })}>
                  <img src={photoSrc(p.thumbnailUrl)} alt="" loading="lazy" />
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="listing-page__info">
          <div className="listing-page__badges">
            <Badge tone="accent">{labelOf(listing.category)}</Badge>
            <Badge>{CONDITION_LABELS[listing.condition]}</Badge>
            {listing.status !== 'Active' && <Badge tone="warning">{LISTING_STATUS_LABELS[listing.status]}</Badge>}
          </div>
          <h1 className="listing-page__title">{listing.title}</h1>
          <p className="listing-page__price">{formatPrice(listing.price)}</p>
          <p className="listing-page__date">{t('listingPage.publishedOn', { date: formatDate(listing.createdAt) })}</p>

          {seller && (
            <Link to={`/members/${seller.id}`} className="seller-chip">
              <span className="seller-chip__avatar">{seller.name.slice(0, 1).toUpperCase()}</span>
              <span>
                <strong>{seller.name}</strong>
                <small>{seller.averageRating === null ? t('listingPage.noReviews') : t('listingPage.reviews', { rating: seller.averageRating.toFixed(1), count: seller.reviewCount })}</small>
              </span>
            </Link>
          )}

          {notice && <Alert tone="success">{notice}</Alert>}
          {actionError && <Alert tone="error">{actionError}</Alert>}

          {!user && (
            <Card className="listing-page__actions">
              <p>{t('listingPage.loginPrompt')}</p>
              <ButtonLink to="/login">{t('layout.login')}</ButtonLink>
            </Card>
          )}

          {user && isSeller && (
            <Card className="listing-page__actions">
              <p>{t('listingPage.yours')}</p>
              <ButtonLink to={`/my/listings/${listing.id}/edit`} variant="secondary">
                {t('listingPage.edit')}
              </ButtonLink>
            </Card>
          )}

          {user && !isSeller && (
            <Card className="listing-page__actions">
              <div className="listing-page__buttons">
                {canBuy && (
                  <Button onClick={reserve} loading={busy === 'reserve'}>
                    {t('listingPage.reserve')}
                  </Button>
                )}
                <Button variant="secondary" onClick={toggleFavorite} loading={busy === 'favorite'}>
                  {favorite ? t('listingPage.saved') : t('listingPage.save')}
                </Button>
              </div>

              {canWrite && (
                <form className="listing-page__message" onSubmit={sendMessage}>
                  <label className="field__label" htmlFor="message">
                    {t('listingPage.writeToSeller')}
                  </label>
                  <textarea
                    id="message"
                    className="listing-page__textarea"
                    placeholder={t('listingPage.messagePlaceholder')}
                    maxLength={1000}
                    value={message}
                    onChange={(e) => setMessage(e.target.value)}
                  />
                  <Button type="submit" variant="secondary" loading={busy === 'message'} disabled={message.trim() === ''}>
                    {t('listingPage.sendMessage')}
                  </Button>
                </form>
              )}

              {canWrite && !reportOpen && (
                <button type="button" className="listing-page__report-link" onClick={() => setReportOpen(true)}>
                  {t('listingPage.report')}
                </button>
              )}
              {reportOpen && (
                <form className="listing-page__message" onSubmit={report}>
                  <label className="field__label" htmlFor="reason">
                    {t('listingPage.reportWhy')}
                  </label>
                  <textarea id="reason" className="listing-page__textarea" maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
                  <div className="listing-page__buttons">
                    <Button type="submit" variant="danger" size="sm" loading={busy === 'report'} disabled={reason.trim() === ''}>
                      {t('listingPage.sendReport')}
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => setReportOpen(false)}>
                      {t('listingPage.cancelReport')}
                    </Button>
                  </div>
                </form>
              )}
            </Card>
          )}
        </div>
      </div>

      {listing.description && (
        <section className="listing-page__section">
          <h2 className="listing-page__section-title">{t('listingPage.description')}</h2>
          <p className="listing-page__description">{listing.description}</p>
        </section>
      )}

      {similar.length > 0 && (
        <section className="listing-page__section">
          <h2 className="listing-page__section-title">{t('listingPage.similar')}</h2>
          <ListingGrid listings={similar} />
        </section>
      )}
    </div>
  )
}
