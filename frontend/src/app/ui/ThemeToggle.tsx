import { useState } from 'react'
import { useT } from '../../i18n/LanguageContext'
import { MoonIcon, SunIcon } from './Icons'
import { currentTheme, setTheme, type Theme } from './theme'

// The sun/moon button in the top bar.
export function ThemeToggle() {
  const t = useT()
  const [theme, setThemeState] = useState<Theme>(currentTheme)
  const next: Theme = theme === 'dark' ? 'light' : 'dark'
  const label = next === 'dark' ? t('theme.dark') : t('theme.light')

  return (
    <button
      type="button"
      className="icon-button"
      onClick={() => {
        setTheme(next)
        setThemeState(next)
      }}
      aria-label={label}
      title={label}
    >
      {theme === 'dark' ? <SunIcon /> : <MoonIcon />}
    </button>
  )
}
