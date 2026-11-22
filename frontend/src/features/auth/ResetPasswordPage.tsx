import { useState, type FormEvent } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router'
import { resetPassword } from '../../api/auth'
import { ApiError } from '../../api/errors'
import { useAuth } from '../../app/auth/AuthContext'
import { Alert, Button, ButtonLink, TextField } from '../../app/ui'
import { ResetShell } from './ResetShell'

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
      return { fields: { password: 'Parola trebuie să aibă cel puțin 8 caractere.' }, form: null, tokenInvalid: false }
    case 'Auth.PasswordsDoNotMatch':
      return { fields: { confirmPassword: 'Cele două parole nu coincid.' }, form: null, tokenInvalid: false }
    case 'Member.PasswordRequired':
      return { fields: { password: 'Parola este obligatorie.' }, form: null, tokenInvalid: false }
    case 'Auth.InvalidResetToken':
      return { fields: {}, form: null, tokenInvalid: true }
    case 'Network.Unreachable':
      return { fields: {}, form: 'Serverul nu răspunde. Verifică dacă API-ul este pornit.', tokenInvalid: false }
    case 'Request.TooManyRequests':
      return { fields: {}, form: 'Prea multe încercări. Încearcă din nou peste un minut.', tokenInvalid: false }
    default:
      return { fields: {}, form: error.message, tokenInvalid: false }
  }
}

export function ResetPasswordPage() {
  const { user } = useAuth()
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
        title="Link incomplet"
        subtitle="Linkul pe care l-ai deschis nu conține un cod de resetare. Cere unul nou de pe pagina „Ai uitat parola”."
      >
        <Alert tone="error">Codul de resetare lipsește din link.</Alert>
        <div className="reset__success-actions">
          <ButtonLink to="/forgot-password" size="lg" block>
            Cere un link nou
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
        setFormError('A apărut o eroare neașteptată.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (done) {
    return (
      <ResetShell title="Parolă schimbată" subtitle="Poți intra în cont cu noua parolă.">
        <Alert tone="success">Parola a fost schimbată cu succes.</Alert>
        <div className="reset__success-actions">
          <ButtonLink to="/login" size="lg" block>
            Intră în cont
          </ButtonLink>
        </div>
      </ResetShell>
    )
  }

  if (tokenInvalid) {
    return (
      <ResetShell
        title="Link expirat sau folosit"
        subtitle="Linkul de resetare nu mai este valid. Linkurile sunt valabile 30 de minute și pot fi folosite o singură dată."
      >
        <Alert tone="error">Linkul de resetare nu este valid. Cere unul nou.</Alert>
        <div className="reset__success-actions">
          <ButtonLink to="/forgot-password" size="lg" block>
            Cere un link nou
          </ButtonLink>
        </div>
      </ResetShell>
    )
  }

  return (
    <ResetShell title="Setează o parolă nouă" subtitle="Alege o parolă nouă pentru contul tău CampusMarket.">
      <form onSubmit={handleSubmit} className="reset__form" noValidate>
        <TextField
          label="Parolă nouă"
          type="password"
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={fieldErrors.password}
          hint="Minim 8 caractere."
          required
          autoFocus
        />
        <TextField
          label="Confirmă parola"
          type="password"
          autoComplete="new-password"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          error={fieldErrors.confirmPassword}
          required
        />
        {formError && <Alert tone="error">{formError}</Alert>}
        <Button type="submit" size="lg" block loading={submitting}>
          {submitting ? 'Se salvează…' : 'Salvează parola nouă'}
        </Button>
      </form>

      <p className="reset__footer">
        <Link to="/login">Înapoi la autentificare</Link>
      </p>
    </ResetShell>
  )
}
