import { useState, type FormEvent } from 'react'
import { Button, TextField } from '../../app/ui'
import { useT } from '../../i18n/LanguageContext'

interface Props {
  label: string
  submitText: string
  // Called with the amount in lei; resolves when the request is done (the form then closes).
  onSubmit: (amount: number) => Promise<void>
  onCancel: () => void
}

// The small form behind "Fă o ofertă" and "Contraofertă": one amount in lei.
export function OfferAmountForm({ label, submitText, onSubmit, onCancel }: Props) {
  const t = useT()
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
        placeholder={t('offers.form.placeholder')}
        value={value}
        onChange={(e) => setValue(e.target.value)}
        hint={t('offers.form.hint')}
        autoFocus
      />
      <div className="offer-form__actions">
        <Button type="submit" size="sm" loading={sending} disabled={!valid}>
          {submitText}
        </Button>
        <Button variant="ghost" size="sm" onClick={onCancel} disabled={sending}>
          {t('offers.form.cancel')}
        </Button>
      </div>
    </form>
  )
}
