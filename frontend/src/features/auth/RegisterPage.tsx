import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router'
import { register, type RegisterResponse } from '../../api/auth'
import { ApiError } from '../../api/errors'
import { useAuth } from '../../app/auth/AuthContext'
import { Alert, Button, ButtonLink, TextField } from '../../app/ui'
import './register.css'

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
      return { fields: { password: 'Parola trebuie să aibă cel puțin 8 caractere.' }, form: null }
    case 'Auth.PasswordsDoNotMatch':
      return { fields: { confirmPassword: 'Cele două parole nu coincid.' }, form: null }
    case 'Member.EmailTaken':
      return { fields: { email: 'Există deja un cont cu acest email.' }, form: null }
    case 'Email.Empty':
    case 'Email.Invalid':
      return { fields: { email: 'Introdu un email valid (ex. nume@student.ro).' }, form: null }
    case 'Member.InvalidName':
      return { fields: { name: 'Numele trebuie să aibă între 2 și 100 de caractere.' }, form: null }
    case 'Member.PasswordRequired':
      return { fields: { password: 'Parola este obligatorie.' }, form: null }
    case 'Network.Unreachable':
      return { fields: {}, form: 'Serverul nu răspunde. Verifică dacă API-ul este pornit.' }
    case 'Request.TooManyRequests':
      return { fields: {}, form: 'Prea multe încercări. Încearcă din nou peste un minut.' }
    default:
      return { fields: {}, form: error.message }
  }
}

export function RegisterPage() {
  const { user } = useAuth()
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
        setFormError('A apărut o eroare neașteptată.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (registeredEmail) {
    return (
      <div className="register">
        <div className="register__form-side">
          <h1 className="register__title">Contul a fost creat</h1>
          <p className="register__subtitle">
            Bine ai venit, <strong>{registeredEmail}</strong>. Intră în cont pentru a continua.
          </p>
          <Alert tone="success">Contul tău a fost creat. Poți acum să intri în cont cu emailul și parola alese.</Alert>
          <div className="register__success-actions">
            <ButtonLink to="/login" size="lg" block>
              Intră în cont
            </ButtonLink>
          </div>
        </div>
        <div className="register__picture" aria-hidden="true">
          <p className="register__quote">
            Alătură-te comunității studenților.
            <small>campusmarket · piața studenților</small>
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="register">
      <div className="register__form-side">
        <h1 className="register__title">Fă-ți cont</h1>
        <p className="register__subtitle">Ai nevoie de un cont CampusMarket pentru a publica și cumpăra.</p>

        <form onSubmit={handleSubmit} className="register__form" noValidate>
          <TextField
            label="Nume"
            autoComplete="name"
            placeholder="Ana Popescu"
            value={name}
            onChange={(e) => setName(e.target.value)}
            error={fieldErrors.name}
            required
            autoFocus
          />
          <TextField
            label="Email"
            type="email"
            autoComplete="email"
            placeholder="nume@student.ro"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={fieldErrors.email}
            required
          />
          <TextField
            label="Parolă"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={fieldErrors.password}
            hint="Minim 8 caractere."
            required
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
            {submitting ? 'Se creează contul…' : 'Creează cont'}
          </Button>
        </form>
      </div>

      <div className="register__picture" aria-hidden="true">
        <p className="register__quote">
          Alătură-te comunității studenților.
          <small>campusmarket · piața studenților</small>
        </p>
      </div>
    </div>
  )
}
