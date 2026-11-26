import { useT } from '../../i18n/LanguageContext'
import { ButtonLink } from '../ui'
import './not-found.css'

export function NotFoundPage() {
  const t = useT()
  return (
    <div className="not-found">
      <p className="not-found__code">404</p>
      <h1>{t('notFound.title')}</h1>
      <p className="muted">{t('notFound.text')}</p>
      <ButtonLink to="/" variant="secondary">
        {t('notFound.back')}
      </ButtonLink>
    </div>
  )
}
