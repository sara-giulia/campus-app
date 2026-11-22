import type { Role } from '../api/jwt'

// The links in the top menu (the login button on the right is not in this list). To add a page to the menu, add one line here
// (only add lines, do not reorder, so Sara's and Anca's PRs do not conflict).
// show: 'always' | 'guest' (only logged out) | 'user' (only logged in) | a role (only that role).
export interface MenuItem {
  to: string
  label: string
  show: 'always' | 'guest' | 'user' | Role
}

export const menuItems: MenuItem[] = [
  { to: '/', label: 'Acasă', show: 'always' },
  { to: '/register', label: 'Cont nou', show: 'guest' },
  { to: '/profile', label: 'Profilul meu', show: 'user' },
  { to: '/logout', label: 'Ieșire', show: 'user' },
]
