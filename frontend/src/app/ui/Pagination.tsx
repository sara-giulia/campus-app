import { useT } from '../../i18n/LanguageContext'

// Page numbers for the paged API answers (page starts at 1).
// <Pagination page={page} totalPages={data.totalPages} onChange={setPage} />
export function Pagination({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (page: number) => void }) {
  const t = useT()
  if (totalPages <= 1) return null

  return (
    <nav className="pagination" aria-label={t('pagination.label')}>
      <button type="button" className="pagination__page" disabled={page <= 1} onClick={() => onChange(page - 1)} aria-label={t('pagination.previous')}>
        ‹
      </button>
      {pagesToShow(page, totalPages).map((p, i) =>
        p === null ? (
          <span key={`gap-${i}`} className="pagination__gap">
            …
          </span>
        ) : (
          <button key={p} type="button" className="pagination__page" aria-current={p === page ? 'page' : undefined} onClick={() => onChange(p)}>
            {p}
          </button>
        ),
      )}
      <button type="button" className="pagination__page" disabled={page >= totalPages} onClick={() => onChange(page + 1)} aria-label={t('pagination.next')}>
        ›
      </button>
    </nav>
  )
}

// First, last, and two around the current page; null where pages are skipped.
function pagesToShow(page: number, total: number): (number | null)[] {
  const wanted = new Set([1, total, page - 1, page, page + 1].filter((p) => p >= 1 && p <= total))
  const sorted = [...wanted].sort((a, b) => a - b)
  const result: (number | null)[] = []
  sorted.forEach((p, i) => {
    if (i > 0 && p - sorted[i - 1] > 1) result.push(null)
    result.push(p)
  })
  return result
}
