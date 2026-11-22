import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { api } from '../../api/client'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Card, EmptyState, Loading, Pagination } from '../../app/ui'
import { formatDate, formatPrice, type PublicProfile } from '../listings/publicTypes'
import './reviews.css'

interface MemberReview {
  id: string
  listingId: string
  reviewerId: string
  rating: number
  comment: string | null
  createdAt: string
}

interface GetMemberReviewsResponse {
  memberId: string
  averageRating: number | null
  reviewCount: number
  items: MemberReview[]
  count: number
  page: number
  pageSize: number
}

const PAGE_SIZE = 10

// A seller's public page: name, rating, active listings and the reviews buyers left after a sale.
export function SellerProfilePage() {
  const { id = '' } = useParams()
  return <SellerProfile key={id} id={id} />
}

function SellerProfile({ id }: { id: string }) {
  const [profile, setProfile] = useState<PublicProfile | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [reviews, setReviews] = useState<GetMemberReviewsResponse | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<PublicProfile>(`/members/${id}/profile`, { signal: controller.signal, auth: false })
      .then(setProfile)
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(errorMessage(err))
      })
    return () => controller.abort()
  }, [id])

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetMemberReviewsResponse>(`/members/${id}/reviews?page=${page}&pageSize=${PAGE_SIZE}`, { signal: controller.signal, auth: false })
      .then(setReviews)
      .catch(() => setReviews(null))
    return () => controller.abort()
  }, [id, page])

  if (error) return <Alert tone="error">{error}</Alert>
  if (!profile) return <Loading text="Se încarcă profilul…" />

  const totalPages = reviews ? Math.max(1, Math.ceil(reviews.reviewCount / PAGE_SIZE)) : 1

  return (
    <div className="seller">
      <header className="seller__head">
        <span className="seller__avatar">{profile.name.slice(0, 1).toUpperCase()}</span>
        <div>
          <h1 className="seller__name">{profile.name}</h1>
          <p className="seller__meta">
            Membru din {formatDate(profile.memberSince)}
            {' · '}
            {profile.averageRating === null ? 'fără review-uri încă' : <Stars value={profile.averageRating} label={`${profile.averageRating.toFixed(1)} din ${profile.reviewCount} review-uri`} />}
          </p>
        </div>
      </header>

      <Card>
        <h2 className="seller__section-title">Anunțuri active</h2>
        {profile.activeListings.length === 0 ? (
          <p className="seller__muted">Nu are anunțuri active acum.</p>
        ) : (
          <ul className="seller__listings">
            {profile.activeListings.map((l) => (
              <li key={l.id}>
                <Link to={`/listings/${l.id}`}>{l.title}</Link>
                <span>{formatPrice(l.price)}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card>
        <h2 className="seller__section-title">Review-uri</h2>
        {reviews === null || reviews.reviewCount === 0 ? (
          <EmptyState title="Niciun review încă.">Review-urile apar după ce un cumpărător confirmă o vânzare.</EmptyState>
        ) : (
          <>
            <ul className="seller__reviews">
              {reviews.items.map((r) => (
                <li key={r.id}>
                  <div className="seller__review-top">
                    <Stars value={r.rating} label={`${r.rating} din 5`} />
                    <time>{formatDate(r.createdAt)}</time>
                  </div>
                  {r.comment && <p>{r.comment}</p>}
                </li>
              ))}
            </ul>
            <Pagination page={page} totalPages={totalPages} onChange={setPage} />
          </>
        )}
      </Card>
    </div>
  )
}

function Stars({ value, label }: { value: number; label: string }) {
  const full = Math.round(value)
  return (
    <span className="stars" title={label} aria-label={label}>
      {'★'.repeat(full)}
      <span className="stars__empty">{'★'.repeat(5 - full)}</span>
      <span className="stars__text">{label}</span>
    </span>
  )
}
