// Shapes shared by the listings pages. They mirror the API DTOs in
// src/CampusMarket.Api/Features/Listings and Domain/Listings; the JSON converter
// on the API sends enums as text (see CampusMarketSetup), so the strings here are
// exactly what the API returns.

export type Category = 'Textbook' | 'Notes' | 'Electronics' | 'LabEquipment' | 'Other'
export type Condition = 'New' | 'Good' | 'Worn'
export type ListingStatus = 'Draft' | 'Active' | 'Reserved' | 'Sold' | 'Hidden'
export type ReservationStatus = 'Active' | 'Cancelled' | 'Expired' | 'Completed'

export interface ListingDetail {
  id: string
  sellerId: string
  title: string
  category: Category
  condition: Condition
  price: number
  status: ListingStatus
  version: number
  createdAt: string
  description: string | null
}

export interface ListingSummary {
  id: string
  sellerId: string
  title: string
  category: Category
  condition: Condition
  price: number
  status: ListingStatus
}

export interface GetListingsResponse {
  items: ListingSummary[]
  count: number
  totalCount: number
  page: number
  pageSize: number
}

export interface MyReservationItem {
  reservationId: string
  listingId: string
  listingTitle: string
  price: number
  status: ReservationStatus
  createdAt: string
  expiresAt: string
}

export interface GetMyReservationsResponse {
  items: MyReservationItem[]
  count: number
}

export interface SuggestPriceRequest {
  title: string
  category: Category
  condition: Condition
  description?: string | null
}

export interface SuggestPriceResponse {
  suggestedPrice: number
}

export interface SuggestCategoryRequest {
  title: string
  description?: string | null
}

export interface SuggestCategoryResponse {
  category: Category | null
  confidence: number
}

export interface CreateListingResponse {
  id: string
  sellerId: string
  title: string
  category: Category
  condition: Condition
  price: number
  status: ListingStatus
  version: number
  createdAt: string
  description: string | null
}

// Only the fallback for the category names: the real ones come from GET /api/v1/categories
// (an Admin can change them), through useCategories() in ./categories.ts.
export const CATEGORY_LABELS: Record<Category, string> = {
  Textbook: 'Manuale',
  Notes: 'Notițe',
  Electronics: 'Electronice',
  LabEquipment: 'Echipament de laborator',
  Other: 'Altele',
}

export const CONDITION_LABELS: Record<Condition, string> = {
  New: 'Nouă',
  Good: 'Bună',
  Worn: 'Uzată',
}

export const LISTING_STATUS_LABELS: Record<ListingStatus, string> = {
  Draft: 'Ciornă',
  Active: 'Activ',
  Reserved: 'Rezervat',
  Sold: 'Vândut',
  Hidden: 'Ascuns',
}

export const RESERVATION_STATUS_LABELS: Record<ReservationStatus, string> = {
  Active: 'Activă',
  Cancelled: 'Anulată',
  Expired: 'Expirată',
  Completed: 'Finalizată',
}

export const CATEGORIES: Category[] = ['Textbook', 'Notes', 'Electronics', 'LabEquipment', 'Other']
export const CONDITIONS: Condition[] = ['New', 'Good', 'Worn']
