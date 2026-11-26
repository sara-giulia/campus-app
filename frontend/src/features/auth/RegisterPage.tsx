import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router'
import { register, type RegisterResponse } from '../../api/auth'
import { ApiError } from '../../api/errors'
import { useAuth } from '../../app/auth/AuthContext'
import { Alert, Button, ButtonLink, TextField } from '../../app/ui'
import './register.css'
import { useT } from '../../i18n/LanguageContext'
import { translate } from '../../i18n/store'

interface FieldErrors {
  name?: string
  email?: string
  password?: string
  confirmPassword?: string
}

interface RegisterWithTokens extends RegisterResponse {
  token?: string
  refreshToken?: string
  expiresAt?: string
}

// Field-level API errors. Anything not placed on a field becomes a top-of-form alert.
function placeError(error: ApiError): { fields: FieldErrors; form: string | null } {
  switch (error.code) {
    case 'Auth.WeakPassword':
      return { fields: { password: translate('auth.error.WeakPassword') }, form: null }
    case 'Auth.PasswordsDoNotMatch':
      return { fields: { confirmPassword: translate('auth.error.PasswordsDoNotMatch') }, form: null }
    case 'Member.EmailTaken':
      return { fields: { email: translate('auth.error.EmailTaken') }, form: null }
    case 'Email.Empty':
    case 'Email.Invalid':
      return { fields: { email: translate('auth.error.EmailInvalid') }, form: null }
    case 'Member.InvalidName':
      return { fields: { name: translate('auth.error.InvalidName') }, form: null }
    case 'Member.PasswordRequired':
      return { fields: { password: translate('auth.error.PasswordRequired') }, form: null }
    case 'Network.Unreachable':
      return { fields: {}, form: translate('common.networkUnreachable') }
    case 'Request.TooManyRequests':
      return { fields: {}, form: translate('login.error.TooManyRequests') }
    default:
      return { fields: {}, form: error.message }
  }
}

export function RegisterPage() {
  const { user } = useAuth()
  const t = useT()
  const navigate = useNavigate()

  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [registeredEmail, setRegisteredEmail] = useState<string | null>(null)

  if (user) return <Navigate to="/" replace />

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFieldErrors({})
    setFormError(null)
    setSubmitting(true)
    try {
      const response = (await register({ name: name.trim(), email: email.trim(), password, confirmPassword })) as RegisterWithTokens
      // If the API answer carries tokens, log in automatically; otherwise show a success message
      // and let the user go to /login.
      if (response.token && response.refreshToken && response.expiresAt) {
        navigate('/', { replace: true })
      } else {
        setRegisteredEmail(response.email)
      }
    } catch (err) {
      if (err instanceof ApiError) {
        const placed = placeError(err)
        setFieldErrors(placed.fields)
        setFormError(placed.form)
      } else {
        setFormError(translate('common.unexpectedError'))
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (registeredEmail) {
    return (
      <div className="register">
        <div className="register__form-side">
          <h1 className="register__title">{t('auth.register.createdTitle')}</h1>
          <p className="register__subtitle">
            {t('auth.register.welcomeBefore')}<strong>{registeredEmail}</strong>{t('auth.register.welcomeAfter')}
          </p>
          <Alert tone="success">{t('auth.register.createdAlert')}</Alert>
          <div className="register__success-actions">
            <ButtonLink to="/login" size="lg" block>
              {t('layout.login')}
            </ButtonLink>
          </div>
        </div>
        <div className="register__picture" aria-hidden="true">
          <p className="register__quote">
            {t('auth.register.quote')}
            <small>{t('layout.footer')}</small>
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="register">
      <div className="register__form-side">
        <h1 className="register__title">{t('auth.register.title')}</h1>
        <p className="register__subtitle">{t('auth.register.subtitle')}</p>

        <form onSubmit={handleSubmit} className="register__form" noValidate>
          <TextField
            label={t('auth.register.name')}
            autoComplete="name"
            placeholder={t('auth.register.namePlaceholder')}
            value={name}
            onChange={(e) => setName(e.target.value)}
            error={fieldErrors.name}
            required
            autoFocus
          />
          <TextField
            label={t('login.email')}
            type="email"
            autoComplete="email"
            placeholder={t('login.emailPlaceholder')}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={fieldErrors.email}
            required
          />
          <TextField
            label={t('login.password')}
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={fieldErrors.password}
            hint={t('auth.passwordHint')}
            required
          />
          <TextField
            label={t('auth.confirmPassword')}
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            error={fieldErrors.confirmPassword}
            required
          />
          {formError && <Alert tone="error">{formError}</Alert>}
          <Button type="submit" size="lg" block loading={submitting}>
            {submitting ? t('auth.register.creating') : t('auth.register.submit')}
          </Button>
        </form>
      </div>

      <div className="register__picture" aria-hidden="true">
        <p className="register__quote">
          {t('auth.register.quote')}
          <small>{t('layout.footer')}</small>
        </p>
      </div>
    </div>
  )
}
