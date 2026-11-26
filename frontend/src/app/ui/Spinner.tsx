import { useT } from '../../i18n/LanguageContext'

// Small spinner inside buttons and text.
export function Spinner({ large = false }: { large?: boolean }) {
  return <span className={large ? 'spinner spinner--lg' : 'spinner'} aria-hidden="true" />
}

// Whole-area loading state: <Loading text="Se încarcă anunțurile…" />
export function Loading({ text }: { text?: string }) {
  const t = useT()
  return (
    <div className="loading" role="status">
      <Spinner large />
      <span>{text ?? t('ui.loading')}</span>
    </div>
  )
}
