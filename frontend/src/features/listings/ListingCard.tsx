import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { formatPrice, photoSrc, type GetListingPhotosResponse } from './publicTypes'
import { CATEGORY_LABELS, CONDITION_LABELS, type Category, type Condition } from './types'

interface CardListing {
  id: string
  title: string
  category: Category
  condition: Condition
  price: number
}

// A listing in a grid: the first photo (or a coloured tile with the category), title, price.
export function ListingCard({ listing }: { listing: CardListing }) {
  const [cover, setCover] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetListingPhotosResponse>(`/listings/${listing.id}/photos`, { signal: controller.signal, auth: false })
      .then((data) => setCover(data.items[0]?.url ?? null))
      .catch(() => setCover(null))
    return () => controller.abort()
  }, [listing.id])

  return (
    <Link to={`/listings/${listing.id}`} className="lcard">
      <div className={`lcard__image lcard__image--${listing.category.toLowerCase()}`}>
        {cover ? <img src={photoSrc(cover)} alt="" loading="lazy" /> : <span>{CATEGORY_LABELS[listing.category]}</span>}
      </div>
      <div className="lcard__body">
        <h3 className="lcard__title">{listing.title}</h3>
        <p className="lcard__price">{formatPrice(listing.price)}</p>
        <p className="lcard__meta">
          {CATEGORY_LABELS[listing.category]} · {CONDITION_LABELS[listing.condition]}
        </p>
      </div>
    </Link>
  )
}

export function ListingGrid({ listings }: { listings: CardListing[] }) {
  return (
    <div className="lgrid">
      {listings.map((l) => (
        <ListingCard key={l.id} listing={l} />
      ))}
    </div>
  )
}
