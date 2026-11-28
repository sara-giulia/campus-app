import { useId, useState, type FormEvent } from 'react'
import { api } from '../../api/client'
import { useAuth } from '../../app/auth/AuthContext'
import { Alert, Button } from '../../app/ui'
import { useT } from '../../i18n/LanguageContext'
import { REPORT_REASON_MAX, reportMemberErrorMessage } from './errors'
import './member-reports.css'

interface ReportMemberResponse {
  id: string
  reportedMemberId: string
  createdAt: string
}

// "Raportează utilizatorul" on a member's public profile and in a conversation with them: a short form
// for the reason, then POST /members/{id}/reports. From a conversation it also sends conversationId, so the
// Admin knows where it happened. Nothing is shown to a visitor who is not logged in, or on the member's own page.
export function ReportMemberButton({ memberId, name, conversationId }: { memberId: string; name: string; conversationId?: string }) {
  const { user } = useAuth()
  const t = useT()
  const fieldId = useId()
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [sent, setSent] = useState(false)

  if (!user || user.id === memberId) return null

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmed = reason.trim()
    if (trimmed === '') {
      setError(t('memberReports.error.ReasonEmpty'))
      return
    }
    setBusy(true)
    setError(null)
    try {
      await api.post<ReportMemberResponse>(`/members/${memberId}/reports`, { reason: trimmed, conversationId: conversationId ?? null })
      setOpen(false)
      setReason('')
      setSent(true)
    } catch (err) {
      setError(reportMemberErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  function cancel() {
    setOpen(false)
    setError(null)
  }

  if (sent) return <Alert tone="success">{t('memberReports.thanks')}</Alert>

  if (!open) {
    return (
      <button type="button" className="member-report__link" onClick={() => setOpen(true)}>
        {t('memberReports.open')}
      </button>
    )
  }

  return (
    <form className="member-report__form" onSubmit={submit}>
      <label className="field__label" htmlFor={fieldId}>
        {t('memberReports.why', { name })}
      </label>
      <textarea
        id={fieldId}
        className="member-report__textarea"
        maxLength={REPORT_REASON_MAX}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
      />
      <p className="field__hint">
        {t('memberReports.hint', { count: reason.length, max: REPORT_REASON_MAX })}
      </p>
      {error && <Alert tone="error">{error}</Alert>}
      <div className="member-report__buttons">
        <Button type="submit" variant="danger" size="sm" loading={busy} disabled={reason.trim() === ''}>
          {t('memberReports.send')}
        </Button>
        <Button variant="ghost" size="sm" onClick={cancel}>
          {t('memberReports.cancel')}
        </Button>
      </div>
    </form>
  )
}
