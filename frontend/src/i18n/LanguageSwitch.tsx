import { useLanguage } from './LanguageContext'
import { LANGUAGES } from './store'

// "RO | EN" in the header menu.
export function LanguageSwitch() {
  const { language, setLanguage, t } = useLanguage()
  return (
    <div className="lang-switch" role="group" aria-label={t('language.label')}>
      {LANGUAGES.map((code, index) => (
        <span key={code}>
          {index > 0 && <span aria-hidden="true"> | </span>}
          <button
            type="button"
            className={code === language ? 'lang-switch__option lang-switch__option--active' : 'lang-switch__option'}
            aria-pressed={code === language}
            onClick={() => setLanguage(code)}
          >
            {code.toUpperCase()}
          </button>
        </span>
      ))}
    </div>
  )
}
