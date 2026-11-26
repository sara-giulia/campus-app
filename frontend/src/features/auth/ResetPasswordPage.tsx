import { useState, type FormEvent } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router'
import { resetPassword } from '../../api/auth'
import { ApiError } from '../../api/errors'
import { useAuth } from '../../app/auth/AuthContext'
import { Alert, Button, ButtonLink, TextField } from '../../app/ui'
import { ResetShell } from './ResetShell'
import { useT } from '../../i18n/LanguageContext'
import { translate } from '../../i18n/store'

interface FieldErrors {
  password?: string
  confirmPassword?: string
}

interface PlacedError {
  fields: FieldErrors
  form: string | null
  tokenInvalid: boolean
}

// Field-level API errors. Anything not placed on a field becomes a top-of-form alert.
function placeError(error: ApiError): PlacedError {
  switch (error.code) {
    case 'Auth.WeakPassword':
      return { fields: { password: translate('auth.error.WeakPassword') }, form: null, tokenInvalid: false }
    case 'Auth.PasswordsDoNotMatch':
      return { fields: { confirmPassword: translate('auth.error.PasswordsDoNotMatch') }, form: null, tokenInvalid: false }
    case 'Member.PasswordRequired':
      return { fields: { password: translate('auth.error.PasswordRequired') }, form: null, tokenInvalid: false }
    case 'Auth.InvalidResetToken':
      return { fields: {}, form: null, tokenInvalid: true }
    case 'Network.Unreachable':
      return { fields: {}, form: translate('common.networkUnreachable'), tokenInvalid: false }
    case 'Request.TooManyRequests':
      return { fields: {}, form: translate('login.error.TooManyRequests'), tokenInvalid: false }
    default:
      return { fields: {}, form: error.message, tokenInvalid: false }
  }
}

export function ResetPasswordPage() {
  const { user } = useAuth()
  const t = useT()
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''

  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [tokenInvalid, setTokenInvalid] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [done, setDone] = useState(false)

  if (user) return <Navigate to="/" replace />

  if (!token) {
    return (
      <ResetShell
        title={t('auth.reset.incompleteTitle')}
        subtitle={t('auth.reset.incompleteSubtitle')}
      >
        <Alert tone="error">{t('auth.reset.incompleteAlert')}</Alert>
        <div className="reset__success-actions">
          <ButtonLink to="/forgot-password" size="lg" block>
            {t('auth.reset.askNew')}
          </ButtonLink>
        </div>
      </ResetShell>
    )
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFieldErrors({})
    setFormError(null)
    setTokenInvalid(false)
    setSubmitting(true)
    try {
      await resetPassword({ token, newPassword: password, confirmNewPassword: confirmPassword })
      setDone(true)
    } catch (err) {
      if (err instanceof ApiError) {
        const placed = placeError(err)
        setFieldErrors(placed.fields)
        setFormError(placed.form)
        setTokenInvalid(placed.tokenInvalid)
      } else {
        setFormError(translate('common.unexpectedError'))
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (done) {
    return (
      <ResetShell title={t('auth.reset.doneTitle')} subtitle={t('auth.reset.doneSubtitle')}>
        <Alert tone="success">{t('auth.reset.doneAlert')}</Alert>
        <div className="reset__success-actions">
          <ButtonLink to="/login" size="lg" block>
            {t('layout.login')}
          </ButtonLink>
        </div>
      </ResetShell>
    )
  }

  if (tokenInvalid) {
    return (
      <ResetShell
        title={t('auth.reset.expiredTitle')}
        subtitle={t('auth.reset.expiredSubtitle')}
      >
        <Alert tone="error">{t('auth.reset.expiredAlert')}</Alert>
        <div className="reset__success-actions">
          <ButtonLink to="/forgot-password" size="lg" block>
            {t('auth.reset.askNew')}
          </ButtonLink>
        </div>
      </ResetShell>
    )
  }

  return (
    <ResetShell title={t('auth.reset.title')} subtitle={t('auth.reset.subtitle')}>
      <form onSubmit={handleSubmit} className="reset__form" noValidate>
        <TextField
          label={t('auth.reset.newPassword')}
          type="password"
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={fieldErrors.password}
          hint={t('auth.passwordHint')}
          required
          autoFocus
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
          {submitting ? t('auth.reset.saving') : t('auth.reset.submit')}
        </Button>
      </form>

      <p className="reset__footer">
        <Link to="/login">{t('auth.backToLogin')}</Link>
      </p>
    </ResetShell>
  )
}
