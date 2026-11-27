import { useT } from '../../i18n/LanguageContext'
import { BrokenIllustration, StatusPage } from './StatusPage'

// Shown when a page crashes unexpectedly (see app/ErrorBoundary.tsx and the root errorElement in app/router.tsx).
export function ErrorPage() {
  const t = useT()
  return (
    <StatusPage
      code={t('errorPage.code')}
      title={t('errorPage.title')}
      text={t('errorPage.text')}
      illustration={<BrokenIllustration />}
      backLabel={t('errorPage.back')}
    />
  )
}
