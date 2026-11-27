import { useEffect } from 'react'
import { useNavigate } from 'react-router'
import { onAccountSuspendedChange, isAccountSuspended } from '../../api/suspension'

// When the client finds out the account was suspended (401 Auth.AccountSuspended on a request, the refresh
// or the live stream), it logs the user out; this sends them to the login page, which says why.
// On a page that needs login RequireAuth would do it anyway; this covers the public pages too.
export function AccountSuspendedRedirect() {
  const navigate = useNavigate()

  useEffect(
    () =>
      onAccountSuspendedChange(() => {
        if (isAccountSuspended()) navigate('/login', { replace: true })
      }),
    [navigate],
  )

  return null
}
