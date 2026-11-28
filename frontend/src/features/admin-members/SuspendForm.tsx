import { useId, useState, type FormEvent } from 'react'
import { api } from '../../api/client'
import { Alert, Button } from '../../app/ui'
import { useT } from '../../i18n/LanguageContext'
import { formatUntil } from './formatUntil'
import { adminMembersErrorMessage, suspendErrorField, SUSPENSION_MAX_DAYS, SUSPENSION_REASON_MAX } from './errors'
import type { ReportedMemberItem, SuspendMemberRequest } from './types'

const DAY_MS = 24 * 60 * 60 * 1000
const DEFAULT_DAYS = 7

// A Date as the value of <input type="datetime-local">, in the browser's time: "2026-12-04T18:30".
function toLocalInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

// The small form under a member row: the reason (required, max 500) and how long: permanent or until a
// date and time (the Admin's local time, sent as UTC; at most 365 days). The API checks the same rules.
export function SuspendForm({ member, onDone, onCancel }: { member: ReportedMemberItem; onDone: () => void; onCancel: () => void }) {
  const t = useT()
  const reasonId = useId()
  const untilId = useId()
  const [reason, setReason] = useState('')
  const [permanent, setPermanent] = useState(false)
  const [until, setUntil] = useState(() => toLocalInput(new Date(Date.now() + DEFAULT_DAYS * DAY_MS)))
  const [reasonError, setReasonError] = useState<string | null>(null)
  const [untilError, setUntilError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // The limits of the date picker, worked out when the form is drawn.
  const [limits] = useState(() => ({
    min: toLocalInput(new Date()),
    max: toLocalInput(new Date(Date.now() + SUSPENSION_MAX_DAYS * DAY_MS)),
  }))

  // The same checks as the API, so the Admin sees the problem before sending.
  function validate(): { reason: string; until: Date | null } | null {
    let ok = true
    const trimmed = reason.trim()
    if (trimmed === '') {
      setReasonError(t('adminMembers.error.ReasonEmpty'))
      ok = false
    } else if (trimmed.length > SUSPENSION_REASON_MAX) {
      setReasonError(t('adminMembers.error.ReasonTooLong', { max: SUSPENSION_REASON_MAX }))
      ok = false
    } else setReasonError(null)

    let untilDate: Date | null = null
    if (!permanent) {
      untilDate = new Date(until)
      const now = Date.now()
      if (until === '' || Number.isNaN(untilDate.getTime())) {
        setUntilError(t('adminMembers.suspend.untilRequired'))
        ok = false
      } else if (untilDate.getTime() <= now) {
        setUntilError(t('adminMembers.error.UntilInPast'))
        ok = false
      } else if (untilDate.getTime() - now > SUSPENSION_MAX_DAYS * DAY_MS) {
        setUntilError(t('adminMembers.error.TooLong', { days: SUSPENSION_MAX_DAYS }))
        ok = false
      } else setUntilError(null)
    } else setUntilError(null)

    return ok ? { reason: trimmed, until: untilDate } : null
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)
    const valid = validate()
    if (!valid) return

    const question = valid.until
      ? t('adminMembers.confirmSuspendUntil', { name: member.name, date: formatUntil(valid.until) })
      : t('adminMembers.confirmSuspendPermanent', { name: member.name })
    if (!window.confirm(question)) return

    const body: SuspendMemberRequest = { reason: valid.reason, until: valid.until ? valid.until.toISOString() : null }
    setBusy(true)
    try {
      await api.post(`/moderation/members/${member.memberId}/suspend`, body)
      onDone()
    } catch (err) {
      const message = adminMembersErrorMessage(err)
      const field = suspendErrorField(err)
      if (field === 'reason') setReasonError(message)
      else if (field === 'until') setUntilError(message)
      else setFormError(message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="admin-members__suspend" onSubmit={submit} noValidate>
      <h3 className="admin-members__suspend-title">{t('adminMembers.suspend.title', { name: member.name })}</h3>

      <div className={reasonError ? 'field field--error' : 'field'}>
        <label className="field__label" htmlFor={reasonId}>
          {t('adminMembers.suspend.reason')}
        </label>
        <textarea
          id={reasonId}
          className="admin-members__textarea"
          maxLength={SUSPENSION_REASON_MAX}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          aria-invalid={reasonError ? true : undefined}
        />
        <p className="field__hint">{t('adminMembers.suspend.reasonHint', { count: reason.length, max: SUSPENSION_REASON_MAX })}</p>
        {reasonError && <p className="field__error">{reasonError}</p>}
      </div>

      <fieldset className="admin-members__duration">
        <legend className="field__label">{t('adminMembers.suspend.duration')}</legend>
        <label className="admin-members__choice">
          <input type="radio" name={`duration-${member.memberId}`} checked={!permanent} onChange={() => setPermanent(false)} />
          {t('adminMembers.suspend.untilOption')}
        </label>
        <label className="admin-members__choice">
          <input type="radio" name={`duration-${member.memberId}`} checked={permanent} onChange={() => setPermanent(true)} />
          {t('adminMembers.suspend.permanentOption')}
        </label>
      </fieldset>

      {!permanent && (
        <div className={untilError ? 'field field--error' : 'field'}>
          <label className="field__label" htmlFor={untilId}>
            {t('adminMembers.suspend.until')}
          </label>
          <input
            id={untilId}
            className="field__input"
            type="datetime-local"
            min={limits.min}
            max={limits.max}
            value={until}
            onChange={(e) => setUntil(e.target.value)}
            aria-invalid={untilError ? true : undefined}
          />
          <p className="field__hint">{t('adminMembers.suspend.untilHint', { days: SUSPENSION_MAX_DAYS })}</p>
          {untilError && <p className="field__error">{untilError}</p>}
        </div>
      )}

      <p className="field__hint">{t('adminMembers.suspend.effects')}</p>
      {formError && <Alert tone="error">{formError}</Alert>}

      <div className="admin-members__buttons">
        <Button type="submit" variant="danger" size="sm" loading={busy}>
          {t('adminMembers.suspend.submit')}
        </Button>
        <Button variant="ghost" size="sm" onClick={onCancel} disabled={busy}>
          {t('adminMembers.suspend.cancel')}
        </Button>
      </div>
    </form>
  )
}
