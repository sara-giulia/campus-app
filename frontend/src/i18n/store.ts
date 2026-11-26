import { en } from './en'
import { ro, type TKey } from './ro'

// The current language lives here (not only in React state) so plain functions, like the
// error-code mappings, can translate too. LanguageProvider keeps React in sync with it.
export type Language = 'ro' | 'en'

export const LANGUAGES: Language[] = ['ro', 'en']
const STORAGE_KEY = 'campusmarket.language'
const DICTIONARIES: Record<Language, Record<TKey, string>> = { ro, en }
const LOCALES: Record<Language, string> = { ro: 'ro-RO', en: 'en-GB' }

function readStored(): Language {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    if (value === 'ro' || value === 'en') return value
  } catch {
    // Storage blocked: use the default.
  }
  return 'ro'
}

let current: Language = readStored()

export function getLanguage(): Language {
  return current
}

export function getLocale(): string {
  return LOCALES[current]
}

export function applyLanguage(language: Language): void {
  current = language
  document.documentElement.lang = language
  try {
    localStorage.setItem(STORAGE_KEY, language)
  } catch {
    // Storage blocked: the choice only lasts until the page is closed.
  }
}

export type Params = Record<string, string | number>

export function hasKey(key: string): key is TKey {
  return key in ro
}

// translate('listing.reservedAt', { price: 20 }) replaces {price} in the text.
export function translate(key: TKey, params?: Params): string {
  const text = DICTIONARIES[current][key]
  if (!params) return text
  return text.replace(/\{(\w+)\}/g, (whole, name: string) => (name in params ? String(params[name]) : whole))
}
