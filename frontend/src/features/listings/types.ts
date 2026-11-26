import { translate } from '../../i18n/store'
// Shapes shared by the listings pages. They mirror the API DTOs in
// src/CampusMarket.Api/Features/Listings and Domain/Listings; the JSON converter
// on the API sends enums as text (see CampusMarketSetup), so the strings here are
// exactly what the API returns.

export type Category = 'Textbook' | 'Notes' | 'Electronics' | 'LabEquipment' | 'Other'
export type Condition = 'New' | 'Good' | 'Worn'
export type ListingStatus = 'Draft' | 'Active' | 'Reserved' | 'Sold' | 'Hidden' | 'Expired'
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

// How the search found a listing: its title has the searched words, or only its meaning is close.
export type SearchMatch = 'Text' | 'Meaning'

export interface ListingSummary {
  id: string
  sellerId: string
  title: string
  category: Category
  condition: Condition
  price: number
  status: ListingStatus
  // Only with a search (GET /listings?search=…).
  match?: SearchMatch | null
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
  // The listing's price.
  price: number
  // The price agreed through an accepted offer (features/offers); null when reserved at the listing's price.
  agreedPrice: number | null
  status: ReservationStatus
  createdAt: string
  expiresAt: string
  // True once the buyer has reviewed this listing (only after a completed sale).
  reviewed: boolean
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
// The label maps are getters, so they always give the text of the current language.
export const CATEGORY_LABELS: Record<Category, string> = {
  get Textbook() {
    return translate('listings.category.Textbook')
  },
  get Notes() {
    return translate('listings.category.Notes')
  },
  get Electronics() {
    return translate('listings.category.Electronics')
  },
  get LabEquipment() {
    return translate('listings.category.LabEquipment')
  },
  get Other() {
    return translate('listings.category.Other')
  },
}

export const CONDITION_LABELS: Record<Condition, string> = {
  get New() {
    return translate('listings.condition.New')
  },
  get Good() {
    return translate('listings.condition.Good')
  },
  get Worn() {
    return translate('listings.condition.Worn')
  },
}

export const LISTING_STATUS_LABELS: Record<ListingStatus, string> = {
  get Draft() {
    return translate('listings.status.Draft')
  },
  get Active() {
    return translate('listings.status.Active')
  },
  get Reserved() {
    return translate('listings.status.Reserved')
  },
  get Sold() {
    return translate('listings.status.Sold')
  },
  get Hidden() {
    return translate('listings.status.Hidden')
  },
  get Expired() {
    return translate('listings.status.Expired')
  },
}

export const RESERVATION_STATUS_LABELS: Record<ReservationStatus, string> = {
  get Active() {
    return translate('listings.reservation.Active')
  },
  get Cancelled() {
    return translate('listings.reservation.Cancelled')
  },
  get Expired() {
    return translate('listings.reservation.Expired')
  },
  get Completed() {
    return translate('listings.reservation.Completed')
  },
}

export const CATEGORIES: Category[] = ['Textbook', 'Notes', 'Electronics', 'LabEquipment', 'Other']
export const CONDITIONS: Condition[] = ['New', 'Good', 'Worn']
