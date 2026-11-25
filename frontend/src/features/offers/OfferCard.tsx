import { useState } from 'react'
import { Badge, Button } from '../../app/ui'
import { formatDateTime } from '../listings/publicTypes'
import { OfferAmountForm } from './OfferAmountForm'
import { OFFER_STATUS_LABELS, type Offer, type OfferStatus } from './types'

function statusTone(status: OfferStatus): 'neutral' | 'success' | 'warning' | 'danger' | 'accent' {
  switch (status) {
    case 'Pending':
      return 'warning'
    case 'Accepted':
      return 'success'
    case 'Rejected':
      return 'danger'
    case 'Countered':
      return 'accent'
    default:
      return 'neutral'
  }
}

interface Props {
  offer: Offer
  currentUserId: string | undefined
  // The name shown for the other member ("Ana a oferit…").
  otherName: string
  // The action that is running on this card, if any ('accept' | 'reject' | 'withdraw' | 'counter').
  busy: string | null
  onAccept: () => void
  onReject: () => void
  onWithdraw: () => void
  onCounter: (amount: number) => Promise<void>
}

// One offer in the conversation. The member who has to answer a Pending offer gets "Acceptă",
// "Refuză" and "Contraofertă"; the buyer gets "Retrage" on their own Pending offer.
export function OfferCard({ offer, currentUserId, otherName, busy, onAccept, onReject, onWithdraw, onCounter }: Props) {
  const [countering, setCountering] = useState(false)

  const mine = offer.proposedById === currentUserId
  const pending = offer.status === 'Pending'
  const canAnswer = pending && !mine && (currentUserId === offer.buyerId || currentUserId === offer.sellerId)
  const canWithdraw = pending && mine && currentUserId === offer.buyerId
  const who = mine ? 'Ai oferit' : `${otherName} a oferit`
  const kind = offer.parentOfferId ? 'Contraofertă' : 'Ofertă'

  return (
    <div className={mine ? 'offer-card offer-card--mine' : 'offer-card'}>
      <div className="offer-card__head">
        <span className="offer-card__kind">{kind}</span>
        <Badge tone={statusTone(offer.status)}>{OFFER_STATUS_LABELS[offer.status]}</Badge>
      </div>
      <p className="offer-card__amount">
        {who} <strong>{offer.amount} RON</strong>
      </p>
      <time>{formatDateTime(offer.createdAt)}</time>

      {canAnswer && !countering && (
        <div className="offer-card__actions">
          <Button size="sm" loading={busy === 'accept'} disabled={busy !== null} onClick={onAccept}>
            Acceptă
          </Button>
          <Button size="sm" variant="danger" loading={busy === 'reject'} disabled={busy !== null} onClick={onReject}>
            Refuză
          </Button>
          <Button size="sm" variant="secondary" disabled={busy !== null} onClick={() => setCountering(true)}>
            Contraofertă
          </Button>
        </div>
      )}
      {canAnswer && countering && (
        <OfferAmountForm
          label="Suma propusă de tine"
          submitText="Trimite contraoferta"
          onSubmit={async (amount) => {
            await onCounter(amount)
            setCountering(false)
          }}
          onCancel={() => setCountering(false)}
        />
      )}
      {canWithdraw && (
        <div className="offer-card__actions">
          <Button size="sm" variant="ghost" loading={busy === 'withdraw'} disabled={busy !== null} onClick={onWithdraw}>
            Retrage
          </Button>
        </div>
      )}
    </div>
  )
}
