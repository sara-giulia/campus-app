import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { ListingGrid } from './ListingCard'
import type { GetListingsResponse, ListingSummary } from './types'
import './browse.css'
import { useT } from '../../i18n/LanguageContext'

// The newest listings, under the hero of the home page. Nothing is shown when there are none or
// when the API cannot be reached (the home page still works without it).
export function LatestListings() {
  const t = useT()
  const [items, setItems] = useState<ListingSummary[]>([])

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetListingsResponse>('/listings?pageSize=8', { signal: controller.signal })
      .then((data) => setItems(data.items))
      .catch(() => setItems([]))
    return () => controller.abort()
  }, [])

  if (items.length === 0) return null

  return (
    <section className="home-latest">
      <div className="home-latest__head">
        <h2>{t('listings.latest.title')}</h2>
        <Link to="/listings">{t('listings.latest.seeAll')}</Link>
      </div>
      <ListingGrid listings={items} />
    </section>
  )
}
