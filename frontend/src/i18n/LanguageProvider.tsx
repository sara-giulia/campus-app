import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { LanguageContext, type LanguageValue } from './LanguageContext'
import { applyLanguage, getLanguage, getLocale, translate, type Language } from './store'

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(getLanguage)

  // <html lang> follows the language (also on the first render, from the stored choice).
  useEffect(() => {
    document.documentElement.lang = language
  }, [language])

  const value = useMemo<LanguageValue>(
    () => ({
      language,
      locale: getLocale(),
      setLanguage: (next) => {
        // The store first (and localStorage), so plain functions like errorMessage already use the new language
        // when React renders again.
        applyLanguage(next)
        setLanguageState(next)
      },
      // A new t on every language change makes the components that use it render again.
      t: (key, params) => translate(key, params),
    }),
    [language],
  )

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>
}
