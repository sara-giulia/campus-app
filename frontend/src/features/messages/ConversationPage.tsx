import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { api } from '../../api/client'
import { useAuth } from '../../app/auth/AuthContext'
import { errorMessage, isAbort } from '../../app/errorMessage'
import { Alert, Button, Loading } from '../../app/ui'
import { formatDateTime } from '../listings/publicTypes'
import type { ConversationDetail } from './types'
import './messages.css'

// New messages from the other member appear without a refresh: the page asks again every few seconds.
const REFRESH_MS = 5000

export function ConversationPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const [conversation, setConversation] = useState<ConversationDetail | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [text, setText] = useState('')
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<string | null>(null)
  const endRef = useRef<HTMLDivElement>(null)
  const lastCount = useRef(0)

  // Bumped after sending, to load the conversation again at once.
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    const fetchConversation = () =>
      api
        .get<ConversationDetail>(`/conversations/${id}`, { signal: controller.signal })
        .then((result) => {
          setConversation(result)
          setLoadError(null)
        })
        .catch((err: unknown) => {
          if (!isAbort(err)) setLoadError(errorMessage(err))
        })
    fetchConversation()
    const timer = window.setInterval(fetchConversation, REFRESH_MS)
    return () => {
      window.clearInterval(timer)
      controller.abort()
    }
  }, [id, version])

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
        <Link to="/messages">← Toate mesajele</Link>
      </div>
    )
  }
  if (!conversation) return <Loading text="Se încarcă conversația…" />

  const other = user?.id === conversation.sellerId ? conversation.buyerName : conversation.sellerName

  return (
    <div className="chat">
      <div className="chat__head">
        <Link to="/messages" className="chat__back">
          ← Mesaje
        </Link>
        <div>
          <h1 className="chat__title">{other}</h1>
          <Link to={`/listings/${conversation.listingId}`} className="chat__listing">
            {conversation.listingTitle}
          </Link>
        </div>
      </div>

      <div className="chat__messages">
        {conversation.messages.map((m) => (
          <div key={m.id} className={m.senderId === user?.id ? 'bubble bubble--mine' : 'bubble'}>
            <p>{m.text}</p>
            <time>{formatDateTime(m.sentAt)}</time>
          </div>
        ))}
        <div ref={endRef} />
      </div>

      <form className="chat__form" onSubmit={send}>
        <textarea
          className="chat__input"
          placeholder="Scrie un mesaj…"
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
          Trimite
        </Button>
      </form>
      {sendError && <Alert tone="error">{sendError}</Alert>}
    </div>
  )
}
