import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { Badge } from '../../app/ui'
import { formatPrice, photoSrc, type GetListingPhotosResponse, type PhotoItem } from './publicTypes'
import { useCategories } from './categories'
import { CONDITION_LABELS, type Category, type Condition, type SearchMatch } from './types'
import { useT } from '../../i18n/LanguageContext'

interface CardListing {
  id: string
  title: string
  category: Category
  condition: Condition
  price: number
  match?: SearchMatch | null
}

// A listing in a grid: the first photo (or a coloured tile with the category), title, price.
// In the search results, a listing found only by meaning (its title does not have the words) is marked.
export function ListingCard({ listing }: { listing: CardListing }) {
  const t = useT()
  const { labelOf } = useCategories()
  const [cover, setCover] = useState<PhotoItem | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetListingPhotosResponse>(`/listings/${listing.id}/photos`, { signal: controller.signal, auth: false })
      .then((data) => setCover(data.items[0] ?? null))
      .catch(() => setCover(null))
    return () => controller.abort()
  }, [listing.id])

  return (
    <Link to={`/listings/${listing.id}`} className="lcard">
      <div className={`lcard__image lcard__image--${listing.category.toLowerCase()}`}>
        {cover ? <img src={photoSrc(cover.thumbnailUrl)} alt="" loading="lazy" /> : <span>{labelOf(listing.category)}</span>}
        {cover?.isGenerated && <span className="generated-badge">{t('listings.card.generated')}</span>}
      </div>
      <div className="lcard__body">
        <h3 className="lcard__title">{listing.title}</h3>
        <p className="lcard__price">{formatPrice(listing.price)}</p>
        <p className="lcard__meta">
          {labelOf(listing.category)} · {CONDITION_LABELS[listing.condition]}
        </p>
        {listing.match === 'Meaning' && (
          <p className="lcard__match">
            <Badge tone="accent">{t('listings.card.byMeaning')}</Badge>
          </p>
        )}
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
