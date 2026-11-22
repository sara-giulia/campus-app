import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { useAuth } from '../../app/auth/AuthContext'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, ButtonLink, EmptyState, Loading, PageHeader } from '../../app/ui'
import { formatDateTime } from '../listings/publicTypes'
import type { GetMyConversationsResponse } from './types'
import './messages.css'

// My conversations, as buyer and as seller, the one with the latest message first.
export function MessagesPage() {
  const { user } = useAuth()
  const [data, setData] = useState<GetMyConversationsResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<GetMyConversationsResponse>('/members/me/conversations', { signal: controller.signal })
      .then(setData)
      .catch((err: unknown) => {
        if (!isAbort(err)) setError(errorMessage(err))
      })
    return () => controller.abort()
  }, [])

  return (
    <div className="messages">
      <PageHeader eyebrow="Contul meu" title="Mesaje" lead="Conversațiile tale despre anunțuri, ca vânzător și ca cumpărător." />
      {error && <Alert tone="error">{error}</Alert>}
      {!error && data === null && <Loading text="Se încarcă mesajele…" />}
      {data !== null && data.count === 0 && (
        <EmptyState title="Nu ai încă nicio conversație." action={<ButtonLink to="/listings">Caută anunțuri</ButtonLink>}>
          Scrie-i unui vânzător de pe pagina unui anunț.
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
                    {c.lastMessageSenderId === user?.id ? 'Tu: ' : ''}
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
