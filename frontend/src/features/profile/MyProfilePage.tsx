import { useEffect, useState, type FormEvent } from 'react'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Alert, Badge, Button, Card, Loading, PageHeader, TextField } from '../../app/ui'
import './profile.css'

interface MyProfile {
  id: string
  name: string
  email: string
  role: 'Student' | 'Admin'
  createdAt: string
}

// Messages for PUT /members/me.
function nameErrorMessage(error: ApiError): { field?: string; form?: string } {
  switch (error.code) {
    case 'Member.InvalidName':
      return { field: 'Numele trebuie să aibă între 2 și 100 de caractere.' }
    case 'Network.Unreachable':
      return { form: 'Serverul nu răspunde. Verifică dacă API-ul este pornit.' }
    default:
      return { form: error.message }
  }
}

// Messages for POST /auth/change-password.
function passwordErrorMessage(error: ApiError): {
  current?: string
  next?: string
  confirm?: string
  form?: string
} {
  switch (error.code) {
    case 'Auth.InvalidCredentials':
      return { current: 'Parola actuală nu este corectă.' }
    case 'Auth.WeakPassword':
      return { next: 'Parola nouă trebuie să aibă cel puțin 8 caractere.' }
    case 'Auth.PasswordsDoNotMatch':
      return { confirm: 'Cele două parole nu coincid.' }
    case 'Member.PasswordRequired':
      return { next: 'Parola nouă este obligatorie.' }
    case 'Network.Unreachable':
      return { form: 'Serverul nu răspunde. Verifică dacă API-ul este pornit.' }
    default:
      return { form: error.message }
  }
}

const dateFormatter = new Intl.DateTimeFormat('ro-RO', { year: 'numeric', month: 'long', day: 'numeric' })

function formatDate(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : dateFormatter.format(date)
}

export function MyProfilePage() {
  const [profile, setProfile] = useState<MyProfile | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  // "Change name" form.
  const [name, setName] = useState('')
  const [nameFieldError, setNameFieldError] = useState<string | null>(null)
  const [nameFormError, setNameFormError] = useState<string | null>(null)
  const [nameSuccess, setNameSuccess] = useState(false)
  const [nameSaving, setNameSaving] = useState(false)

  // "Change password" form.
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmNewPassword, setConfirmNewPassword] = useState('')
  const [passwordErrors, setPasswordErrors] = useState<{ current?: string; next?: string; confirm?: string }>({})
  const [passwordFormError, setPasswordFormError] = useState<string | null>(null)
  const [passwordSuccess, setPasswordSuccess] = useState(false)
  const [passwordSaving, setPasswordSaving] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    api
      .get<MyProfile>('/members/me', { signal: controller.signal })
      .then((data) => {
        setProfile(data)
        setName(data.name)
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        if (err instanceof ApiError) {
          setLoadError(err.code === 'Network.Unreachable' ? 'Serverul nu răspunde. Verifică dacă API-ul este pornit.' : err.message)
        } else {
          setLoadError('A apărut o eroare neașteptată.')
        }
      })
      .finally(() => setLoading(false))
    return () => controller.abort()
  }, [])

  async function handleNameSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!profile) return
    setNameFieldError(null)
    setNameFormError(null)
    setNameSuccess(false)
    setNameSaving(true)
    try {
      await api.put<void>('/members/me', { name: name.trim() })
      setProfile({ ...profile, name: name.trim() })
      setNameSuccess(true)
    } catch (err) {
      if (err instanceof ApiError) {
        const placed = nameErrorMessage(err)
        setNameFieldError(placed.field ?? null)
        setNameFormError(placed.form ?? null)
      } else {
        setNameFormError('A apărut o eroare neașteptată.')
      }
    } finally {
      setNameSaving(false)
    }
  }

  async function handlePasswordSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPasswordErrors({})
    setPasswordFormError(null)
    setPasswordSuccess(false)
    setPasswordSaving(true)
    try {
      await api.post<void>('/auth/change-password', {
        currentPassword,
        newPassword,
        confirmNewPassword,
      })
      setCurrentPassword('')
      setNewPassword('')
      setConfirmNewPassword('')
      setPasswordSuccess(true)
    } catch (err) {
      if (err instanceof ApiError) {
        const placed = passwordErrorMessage(err)
        setPasswordErrors({ current: placed.current, next: placed.next, confirm: placed.confirm })
        setPasswordFormError(placed.form ?? null)
      } else {
        setPasswordFormError('A apărut o eroare neașteptată.')
      }
    } finally {
      setPasswordSaving(false)
    }
  }

  if (loading) return <Loading text="Se încarcă profilul…" />

  if (loadError || !profile) {
    return (
      <div className="profile">
        <PageHeader eyebrow="Contul meu" title="Profilul meu" />
        <Alert tone="error">{loadError ?? 'Profilul nu a putut fi încărcat.'}</Alert>
      </div>
    )
  }

  const roleLabel = profile.role === 'Admin' ? 'Administrator' : 'Student'

  return (
    <div className="profile">
      <PageHeader eyebrow="Contul meu" title="Profilul meu" lead="Vezi și actualizează datele contului tău." />

      <Card className="profile__card">
        <h2 className="profile__card-title">Datele contului</h2>
        <dl className="profile__details">
          <div>
            <dt>Nume</dt>
            <dd>{profile.name}</dd>
          </div>
          <div>
            <dt>Email</dt>
            <dd>{profile.email}</dd>
          </div>
          <div>
            <dt>Rol</dt>
            <dd>
              <Badge tone={profile.role === 'Admin' ? 'accent' : 'neutral'}>{roleLabel}</Badge>
            </dd>
          </div>
          <div>
            <dt>Membru din</dt>
            <dd>{formatDate(profile.createdAt)}</dd>
          </div>
        </dl>
      </Card>

      <Card className="profile__card">
        <h2 className="profile__card-title">Schimbă numele</h2>
        <form onSubmit={handleNameSubmit} className="profile__form" noValidate>
          <TextField
            label="Nume"
            value={name}
            onChange={(e) => {
              setName(e.target.value)
              setNameSuccess(false)
            }}
            error={nameFieldError}
            autoComplete="name"
            required
          />
          {nameFormError && <Alert tone="error">{nameFormError}</Alert>}
          {nameSuccess && <Alert tone="success">Numele a fost actualizat.</Alert>}
          <div className="profile__form-actions">
            <Button type="submit" loading={nameSaving} disabled={name.trim() === profile.name}>
              {nameSaving ? 'Se salvează…' : 'Salvează numele'}
            </Button>
          </div>
        </form>
      </Card>

      <Card className="profile__card">
        <h2 className="profile__card-title">Schimbă parola</h2>
        <form onSubmit={handlePasswordSubmit} className="profile__form" noValidate>
          <TextField
            label="Parola actuală"
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            error={passwordErrors.current}
            required
          />
          <TextField
            label="Parola nouă"
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            error={passwordErrors.next}
            hint="Minim 8 caractere."
            required
          />
          <TextField
            label="Confirmă parola nouă"
            type="password"
            autoComplete="new-password"
            value={confirmNewPassword}
            onChange={(e) => setConfirmNewPassword(e.target.value)}
            error={passwordErrors.confirm}
            required
          />
          {passwordFormError && <Alert tone="error">{passwordFormError}</Alert>}
          {passwordSuccess && <Alert tone="success">Parola a fost schimbată.</Alert>}
          <div className="profile__form-actions">
            <Button type="submit" loading={passwordSaving}>
              {passwordSaving ? 'Se salvează…' : 'Schimbă parola'}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  )
}
