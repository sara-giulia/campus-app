import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { isAbort } from '../../app/errorMessage'
import { Alert, Card, EmptyState, Loading, PageHeader } from '../../app/ui'
import { useCategories } from '../listings/categories'
import { formatDateTime, formatPrice } from '../listings/publicTypes'
import { CONDITION_LABELS, type Category, type Condition } from '../listings/types'
import { priceAccuracyError } from './errors'
import '../listings/listings.css'
import './admin-ml.css'

// GET /api/v1/admin/ml/price-accuracy (Features/Admin/PriceAccuracy/PriceAccuracyResponse.cs).
// count 0 → mae and mape are null, perCategory and lastRecords are empty.
interface PriceAccuracyPerCategory {
  category: Category
  count: number
  mae: number
}

interface PriceAccuracyRecord {
  listingId: string
  category: Category
  condition: Condition
  suggestedPrice: number
  soldPrice: number
  soldAt: string
}

interface PriceAccuracyResponse {
  count: number
  mae: number | null
  mape: number | null
  perCategory: PriceAccuracyPerCategory[]
  lastRecords: PriceAccuracyRecord[]
}

// Admin: how close the price model's suggestions were to the prices the listings really sold for.
export function PriceAccuracyPage() {
  const [data, setData] = useState<PriceAccuracyResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { labelOf } = useCategories()

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<PriceAccuracyResponse>('/admin/ml/price-accuracy', { signal: controller.signal })
      .then((result) => {
        setData(result)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(priceAccuracyError(err))
      })
    return () => controller.abort()
  }, [])

  return (
    <div className="listings-page admin-ml">
      <PageHeader
        eyebrow="Administrare"
        title="Acuratețea prețurilor"
        lead="Cât de aproape a fost prețul sugerat de model de prețul cu care s-a vândut anunțul, la vânzările confirmate."
      />
      {error && <Alert tone="error">{error}</Alert>}
      {!error && data === null && <Loading text="Se încarcă statisticile…" />}
      {data?.count === 0 && (
        <EmptyState title="Nu există încă vânzări confirmate.">
          Când un vânzător confirmă o vânzare, ea apare aici, împreună cu prețul sugerat.
        </EmptyState>
      )}
      {data !== null && data.count > 0 && (
        <>
          <div className="admin-ml__stats">
            <Stat label="Vânzări confirmate" value={String(data.count)} />
            <Stat label="Eroare medie (MAE)" value={data.mae === null ? '–' : formatPrice(data.mae)} />
            <Stat label="Eroare medie procentuală (MAPE)" value={data.mape === null ? '–' : formatPercent(data.mape)} />
          </div>

          {data.perCategory.length > 0 && (
            <Card className="listings-card">
              <h2 className="admin-ml__section-title">Pe categorii</h2>
              <div className="admin-ml__table-wrap">
                <table className="admin-ml__table">
                  <thead>
                    <tr>
                      <th scope="col">Categorie</th>
                      <th scope="col" className="admin-ml__num">Vânzări</th>
                      <th scope="col" className="admin-ml__num">Eroare medie</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.perCategory.map((row) => (
                      <tr key={row.category}>
                        <td>{labelOf(row.category)}</td>
                        <td className="admin-ml__num">{row.count}</td>
                        <td className="admin-ml__num">{formatPrice(row.mae)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
          )}

          <Card className="listings-card">
            <h2 className="admin-ml__section-title">Ultimele vânzări</h2>
            <div className="admin-ml__table-wrap">
              <table className="admin-ml__table">
                <thead>
                  <tr>
                    <th scope="col">Data</th>
                    <th scope="col">Categorie</th>
                    <th scope="col">Stare</th>
                    <th scope="col" className="admin-ml__num">Preț sugerat</th>
                    <th scope="col" className="admin-ml__num">Preț vândut</th>
                    <th scope="col" className="admin-ml__num">Diferență</th>
                    <th scope="col">Anunț</th>
                  </tr>
                </thead>
                <tbody>
                  {data.lastRecords.map((record) => (
                    <tr key={`${record.listingId}-${record.soldAt}`}>
                      <td>{formatDateTime(record.soldAt)}</td>
                      <td>{labelOf(record.category)}</td>
                      <td>{CONDITION_LABELS[record.condition]}</td>
                      <td className="admin-ml__num">{formatPrice(record.suggestedPrice)}</td>
                      <td className="admin-ml__num">{formatPrice(record.soldPrice)}</td>
                      <td className="admin-ml__num">
                        <Difference suggested={record.suggestedPrice} sold={record.soldPrice} />
                      </td>
                      <td>
                        <Link to={`/listings/${record.listingId}`}>Vezi</Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </>
      )}
    </div>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <Card className="admin-ml__stat">
      <span className="admin-ml__stat-label">{label}</span>
      <span className="admin-ml__stat-value">{value}</span>
    </Card>
  )
}

// Positive: the model suggested more than the item sold for; negative: less.
function Difference({ suggested, sold }: { suggested: number; sold: number }) {
  const diff = Math.round((suggested - sold) * 100) / 100
  if (diff === 0) return <span>0 lei</span>
  const className = diff > 0 ? 'admin-ml__over' : 'admin-ml__under'
  const sign = diff > 0 ? '+' : '−'
  return (
    <span className={className} title={diff > 0 ? 'Sugerat peste prețul de vânzare' : 'Sugerat sub prețul de vânzare'}>
      {sign}
      {formatPrice(Math.abs(diff))}
    </span>
  )
}

function formatPercent(value: number): string {
  return `${value.toLocaleString('ro-RO', { maximumFractionDigits: 2 })}%`
}
