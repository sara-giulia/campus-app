import { useState, type FormEvent } from 'react'
import { Link, Navigate } from 'react-router'
import { forgotPassword } from '../../api/auth'
import { ApiError } from '../../api/errors'
import { useAuth } from '../../app/auth/AuthContext'
import { Alert, Button, TextField } from '../../app/ui'
import { ResetShell } from './ResetShell'
import { useT } from '../../i18n/LanguageContext'
import { translate } from '../../i18n/store'

// The API always answers 202 with the same body (so the endpoint cannot be used to
// enumerate registered emails). Field validation errors still come back as 400,
// e.g. Email.Empty / Email.Invalid.
function fieldErrorFor(error: ApiError): { email?: string; form?: string } {
  switch (error.code) {
    case 'Email.Empty':
    case 'Email.Invalid':
      return { email: translate('auth.error.EmailInvalid') }
    case 'Network.Unreachable':
      return { form: translate('common.networkUnreachable') }
    case 'Request.TooManyRequests':
      return { form: translate('login.error.TooManyRequests') }
    default:
      return { form: error.message }
  }
}

export function ForgotPasswordPage() {
  const { user } = useAuth()
  const t = useT()

  const [email, setEmail] = useState('')
  const [emailError, setEmailError] = useState<string | undefined>(undefined)
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [sent, setSent] = useState(false)

  if (user) return <Navigate to="/" replace />

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setEmailError(undefined)
    setFormError(null)
    setSubmitting(true)
    try {
      await forgotPassword(email.trim())
      setSent(true)
    } catch (err) {
      if (err instanceof ApiError) {
        const placed = fieldErrorFor(err)
        setEmailError(placed.email)
        setFormError(placed.form ?? null)
      } else {
        setFormError(translate('common.unexpectedError'))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <ResetShell
      title={t('auth.forgot.title')}
      subtitle={t('auth.forgot.subtitle')}
    >
      {sent ? (
        <Alert tone="success">{t('auth.forgot.sent')}</Alert>
      ) : (
        <form onSubmit={handleSubmit} className="reset__form" noValidate>
          <TextField
            label={t('login.email')}
            type="email"
            autoComplete="email"
            placeholder={t('login.emailPlaceholder')}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={emailError}
            required
            autoFocus
          />
          {formError && <Alert tone="error">{formError}</Alert>}
          <Button type="submit" size="lg" block loading={submitting}>
            {submitting ? t('auth.forgot.sending') : t('auth.forgot.submit')}
          </Button>
        </form>
      )}

      <p className="reset__footer">
        <Link to="/login">{t('auth.backToLogin')}</Link>
      </p>
    </ResetShell>
  )
}
