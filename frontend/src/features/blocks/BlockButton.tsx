import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import { useAuth } from '../../app/auth/AuthContext'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Button } from '../../app/ui'
import type { GetMyBlockedResponse } from './types'
import { useT } from '../../i18n/LanguageContext'

// "Blochează" / "Deblochează" for another member, on their public profile and in a conversation with them.
// It asks the blocked list once to know the state, then POST / DELETE /members/{id}/block (both idempotent).
// Nothing is shown to a visitor who is not logged in, or on the member's own page.
export function BlockButton({ memberId, name }: { memberId: string; name: string }) {
  const { user } = useAuth()
  const t = useT()
  const [blocked, setBlocked] = useState<boolean | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const userId = user?.id
  useEffect(() => {
    if (!userId || userId === memberId) return
    const controller = new AbortController()
    api
      .get<GetMyBlockedResponse>('/members/me/blocked', { signal: controller.signal })
      .then((r) => setBlocked(r.items.some((b) => b.memberId === memberId)))
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(errorMessage(err))
      })
    return () => controller.abort()
  }, [userId, memberId])

  if (!userId || userId === memberId) return null

  async function toggle() {
    if (blocked === null) return
    if (!blocked && !window.confirm(t('blocks.confirm', { name }))) return
    setBusy(true)
    setError(null)
    try {
      if (blocked) await api.delete(`/members/${memberId}/block`)
      else await api.post(`/members/${memberId}/block`)
      setBlocked(!blocked)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <Button variant={blocked ? 'secondary' : 'danger'} size="sm" loading={busy} disabled={blocked === null} onClick={toggle}>
        {blocked ? t('blocks.unblock') : t('blocks.block')}
      </Button>
      {error && <Alert tone="error">{error}</Alert>}
    </>
  )
}
