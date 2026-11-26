import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Button, Card, EmptyState, Loading } from '../../app/ui'
import type { BlockedMember, GetMyBlockedResponse } from './types'
import { useT } from '../../i18n/LanguageContext'

// "Utilizatori blocați" in the profile: the members the user blocked, each with an unblock button.
export function BlockedMembersCard() {
  const t = useT()
  const [items, setItems] = useState<BlockedMember[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetMyBlockedResponse>('/members/me/blocked', { signal: controller.signal })
      .then((r) => setItems(r.items))
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(errorMessage(err))
      })
    return () => controller.abort()
  }, [])

  async function unblock(memberId: string) {
    setBusyId(memberId)
    setError(null)
    try {
      await api.delete(`/members/${memberId}/block`)
      setItems((current) => current?.filter((m) => m.memberId !== memberId) ?? null)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusyId(null)
    }
  }

  return (
    <Card className="profile__card">
      <h2 className="profile__card-title">{t('blocks.title')}</h2>
      {error && <Alert tone="error">{error}</Alert>}
      {items === null ? (
        !error && <Loading text={t('blocks.loading')} />
      ) : items.length === 0 ? (
        <EmptyState title={t('blocks.emptyTitle')}>{t('blocks.emptyText')}</EmptyState>
      ) : (
        <ul className="profile__blocked">
          {items.map((m) => (
            <li key={m.memberId}>
              <Link to={`/members/${m.memberId}`}>{m.name}</Link>
              <Button variant="secondary" size="sm" loading={busyId === m.memberId} onClick={() => unblock(m.memberId)}>
                {t('blocks.unblock')}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}
