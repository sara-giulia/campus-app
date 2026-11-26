import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Alert, Loading } from '../../app/ui'
import { formatDate } from '../../i18n/format'
import type { TKey } from '../../i18n/ro'
import { translate } from '../../i18n/store'
import { useT } from '../../i18n/LanguageContext'

type Counts = { views: number; favorites: number; messages: number; offers: number }
type StatsDay = Counts & { date: string }
type ListingStatsResponse = { days: number; totals: Counts; daily: StatsDay[] }

type Metric = keyof Counts

const METRICS: { key: Metric; label: TKey }[] = [
  { key: 'views', label: 'stats.views' },
  { key: 'favorites', label: 'stats.favorites' },
  { key: 'messages', label: 'stats.messages' },
  { key: 'offers', label: 'stats.offers' },
]

const PERIODS = [7, 30, 90]

// "2026-11-26" → "26 Nov" / "26 Nov" in the current language.
function dayLabel(date: string): string {
  return formatDate(date, { day: 'numeric', month: 'short', timeZone: 'UTC' })
}

const W = 600
const H = 160

// Plain SVG bars: one per day, the height from the largest value of the period.
function BarChart({ daily, metric, label }: { daily: StatsDay[]; metric: Metric; label: string }) {
  const max = Math.max(...daily.map((d) => d[metric]))
  const scale = Math.max(1, max)
  const slot = W / daily.length
  const barW = Math.max(1, slot * 0.7)

  return (
    <div className="stats__chart">
      <svg viewBox={`0 0 ${W} ${H}`} role="img" aria-label={translate('stats.byDay', { label })} preserveAspectRatio="none">
        <line x1="0" y1={H} x2={W} y2={H} className="stats__axis" />
        {daily.map((d, i) => {
          const h = (d[metric] / scale) * (H - 4)
          return (
            <rect key={d.date} x={i * slot + (slot - barW) / 2} y={H - h} width={barW} height={h} className="stats__bar">
              <title>{`${dayLabel(d.date)}: ${d[metric]}`}</title>
            </rect>
          )
        })}
      </svg>
      <div className="stats__range">
        <span>{dayLabel(daily[0].date)}</span>
        <span>{translate('stats.maxPerDay', { max })}</span>
        <span>{dayLabel(daily[daily.length - 1].date)}</span>
      </div>
    </div>
  )
}

export function ListingStats({ listingId }: { listingId: string }) {
  const t = useT()
  const [days, setDays] = useState(30)
  const [metric, setMetric] = useState<Metric>('views')
  const [data, setData] = useState<ListingStatsResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    setData(null)
    setError(null)
    api
      .get<ListingStatsResponse>(`/listings/${listingId}/stats?days=${days}`, { signal: controller.signal })
      .then(setData)
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        setError(err instanceof ApiError ? err.message : translate('stats.failed'))
      })
    return () => controller.abort()
  }, [listingId, days])

  const current = METRICS.find((m) => m.key === metric) ?? METRICS[0]

  return (
    <div className="stats">
      <div className="stats__periods" role="group" aria-label={t('stats.period')}>
        {PERIODS.map((p) => (
          <button
            key={p}
            type="button"
            className={`stats__tab${p === days ? ' is-active' : ''}`}
            aria-pressed={p === days}
            onClick={() => setDays(p)}
          >
            {t('stats.lastDays', { days: p })}
          </button>
        ))}
      </div>

      {error && <Alert tone="error">{error}</Alert>}
      {!error && data === null && <Loading text={t('stats.loading')} />}

      {data && (
        <>
          <div className="stats__totals">
            {METRICS.map((m) => (
              <button
                key={m.key}
                type="button"
                className={`stats__total${m.key === metric ? ' is-active' : ''}`}
                aria-pressed={m.key === metric}
                onClick={() => setMetric(m.key)}
              >
                <strong>{data.totals[m.key]}</strong>
                <span>{t(m.label)}</span>
              </button>
            ))}
          </div>
          <BarChart daily={data.daily} metric={metric} label={t(current.label)} />
        </>
      )}
    </div>
  )
}
