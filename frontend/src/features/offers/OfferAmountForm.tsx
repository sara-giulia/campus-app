import { useState, type FormEvent } from 'react'
import { Button, TextField } from '../../app/ui'

interface Props {
  label: string
  submitText: string
  // Called with the amount in lei; resolves when the request is done (the form then closes).
  onSubmit: (amount: number) => Promise<void>
  onCancel: () => void
}

// The small form behind "Fă o ofertă" and "Contraofertă": one amount in lei.
export function OfferAmountForm({ label, submitText, onSubmit, onCancel }: Props) {
  const [value, setValue] = useState('')
  const [sending, setSending] = useState(false)

  const amount = Number(value.replace(',', '.'))
  const valid = value.trim() !== '' && Number.isFinite(amount) && amount > 0

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!valid) return
    setSending(true)
    try {
      await onSubmit(Math.round(amount * 100) / 100)
    } catch {
      // The caller shows the error; the form stays open so the amount can be changed.
    } finally {
      setSending(false)
    }
  }

  return (
    <form className="offer-form" onSubmit={submit}>
      <TextField
        label={label}
        type="text"
        inputMode="decimal"
        placeholder="ex. 50"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        hint="Suma în lei"
        autoFocus
      />
      <div className="offer-form__actions">
        <Button type="submit" size="sm" loading={sending} disabled={!valid}>
          {submitText}
        </Button>
        <Button variant="ghost" size="sm" onClick={onCancel} disabled={sending}>
          Renunță
        </Button>
      </div>
    </form>
  )
}
