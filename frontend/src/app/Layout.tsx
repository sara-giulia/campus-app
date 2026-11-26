import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import type { CurrentUser } from '../api/jwt'
import { LanguageSwitch } from '../i18n/LanguageSwitch'
import { useT } from '../i18n/LanguageContext'
import { useNotifications } from '../features/notifications/NotificationsContext'
import { NotificationsProvider } from '../features/notifications/NotificationsProvider'
import { LiveEventsProvider } from '../features/live/LiveEventsProvider'
import { useAuth } from './auth/AuthContext'
import { menuItems, type MenuItem } from './menu'
import { ButtonLink, CloseIcon, Logo, LogoutIcon, MenuIcon, ThemeToggle } from './ui'
import './layout.css'

function visible(item: MenuItem, user: CurrentUser | null): boolean {
  switch (item.show) {
    case 'always':
      return true
    case 'guest':
      return user === null
    case 'user':
      return user !== null
    default:
      return user?.role === item.show
  }
}

// "ana.pop@student.ro" → "AP"
function initials(email: string): string {
  const parts = email.split('@')[0].split(/[._-]+/).filter(Boolean)
  return ((parts[0]?.[0] ?? '?') + (parts[1]?.[0] ?? '')).toUpperCase()
}

// The frame of every page: the top bar (logo, menu, theme, account), the page (<Outlet />), the footer.
export function Layout() {
  return (
    <LiveEventsProvider>
      <NotificationsProvider>
        <LayoutShell />
      </NotificationsProvider>
    </LiveEventsProvider>
  )
}

function LayoutShell() {
  const { user, logout } = useAuth()
  const { unreadCount } = useNotifications()
  const t = useT()
  const navigate = useNavigate()
  const location = useLocation()
  // The phone menu stays open only on the page where it was opened, so it closes on navigation.
  const [menuOpenOn, setMenuOpenOn] = useState<string | null>(null)
  const menuOpen = menuOpenOn === location.pathname
  // The account menu (from the avatar) works the same way, and also closes on a click outside it.
  const [accountOpenOn, setAccountOpenOn] = useState<string | null>(null)
  const accountOpen = accountOpenOn === location.pathname
  const accountRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!accountOpen) return
    function close(event: MouseEvent) {
      if (!accountRef.current?.contains(event.target as Node)) setAccountOpenOn(null)
    }
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [accountOpen])

  async function handleLogout() {
    await logout()
    navigate('/')
  }

  const links = menuItems.filter((item) => visible(item, user))
  const topLinks = links.filter((item) => item.place !== 'account')
  const accountLinks = links.filter((item) => item.place === 'account')

  const badge = (item: MenuItem) =>
    item.to === '/notifications' &&
    unreadCount !== null &&
    unreadCount > 0 && (
      <span className="nav__badge" aria-label={t('layout.unreadNotifications', { count: unreadCount })}>
        {unreadCount > 99 ? '99+' : unreadCount}
      </span>
    )

  return (
    <div className="shell">
      <header className="topbar">
        <div className="topbar__inner">
          <NavLink to="/" className="topbar__brand" aria-label={t('layout.homeLink')}>
            <Logo />
          </NavLink>

          <nav className={menuOpen ? 'nav nav--open' : 'nav'} aria-label={t('layout.mainMenu')}>
            {topLinks.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.to === '/'} className="nav__link">
                {t(item.label)}
                {badge(item)}
              </NavLink>
            ))}
            {/* On the phone the account links are part of this menu too. */}
            {accountLinks.map((item) => (
              <NavLink key={item.to} to={item.to} className="nav__link nav__link--account">
                {t(item.label)}
              </NavLink>
            ))}
          </nav>

          <div className="topbar__actions">
            <LanguageSwitch />
            <ThemeToggle />
            {user ? (
              <div className="account" ref={accountRef}>
                <button
                  type="button"
                  className="account__avatar"
                  title={user.email}
                  aria-label={t('layout.myAccount', { email: user.email })}
                  aria-expanded={accountOpen}
                  onClick={() => setAccountOpenOn(accountOpen ? null : location.pathname)}
                >
                  {initials(user.email)}
                </button>
                {accountOpen && (
                  <div className="account__menu" role="menu">
                    <p className="account__email">{user.email}</p>
                    {accountLinks.map((item) => (
                      <NavLink key={item.to} to={item.to} className="account__link" role="menuitem">
                        {t(item.label)}
                      </NavLink>
                    ))}
                    <button type="button" className="account__link account__logout" role="menuitem" onClick={handleLogout}>
                      <LogoutIcon size={16} /> {t('layout.logout')}
                    </button>
                  </div>
                )}
              </div>
            ) : (
              location.pathname !== '/login' && (
                <ButtonLink to="/login" size="sm">
                  {t('layout.login')}
                </ButtonLink>
              )
            )}
            <button
              type="button"
              className="icon-button topbar__burger"
              onClick={() => setMenuOpenOn(menuOpen ? null : location.pathname)}
              aria-expanded={menuOpen}
              aria-label={menuOpen ? t('layout.closeMenu') : t('layout.openMenu')}
            >
              {menuOpen ? <CloseIcon /> : <MenuIcon />}
            </button>
          </div>
        </div>
      </header>

      <main className="page">
        <Outlet />
      </main>

      <footer className="footer">
        <div className="footer__inner">
          <span>{t('layout.footer')}</span>
        </div>
      </footer>
    </div>
  )
}
