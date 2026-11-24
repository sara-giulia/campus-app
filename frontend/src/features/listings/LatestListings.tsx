import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { ListingGrid } from './ListingCard'
import type { GetListingsResponse, ListingSummary } from './types'
import './browse.css'

// The newest listings, under the hero of the home page. Nothing is shown when there are none or
// when the API cannot be reached (the home page still works without it).
export function LatestListings() {
  const [items, setItems] = useState<ListingSummary[]>([])

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetListingsResponse>('/listings?pageSize=8', { signal: controller.signal, auth: false })
      .then((data) => setItems(data.items))
      .catch(() => setItems([]))
    return () => controller.abort()
  }, [])

  if (items.length === 0) return null

  return (
    <section className="home-latest">
      <div className="home-latest__head">
        <h2>Anunțuri noi</h2>
        <Link to="/listings">Vezi toate →</Link>
      </div>
      <ListingGrid listings={items} />
    </section>
  )
}
