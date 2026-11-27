import { useT } from '../../i18n/LanguageContext'
import { LostIllustration, StatusPage } from './StatusPage'

export function NotFoundPage() {
  const t = useT()
  return (
    <StatusPage
      code="404"
      title={t('notFound.title')}
      text={t('notFound.text')}
      illustration={<LostIllustration />}
      backLabel={t('notFound.back')}
    />
  )
}
