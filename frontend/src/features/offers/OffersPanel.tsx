import { useState } from 'react'
import { api } from '../../api/client'
import { Alert, Button } from '../../app/ui'
import { formatRon } from '../../i18n/format'
import { OfferAmountForm } from './OfferAmountForm'
import { OfferCard } from './OfferCard'
import { offerErrorMessage } from './errors'
import type { Offer } from './types'
import './offers.css'
import { useT } from '../../i18n/LanguageContext'

interface Props {
  listingId: string
  buyerId: string
  currentUserId: string | undefined
  otherName: string
  offers: Offer[]
  // Called after every successful action, so the conversation (with its offers) is loaded again.
  onChanged: () => void
}

// The price negotiation inside a conversation: the offers, the oldest first, and "Fă o ofertă" for
// the buyer while no offer is waiting for an answer.
export function OffersPanel({ listingId, buyerId, currentUserId, otherName, offers, onChanged }: Props) {
  const t = useT()
  const [making, setMaking] = useState(false)
  const [busy, setBusy] = useState<{ offerId: string; action: string } | null>(null)
  const [error, setError] = useState<string | null>(null)

  const isBuyer = currentUserId === buyerId
  const hasPending = offers.some((o) => o.status === 'Pending')
  const accepted = offers.find((o) => o.status === 'Accepted')

  async function run(offerId: string, action: string, call: () => Promise<unknown>) {
    setBusy({ offerId, action })
    setError(null)
    try {
      await call()
      onChanged()
    } catch (err) {
      setError(offerErrorMessage(err))
      throw err
    } finally {
      setBusy(null)
    }
  }

  // Errors are shown in the panel; the rejected promise only keeps a form open.
  const quiet = (p: Promise<unknown>) => p.catch(() => undefined)

  async function makeOffer(amount: number) {
    setError(null)
    try {
      await api.post(`/listings/${listingId}/offers`, { amount })
      setMaking(false)
      onChanged()
    } catch (err) {
      setError(offerErrorMessage(err))
    }
  }

  if (offers.length === 0 && !isBuyer) return null

  return (
    <section className="offers" aria-label={t('offers.panel.label')}>
      {offers.map((offer) => (
        <OfferCard
          key={offer.id}
          offer={offer}
          currentUserId={currentUserId}
          otherName={otherName}
          busy={busy?.offerId === offer.id ? busy.action : null}
          onAccept={() => void quiet(run(offer.id, 'accept', () => api.post(`/offers/${offer.id}/accept`)))}
          onReject={() => void quiet(run(offer.id, 'reject', () => api.post(`/offers/${offer.id}/reject`)))}
          onWithdraw={() => void quiet(run(offer.id, 'withdraw', () => api.post(`/offers/${offer.id}/withdraw`)))}
          onCounter={(amount) => run(offer.id, 'counter', () => api.post(`/offers/${offer.id}/counter`, { amount }))}
        />
      ))}

      {accepted && (
        <Alert tone="success">
          {t('offers.panel.accepted', { amount: formatRon(accepted.amount) })}
        </Alert>
      )}

      {isBuyer && !hasPending && !accepted && !making && (
        <div>
          <Button variant="secondary" size="sm" onClick={() => setMaking(true)}>
            {t('offers.panel.make')}
          </Button>
        </div>
      )}
      {isBuyer && making && (
        <OfferAmountForm
          label={t('offers.panel.howMuch')}
          submitText={t('offers.panel.submit')}
          onSubmit={makeOffer}
          onCancel={() => setMaking(false)}
        />
      )}

      {error && <Alert tone="error">{error}</Alert>}
    </section>
  )
}
