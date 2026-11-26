import { useEffect, useSyncExternalStore } from 'react'
import { api } from '../../api/client'
import { CATEGORIES, CATEGORY_LABELS, type Category } from './types'

// The categories from GET /api/v1/categories, loaded once and shared by every page and card
// (one request for the whole app, not one per component). An Admin changes the display name,
// the description and the active flag on /admin/categories; the 5 keys never change.
//
// Usage: const { categories, labelOf, loaded } = useCategories()
//   labelOf('Textbook')           → the display name from the API
//   categories.filter(c => c.isActive) → what the publish and edit forms offer
// Until the first answer (or if it fails), the old built-in labels are used, all active,
// so the pages never show an empty name.

export interface CategoryItem {
  key: Category
  displayName: string
  description: string | null
  isActive: boolean
}

export interface GetCategoriesResponse {
  items: CategoryItem[]
  count: number
}

interface CategoriesSnapshot {
  categories: CategoryItem[]
  // true once the API answered; false while the fallback is shown.
  loaded: boolean
  // The error of the last failed request (null when the last request worked).
  error: unknown
}

const FALLBACK: CategoryItem[] = CATEGORIES.map((key) => ({
  key,
  // A getter, so the fallback name follows the language.
  get displayName() {
    return CATEGORY_LABELS[key]
  },
  description: null,
  isActive: true,
}))

let snapshot: CategoriesSnapshot = { categories: FALLBACK, loaded: false, error: null }
let inFlight: Promise<void> | null = null
const listeners = new Set<() => void>()

function setSnapshot(next: CategoriesSnapshot) {
  snapshot = next
  for (const listener of listeners) listener()
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

function getSnapshot(): CategoriesSnapshot {
  return snapshot
}

// Never rejects: on an error the current list stays (the fallback before the first answer)
// and the error is kept in the snapshot, so a page can show it.
async function fetchCategories(): Promise<void> {
  try {
    const data = await api.get<GetCategoriesResponse>('/categories', { auth: false })
    setSnapshot({ categories: data.items, loaded: true, error: null })
  } catch (error) {
    setSnapshot({ ...snapshot, error })
  } finally {
    inFlight = null
  }
}

// Returns nothing: the request handles its own errors, so a component can just start it.
// Does nothing when a request is on its way, or when the categories are loaded and fresh is false.
function startLoading(fresh: boolean): void {
  if (inFlight || (snapshot.loaded && !fresh)) return
  inFlight = fetchCategories()
}

// Loads the categories again (after an Admin changed one), so every page shows the new names.
// Waits for a request already on its way first, so an older answer cannot overwrite the new one.
export async function reloadCategories(): Promise<void> {
  if (inFlight) await inFlight
  inFlight = fetchCategories()
  await inFlight
}

// fresh: true loads the categories again when the component mounts (the Admin page wants the
// current values, not the ones loaded when the app started). The other pages load them only once.
export function useCategories({ fresh = false }: { fresh?: boolean } = {}) {
  const current = useSyncExternalStore(subscribe, getSnapshot)

  useEffect(() => {
    startLoading(fresh)
  }, [fresh])

  return {
    categories: current.categories,
    loaded: current.loaded,
    error: current.error,
    labelOf: (key: Category): string =>
      current.categories.find((c) => c.key === key)?.displayName ?? CATEGORY_LABELS[key],
  }
}
