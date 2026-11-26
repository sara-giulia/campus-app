import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { api } from '../../api/client'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Button, EmptyState, Loading, PageHeader, Pagination, TextField } from '../../app/ui'
import { ListingGrid } from './ListingCard'
import { SORT_LABELS, type SortOption } from './publicTypes'
import { useCategories } from './categories'
import { CATEGORIES, CONDITIONS, CONDITION_LABELS, type GetListingsResponse } from './types'
import './browse.css'

const PAGE_SIZE = 12
// With a search the default order is "relevance" (the words first, then the meaning); without one, "newest".
const PRICE_SORTS: SortOption[] = ['price_asc', 'price_desc']
const FILTER_KEYS = ['search', 'category', 'condition', 'minPrice', 'maxPrice', 'sort'] as const

// Search and filters live in the address (?search=…&category=…&page=2), so a search can be
// shared, the back button works and a refresh keeps it. GET /api/v1/listings returns only Active listings.
export function BrowsePage() {
  const { labelOf } = useCategories()
  const [params, setParams] = useSearchParams()
  const page = Math.max(1, Number(params.get('page')) || 1)

  const [form, setForm] = useState(() => readForm(params))
  const [data, setData] = useState<GetListingsResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  const query = buildQuery(params, page)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetListingsResponse>(`/listings?${query}`, { signal: controller.signal })
      .then((result) => {
        setData(result)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(errorMessage(err))
      })
    return () => controller.abort()
  }, [query])

  function apply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const next = new URLSearchParams()
    for (const key of FILTER_KEYS) {
      const value = form[key].trim()
      if (value) next.set(key, value)
    }
    setParams(next)
  }

  function reset() {
    setForm(readForm(new URLSearchParams()))
    setParams(new URLSearchParams())
  }

  function goToPage(p: number) {
    const next = new URLSearchParams(params)
    next.set('page', String(p))
    setParams(next)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  return (
    <div className="browse">
      <PageHeader eyebrow="Anunțuri" title="Caută în campus" lead="Manuale, notițe, electronice și echipament de laborator de la colegi." />

      <form className="browse__filters" onSubmit={apply}>
        <TextField
          label="Caută"
          placeholder="ex. analiză matematică"
          value={form.search}
          onChange={(e) => setForm({ ...form, search: e.target.value })}
          className="browse__search"
        />
        <SelectField label="Categorie" value={form.category} onChange={(v) => setForm({ ...form, category: v })}>
          <option value="">Toate</option>
          {CATEGORIES.map((c) => (
            <option key={c} value={c}>
              {labelOf(c)}
            </option>
          ))}
        </SelectField>
        <SelectField label="Stare" value={form.condition} onChange={(v) => setForm({ ...form, condition: v })}>
          <option value="">Oricare</option>
          {CONDITIONS.map((c) => (
            <option key={c} value={c}>
              {CONDITION_LABELS[c]}
            </option>
          ))}
        </SelectField>
        <TextField label="Preț minim" type="number" min={0} value={form.minPrice} onChange={(e) => setForm({ ...form, minPrice: e.target.value })} />
        <TextField label="Preț maxim" type="number" min={0} value={form.maxPrice} onChange={(e) => setForm({ ...form, maxPrice: e.target.value })} />
        <SelectField label="Ordine" value={form.sort} onChange={(v) => setForm({ ...form, sort: v })}>
          <option value="">{form.search.trim() ? SORT_LABELS.relevance : SORT_LABELS.newest}</option>
          {form.search.trim() && <option value="newest">{SORT_LABELS.newest}</option>}
          {PRICE_SORTS.map((s) => (
            <option key={s} value={s}>
              {SORT_LABELS[s]}
            </option>
          ))}
        </SelectField>
        <div className="browse__filter-actions">
          <Button type="submit">Caută</Button>
          <Button variant="ghost" onClick={reset}>
            Resetează
          </Button>
        </div>
      </form>

      {error && <Alert tone="error">{error}</Alert>}
      {!error && data === null && <Loading text="Se încarcă anunțurile…" />}
      {!error && data !== null && data.totalCount === 0 && (
        <EmptyState title="Niciun anunț nu se potrivește.">Încearcă alte cuvinte sau scoate o parte din filtre.</EmptyState>
      )}
      {!error && data !== null && data.totalCount > 0 && (
        <>
          <p className="browse__count">
            {data.totalCount === 1 ? '1 anunț' : `${data.totalCount} anunțuri`}
          </p>
          {data.items.some((l) => l.match === 'Meaning') && (
            <p className="browse__hint">Anunțurile „Găsit după sens” nu conțin cuvintele căutate, dar au un înțeles apropiat.</p>
          )}
          <ListingGrid listings={data.items} />
          <Pagination page={page} totalPages={totalPages} onChange={goToPage} />
        </>
      )}
    </div>
  )
}

function readForm(params: URLSearchParams) {
  return {
    search: params.get('search') ?? '',
    category: params.get('category') ?? '',
    condition: params.get('condition') ?? '',
    minPrice: params.get('minPrice') ?? '',
    maxPrice: params.get('maxPrice') ?? '',
    sort: params.get('sort') ?? '',
  }
}

function buildQuery(params: URLSearchParams, page: number): string {
  const q = new URLSearchParams()
  for (const key of FILTER_KEYS) {
    const value = params.get(key)
    if (value) q.set(key, value)
  }
  q.set('page', String(page))
  q.set('pageSize', String(PAGE_SIZE))
  return q.toString()
}

function SelectField({ label, value, onChange, children }: { label: string; value: string; onChange: (value: string) => void; children: ReactNode }) {
  return (
    <label className="field">
      <span className="field__label">{label}</span>
      <select className="field__input browse__select" value={value} onChange={(e) => onChange(e.target.value)}>
        {children}
      </select>
    </label>
  )
}
