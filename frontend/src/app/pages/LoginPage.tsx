import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router'
import { ApiError } from '../../api/errors'
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
    case 'Request.TooManyRequests':
      return translate('login.error.TooManyRequests')
    case 'Network.Unreachable':
      return translate('common.networkUnreachable')
    default:
      return error.message
  }
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

  if (user) return <Navigate to={from} replace />

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await login(email.trim(), password)
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
