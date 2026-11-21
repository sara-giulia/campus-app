import { useState } from 'react'
import { MoonIcon, SunIcon } from './Icons'
import { currentTheme, setTheme, type Theme } from './theme'

// The sun/moon button in the top bar.
export function ThemeToggle() {
  const [theme, setThemeState] = useState<Theme>(currentTheme)
  const next: Theme = theme === 'dark' ? 'light' : 'dark'
  const label = next === 'dark' ? 'Temă întunecată' : 'Temă luminoasă'

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
