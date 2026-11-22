import { useEffect, useRef } from 'react'
import { useNavigate } from 'react-router'
import { useAuth } from '../../app/auth/AuthContext'
import { Loading } from '../../app/ui'

// Hitting /logout logs out (POST /auth/logout + clears tokens through useAuth) and goes to /.
// Used by the "Ieșire" link in the top menu.
export function LogoutPage() {
  const { logout } = useAuth()
  const navigate = useNavigate()
  const ran = useRef(false)

  useEffect(() => {
    // StrictMode fires effects twice in development; only trigger the logout once.
    if (ran.current) return
    ran.current = true
    void (async () => {
      try {
        await logout()
      } catch {
        // Server call may fail; useAuth.logout still clears local tokens.
      }
      navigate('/', { replace: true })
    })()
  }, [logout, navigate])

  return <Loading text="Se închide sesiunea…" />
}
