import { createContext, useContext } from 'react'
import type { TKey } from './ro'
import type { Language, Params } from './store'

export interface LanguageValue {
  language: Language
  locale: string
  setLanguage: (language: Language) => void
  t: (key: TKey, params?: Params) => string
}

export const LanguageContext = createContext<LanguageValue | null>(null)

export function useLanguage(): LanguageValue {
  const value = useContext(LanguageContext)
  if (!value) throw new Error('useLanguage must be used inside <LanguageProvider>')
  return value
}

// const t = useT(); t('listing.title')
export function useT(): LanguageValue['t'] {
  return useLanguage().t
}
