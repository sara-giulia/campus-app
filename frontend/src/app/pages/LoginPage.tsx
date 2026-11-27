import { useState, useSyncExternalStore, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router'
import { ApiError } from '../../api/errors'
import { ACCOUNT_SUSPENDED, clearAccountSuspended, isAccountSuspended, onAccountSuspendedChange } from '../../api/suspension'
import { formatDate } from '../../i18n/format'
import { useT } from '../../i18n/LanguageContext'
import { translate } from '../../i18n/store'
import { useAuth } from '../auth/AuthContext'
import { Alert, Button, TextField } from '../ui'
import './login.css'

// Messages for the error codes POST /auth/login can return.
function loginErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return translate('common.unexpectedError')
  switch (error.code) {
    case 'Auth.InvalidCredentials':
      return translate('login.error.InvalidCredentials')
    case ACCOUNT_SUSPENDED:
      return suspendedMessage(error.message)
    case 'Request.TooManyRequests':
      return translate('login.error.TooManyRequests')
    case 'Network.Unreachable':
      return translate('common.networkUnreachable')
    default:
      return error.message
  }
}

// The 403 of login says "...suspended until 2026-12-31 21:59 UTC." or "...suspended permanently.";
// the date is shown in the user's time and language. The reason is only in the email the member got.
function suspendedMessage(apiMessage: string): string {
  const until = /until (\d{4}-\d{2}-\d{2}) (\d{2}:\d{2}) UTC/.exec(apiMessage)
  if (until) {
    const date = new Date(`${until[1]}T${until[2]}:00Z`)
    if (!Number.isNaN(date.getTime()))
      return translate('login.suspendedUntil', { date: formatDate(date, { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' }) })
  }
  if (/permanently/.test(apiMessage)) return translate('login.suspendedPermanent')
  return translate('login.suspended')
}

export function LoginPage() {
  const { user, login } = useAuth()
  const t = useT()
  const navigate = useNavigate()
  const location = useLocation()
  // RequireAuth sends the page the user wanted to open; go back there after login.
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  // Set when the client logged the user out because the account was suspended (see api/suspension.ts).
  const suspended = useSyncExternalStore(onAccountSuspendedChange, isAccountSuspended)

  if (user) return <Navigate to={from} replace />

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await login(email.trim(), password)
      clearAccountSuspended()
      navigate(from, { replace: true })
    } catch (err) {
      setError(loginErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login">
      <div className="login__form-side">
        <h1 className="login__title">{t('login.title')}</h1>
        <p className="login__subtitle">{t('login.subtitle')}</p>
        {suspended && !error && <Alert tone="error">{t('login.suspended')}</Alert>}

        <form onSubmit={handleSubmit} className="login__form">
          <TextField label={t('login.email')} type="email" autoComplete="email" placeholder={t('login.emailPlaceholder')} value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
          <TextField label={t('login.password')} type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          <p className="login__forgot">
            <Link to="/forgot-password">{t('login.forgot')}</Link>
          </p>
          {error && <Alert tone="error">{error}</Alert>}
          <Button type="submit" size="lg" block loading={submitting}>
            {submitting ? t('login.checking') : t('login.submit')}
          </Button>
        </form>
      </div>

      <div className="login__picture" aria-hidden="true">
        <p className="login__quote">
          {t('home.title')}
          <small>{t('layout.footer')}</small>
        </p>
      </div>
    </div>
  )
}
