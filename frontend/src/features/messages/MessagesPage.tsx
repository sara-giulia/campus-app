import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { useAuth } from '../../app/auth/AuthContext'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, ButtonLink, EmptyState, Loading, PageHeader } from '../../app/ui'
import { useLiveEvents } from '../live/LiveEventsContext'
import { formatDateTime } from '../listings/publicTypes'
import type { GetMyConversationsResponse } from './types'
import './messages.css'
import { useT } from '../../i18n/LanguageContext'

// My conversations, as buyer and as seller, the one with the latest message first.
export function MessagesPage() {
  const t = useT()
  const { user } = useAuth()
  const [data, setData] = useState<GetMyConversationsResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  // Bumped on a live message event, so a new conversation or a new last message shows up at once.
  const [version, setVersion] = useState(0)

  useLiveEvents((event) => {
    if (event.type === 'message') setVersion((v) => v + 1)
  })

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetMyConversationsResponse>('/members/me/conversations', { signal: controller.signal })
      .then(setData)
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(errorMessage(err))
      })
    return () => controller.abort()
  }, [version])

  return (
    <div className="messages">
      <PageHeader eyebrow={t('myListings.eyebrow')} title={t('messages.title')} lead={t('messages.lead')} />
      {error && <Alert tone="error">{error}</Alert>}
      {!error && data === null && <Loading text={t('messages.loading')} />}
      {data !== null && data.count === 0 && (
        <EmptyState title={t('messages.emptyTitle')} action={<ButtonLink to="/listings">{t('messages.emptyAction')}</ButtonLink>}>
          {t('messages.emptyText')}
        </EmptyState>
      )}
      {data !== null && data.count > 0 && (
        <ul className="conv-list">
          {data.items.map((c) => (
            <li key={c.id}>
              <Link to={`/messages/${c.id}`} className="conv-item">
                <span className="conv-item__avatar">{c.otherMemberName.slice(0, 1).toUpperCase()}</span>
                <span className="conv-item__body">
                  <span className="conv-item__top">
                    <strong>{c.otherMemberName}</strong>
                    <time>{formatDateTime(c.lastMessageAt)}</time>
                  </span>
                  <span className="conv-item__listing">{c.listingTitle}</span>
                  <span className="conv-item__last">
                    {c.lastMessageSenderId === user?.id ? t('messages.you') : ''}
                    {c.lastMessage}
                  </span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
