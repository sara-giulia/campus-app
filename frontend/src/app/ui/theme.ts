// Light (paper) or dark (chalkboard) theme. The choice is kept in localStorage;
// without a choice the system setting is used. index.html applies it before React
// starts, so the page does not flash in the wrong theme.

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'campusmarket.theme'

export function currentTheme(): Theme {
  return document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light'
}

export function setTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme
  localStorage.setItem(STORAGE_KEY, theme)
}
