import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { ApiError } from '../../api/errors'
import { useAuth } from '../auth/AuthContext'
import { Alert, Button, Card, LogoMark, TextField } from '../ui'
import './login.css'

// Messages for the error codes POST /auth/login can return.
function loginErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return 'A apărut o eroare neașteptată.'
  switch (error.code) {
    case 'Auth.InvalidCredentials':
      return 'Email sau parolă greșită.'
    case 'Request.TooManyRequests':
      return 'Prea multe încercări. Încearcă din nou peste un minut.'
    case 'Network.Unreachable':
      return 'Serverul nu răspunde. Verifică dacă API-ul este pornit.'
    default:
      return error.message
  }
}

export function LoginPage() {
  const { user, login } = useAuth()
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
      <div className="login__heading">
        <LogoMark size={40} />
        <div>
          <h1>Intră în cont</h1>
          <p className="muted">Folosește emailul și parola contului CampusMarket.</p>
        </div>
      </div>

      <Card className="login__card">
        <form onSubmit={handleSubmit} className="login__form">
          <TextField label="Email" type="email" autoComplete="email" placeholder="nume@student.ro" value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
          <TextField label="Parolă" type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          {error && <Alert tone="error">{error}</Alert>}
          <Button type="submit" size="lg" block loading={submitting}>
            {submitting ? 'Se verifică…' : 'Intră în cont'}
          </Button>
        </form>
      </Card>
    </div>
  )
}
