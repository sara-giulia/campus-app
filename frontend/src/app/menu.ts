import type { Role } from '../api/jwt'
import type { TKey } from '../i18n/ro'

// The links in the top menu (the login button on the right is not in this list). To add a page to the menu, add one line here
// (only add lines, do not reorder, so Sara's and Anca's PRs do not conflict).
// show: 'always' | 'guest' (only logged out) | 'user' (only logged in) | a role (only that role).
export interface MenuItem {
  to: string
  label: TKey // dictionary key, translated when the menu is drawn
  show: 'always' | 'guest' | 'user' | Role
  // 'account': in the menu that opens from the avatar (and in the phone menu), not in the top bar.
  place?: 'account'
}

export const menuItems: MenuItem[] = [
  { to: '/', label: 'menu.home', show: 'always' },
  { to: '/listings', label: 'menu.listings', show: 'always' },
  { to: '/register', label: 'menu.register', show: 'guest' },
  { to: '/profile', label: 'menu.profile', show: 'user', place: 'account' },
  { to: '/listings/new', label: 'menu.newListing', show: 'user' },
  { to: '/my/listings', label: 'menu.myListings', show: 'user', place: 'account' },
  { to: '/my/reservations', label: 'menu.myReservations', show: 'user', place: 'account' },
  { to: '/messages', label: 'menu.messages', show: 'user' },
  { to: '/my/favorites', label: 'menu.favorites', show: 'user', place: 'account' },
  { to: '/notifications', label: 'menu.notifications', show: 'user' },
  { to: '/admin/moderation', label: 'menu.moderation', show: 'Admin', place: 'account' },
  { to: '/admin/categories', label: 'menu.categories', show: 'Admin', place: 'account' },
  { to: '/admin/price-accuracy', label: 'menu.priceAccuracy', show: 'Admin', place: 'account' },
  { to: '/admin/members', label: 'menu.reportedMembers', show: 'Admin', place: 'account' },
]
