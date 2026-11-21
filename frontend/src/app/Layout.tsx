import { useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import type { CurrentUser } from '../api/jwt'
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
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  // The phone menu stays open only on the page where it was opened, so it closes on navigation.
  const [menuOpenOn, setMenuOpenOn] = useState<string | null>(null)
  const menuOpen = menuOpenOn === location.pathname

  async function handleLogout() {
    await logout()
    navigate('/')
  }

  const links = menuItems.filter((item) => visible(item, user))

  return (
    <div className="shell">
      <header className="topbar">
        <div className="topbar__inner">
          <NavLink to="/" className="topbar__brand" aria-label="CampusMarket, pagina principală">
            <Logo />
          </NavLink>

          <nav className={menuOpen ? 'nav nav--open' : 'nav'} aria-label="Meniu principal">
            {links.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.to === '/'} className="nav__link">
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="topbar__actions">
            <ThemeToggle />
            {user ? (
              <div className="account">
                <span className="account__avatar" title={user.email} aria-label={user.email}>
                  {initials(user.email)}
                </span>
                <button type="button" className="icon-button" onClick={handleLogout} aria-label="Ieșire din cont" title="Ieșire din cont">
                  <LogoutIcon />
                </button>
              </div>
            ) : (
              location.pathname !== '/login' && (
                <ButtonLink to="/login" size="sm">
                  Intră în cont
                </ButtonLink>
              )
            )}
            <button
              type="button"
              className="icon-button topbar__burger"
              onClick={() => setMenuOpenOn(menuOpen ? null : location.pathname)}
              aria-expanded={menuOpen}
              aria-label={menuOpen ? 'Închide meniul' : 'Deschide meniul'}
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
          <span>© CampusMarket · marketplace pentru studenți</span>
        </div>
      </footer>
    </div>
  )
}
