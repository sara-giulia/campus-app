// Shapes of the public listing pages (browse, listing page, similar listings, photos). They mirror
// the API answers in src/CampusMarket.Api/Features/Listings and Features/Photos.
import type { Category, Condition } from './types'

export interface PhotoItem {
  id: string
  listingId: string
  url: string
  contentType: string
  sizeBytes: number
  createdAt: string
  // Made by the app for a listing without photos (title and category on a coloured picture); it goes
  // away when the seller adds a real photo.
  isGenerated: boolean
}

export interface GetListingPhotosResponse {
  items: PhotoItem[]
  count: number
}

export interface SimilarListingItem {
  id: string
  title: string
  category: Category
  condition: Condition
  price: number
  similarity: number
}

export interface GetSimilarListingsResponse {
  items: SimilarListingItem[]
}

export interface PublicProfile {
  id: string
  name: string
  memberSince: string
  averageRating: number | null
  reviewCount: number
  activeListings: { id: string; title: string; price: number }[]
}

export interface MyFavoriteItem {
  listingId: string
  title: string
  category: Category
  condition: Condition
  price: number
  status: 'Draft' | 'Active' | 'Reserved' | 'Sold' | 'Hidden'
  favoritedAt: string
}

export interface GetMyFavoritesResponse {
  items: MyFavoriteItem[]
  count: number
  totalCount: number
  page: number
  pageSize: number
}

export interface ReserveListingResponse {
  id: string
  listingId: string
  buyerId: string
  createdAt: string
  expiresAt: string
  status: string
}

export type SortOption = 'relevance' | 'newest' | 'price_asc' | 'price_desc'

export const SORT_LABELS: Record<SortOption, string> = {
  relevance: 'Cele mai relevante',
  newest: 'Cele mai noi',
  price_asc: 'Preț crescător',
  price_desc: 'Preț descrescător',
}

// "/api/v1/photos/{id}" from the API, made absolute when the API runs on another origin.
export { photoSrc } from './photoSrc'

const dateFormatter = new Intl.DateTimeFormat('ro-RO', { year: 'numeric', month: 'long', day: 'numeric' })
const dateTimeFormatter = new Intl.DateTimeFormat('ro-RO', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

export function formatDate(iso: string): string {
  return dateFormatter.format(new Date(iso))
}

export function formatDateTime(iso: string): string {
  return dateTimeFormatter.format(new Date(iso))
}

export function formatPrice(price: number): string {
  return `${price.toLocaleString('ro-RO')} lei`
}
