import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { api } from '../../api/client'
import { useAuth } from '../../app/auth/AuthContext'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Button, Loading } from '../../app/ui'
import { BlockButton } from '../blocks/BlockButton'
import { useLiveEvents } from '../live/LiveEventsContext'
import { formatDateTime } from '../listings/publicTypes'
import { OffersPanel } from '../offers/OffersPanel'
import { useUnreadMessages } from './UnreadMessagesContext'
import type { ConversationDetail } from './types'
import './messages.css'
import { useT } from '../../i18n/LanguageContext'

// New messages from the other member arrive on the live stream (features/live) and the page loads
// the conversation again at once. Only while the stream is not connected does it ask every few seconds.
const REFRESH_MS = 5000

export function ConversationPage() {
  const t = useT()
  const { id = '' } = useParams()
  const { user } = useAuth()
  const { refresh: refreshUnread } = useUnreadMessages()
  const [conversation, setConversation] = useState<ConversationDetail | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [text, setText] = useState('')
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<string | null>(null)
  const endRef = useRef<HTMLDivElement>(null)
  const lastCount = useRef(0)

  // Bumped after sending and on a live event for this conversation, to load it again at once.
  const [version, setVersion] = useState(0)

  // A notification may be about an offer in this conversation (new offer, counter-offer, answer):
  // loading it again is cheap, so any notification refreshes the page too.
  const live = useLiveEvents((event) => {
    if ((event.type === 'message' && event.conversationId === id) || event.type === 'notification') setVersion((v) => v + 1)
  })

  useEffect(() => {
    const controller = new AbortController()
    // Returns nothing: errors are handled inside, so the interval and the first call can just fire it.
    const fetchConversation = () => {
      api
        .get<ConversationDetail>(`/conversations/${id}`, { signal: controller.signal })
        .then((result) => {
          setConversation(result)
          setLoadError(null)
          // Open on the screen means read: on the first load and on every new message while it is open.
          if (result.unreadCount > 0) {
            api
              .post(`/conversations/${id}/read`, undefined, { signal: controller.signal })
              .then(refreshUnread)
              .catch(() => {
                // Not marked this time; the next load tries again.
              })
          }
        })
        .catch((err: unknown) => {
          if (!isAbort(err)) setLoadError(errorMessage(err))
        })
    }
    fetchConversation()
    const timer = live ? undefined : window.setInterval(fetchConversation, REFRESH_MS)
    return () => {
      window.clearInterval(timer)
      controller.abort()
    }
  }, [id, version, live, refreshUnread])

  // Scroll to the newest message when one arrives.
  useEffect(() => {
    const count = conversation?.messages.length ?? 0
    if (count !== lastCount.current) {
      lastCount.current = count
      endRef.current?.scrollIntoView({ block: 'end' })
    }
  }, [conversation])

  async function send(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSending(true)
    setSendError(null)
    try {
      await api.post(`/conversations/${id}/messages`, { text })
      setText('')
      setVersion((v) => v + 1)
    } catch (err) {
      setSendError(errorMessage(err))
    } finally {
      setSending(false)
    }
  }

  if (loadError && !conversation) {
    return (
      <div className="chat">
        <Alert tone="error">{loadError}</Alert>
        <Link to="/messages">{t('messages.allMessages')}</Link>
      </div>
    )
  }
  if (!conversation) return <Loading text={t('messages.loadingConversation')} />

  const other = user?.id === conversation.sellerId ? conversation.buyerName : conversation.sellerName
  const otherId = user?.id === conversation.sellerId ? conversation.buyerId : conversation.sellerId

  return (
    <div className="chat">
      <div className="chat__head">
        <Link to="/messages" className="chat__back">
          {t('messages.back')}
        </Link>
        <div>
          <h1 className="chat__title">{other}</h1>
          <Link to={`/listings/${conversation.listingId}`} className="chat__listing">
            {conversation.listingTitle}
          </Link>
        </div>
        <div className="chat__actions">
          <BlockButton memberId={otherId} name={other} />
        </div>
      </div>

      <div className="chat__messages">
        {conversation.messages.map((m) => (
          <div key={m.id} className={m.senderId === user?.id ? 'bubble bubble--mine' : 'bubble'}>
            {m.isSuspicious && (
              <p className="bubble__warning" role="note">
                {m.senderId === user?.id ? t('messages.suspiciousMine') : t('messages.suspicious')}
              </p>
            )}
            <p>{m.text}</p>
            <time>{formatDateTime(m.sentAt)}</time>
          </div>
        ))}
        <div ref={endRef} />
      </div>

      <OffersPanel
        listingId={conversation.listingId}
        buyerId={conversation.buyerId}
        currentUserId={user?.id}
        otherName={other}
        offers={conversation.offers}
        onChanged={() => setVersion((v) => v + 1)}
      />

      <form className="chat__form" onSubmit={send}>
        <textarea
          className="chat__input"
          placeholder={t('messages.placeholder')}
          maxLength={1000}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault()
              e.currentTarget.form?.requestSubmit()
            }
          }}
        />
        <Button type="submit" loading={sending} disabled={text.trim() === ''}>
          {t('messages.send')}
        </Button>
      </form>
      {sendError && <Alert tone="error">{sendError}</Alert>}
    </div>
  )
}
