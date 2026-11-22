import { useState, type FormEvent } from 'react'
import { Link, Navigate } from 'react-router'
import { forgotPassword } from '../../api/auth'
import { ApiError } from '../../api/errors'
import { useAuth } from '../../app/auth/AuthContext'
import { Alert, Button, TextField } from '../../app/ui'
import { ResetShell } from './ResetShell'

// The API always answers 202 with the same body (so the endpoint cannot be used to
// enumerate registered emails). Field validation errors still come back as 400,
// e.g. Email.Empty / Email.Invalid.
function fieldErrorFor(error: ApiError): { email?: string; form?: string } {
  switch (error.code) {
    case 'Email.Empty':
    case 'Email.Invalid':
      return { email: 'Introdu un email valid (ex. nume@student.ro).' }
    case 'Network.Unreachable':
      return { form: 'Serverul nu răspunde. Verifică dacă API-ul este pornit.' }
    case 'Request.TooManyRequests':
      return { form: 'Prea multe încercări. Încearcă din nou peste un minut.' }
    default:
      return { form: error.message }
  }
}

const SUCCESS_MESSAGE =
  'Dacă emailul există, ți-am trimis un link de resetare. Verifică și Spam.'

export function ForgotPasswordPage() {
  const { user } = useAuth()

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
        setFormError('A apărut o eroare neașteptată.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <ResetShell
      title="Ai uitat parola?"
      subtitle="Introdu emailul contului tău și îți trimitem un link pentru a seta o parolă nouă."
    >
      {sent ? (
        <Alert tone="success">{SUCCESS_MESSAGE}</Alert>
      ) : (
        <form onSubmit={handleSubmit} className="reset__form" noValidate>
          <TextField
            label="Email"
            type="email"
            autoComplete="email"
            placeholder="nume@student.ro"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={emailError}
            required
            autoFocus
          />
          {formError && <Alert tone="error">{formError}</Alert>}
          <Button type="submit" size="lg" block loading={submitting}>
            {submitting ? 'Se trimite…' : 'Trimite linkul de resetare'}
          </Button>
        </form>
      )}

      <p className="reset__footer">
        <Link to="/login">Înapoi la autentificare</Link>
      </p>
    </ResetShell>
  )
}
