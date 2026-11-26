import { useEffect, useState, type FormEvent } from 'react'
import { api } from '../../api/client'
import { ApiError } from '../../api/errors'
import { Alert, Badge, Button, Card, Loading, PageHeader, TextField } from '../../app/ui'
import { formatDate } from '../../i18n/format'
import { BlockedMembersCard } from '../blocks/BlockedMembersCard'
import './profile.css'
import { useT } from '../../i18n/LanguageContext'
import { translate } from '../../i18n/store'

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
      return { field: translate('profile.error.InvalidName') }
    case 'Network.Unreachable':
      return { form: translate('common.networkUnreachable') }
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
      return { current: translate('profile.error.currentWrong') }
    case 'Auth.WeakPassword':
      return { next: translate('profile.error.newWeak') }
    case 'Auth.PasswordsDoNotMatch':
      return { confirm: translate('auth.error.PasswordsDoNotMatch') }
    case 'Member.PasswordRequired':
      return { next: translate('profile.error.newRequired') }
    case 'Network.Unreachable':
      return { form: translate('common.networkUnreachable') }
    default:
      return { form: error.message }
  }
}

function formatMemberDate(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : formatDate(date, { year: 'numeric', month: 'long', day: 'numeric' })
}

export function MyProfilePage() {
  const t = useT()
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
          setLoadError(err.code === 'Network.Unreachable' ? translate('common.networkUnreachable') : err.message)
        } else {
          setLoadError(translate('common.unexpectedError'))
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
        setNameFormError(translate('common.unexpectedError'))
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
        setPasswordFormError(translate('common.unexpectedError'))
      }
    } finally {
      setPasswordSaving(false)
    }
  }

  if (loading) return <Loading text={t('profile.loading')} />

  if (loadError || !profile) {
    return (
      <div className="profile">
        <PageHeader eyebrow={t('profile.eyebrow')} title={t('profile.title')} />
        <Alert tone="error">{loadError ?? t('profile.loadFailed')}</Alert>
      </div>
    )
  }

  const roleLabel = profile.role === 'Admin' ? t('profile.role.admin') : t('profile.role.student')

  return (
    <div className="profile">
      <PageHeader eyebrow={t('profile.eyebrow')} title={t('profile.title')} lead={t('profile.lead')} />

      <Card className="profile__card">
        <h2 className="profile__card-title">{t('profile.details.title')}</h2>
        <dl className="profile__details">
          <div>
            <dt>{t('profile.details.name')}</dt>
            <dd>{profile.name}</dd>
          </div>
          <div>
            <dt>{t('profile.details.email')}</dt>
            <dd>{profile.email}</dd>
          </div>
          <div>
            <dt>{t('profile.details.role')}</dt>
            <dd>
              <Badge tone={profile.role === 'Admin' ? 'accent' : 'neutral'}>{roleLabel}</Badge>
            </dd>
          </div>
          <div>
            <dt>{t('profile.details.memberSince')}</dt>
            <dd>{formatMemberDate(profile.createdAt)}</dd>
          </div>
        </dl>
      </Card>

      <Card className="profile__card">
        <h2 className="profile__card-title">{t('profile.name.title')}</h2>
        <form onSubmit={handleNameSubmit} className="profile__form" noValidate>
          <TextField
            label={t('profile.name.label')}
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
          {nameSuccess && <Alert tone="success">{t('profile.name.updated')}</Alert>}
          <div className="profile__form-actions">
            <Button type="submit" loading={nameSaving} disabled={name.trim() === profile.name}>
              {nameSaving ? t('profile.saving') : t('profile.name.submit')}
            </Button>
          </div>
        </form>
      </Card>

      <Card className="profile__card">
        <h2 className="profile__card-title">{t('profile.password.title')}</h2>
        <form onSubmit={handlePasswordSubmit} className="profile__form" noValidate>
          <TextField
            label={t('profile.password.current')}
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            error={passwordErrors.current}
            required
          />
          <TextField
            label={t('profile.password.new')}
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            error={passwordErrors.next}
            hint={t('auth.passwordHint')}
            required
          />
          <TextField
            label={t('profile.password.confirm')}
            type="password"
            autoComplete="new-password"
            value={confirmNewPassword}
            onChange={(e) => setConfirmNewPassword(e.target.value)}
            error={passwordErrors.confirm}
            required
          />
          {passwordFormError && <Alert tone="error">{passwordFormError}</Alert>}
          {passwordSuccess && <Alert tone="success">{t('profile.password.changed')}</Alert>}
          <div className="profile__form-actions">
            <Button type="submit" loading={passwordSaving}>
              {passwordSaving ? t('profile.saving') : t('profile.password.submit')}
            </Button>
          </div>
        </form>
      </Card>

      <BlockedMembersCard />
    </div>
  )
}
