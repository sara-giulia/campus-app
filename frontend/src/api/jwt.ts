// Reads the claims the API puts in the access token (JwtTokenService):
// sub = member id, email, role = "Student" | "Admin", exp = expiry in seconds.
// Only for showing the UI; the API still checks the signature on every request.

export type Role = 'Student' | 'Admin'

export interface CurrentUser {
  id: string
  email: string
  role: Role
}

interface Claims {
  sub?: string
  email?: string
  role?: string
}

export function readUser(token: string): CurrentUser | null {
  const payload = token.split('.')[1]
  if (!payload) return null
  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const json = new TextDecoder().decode(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)))
    const claims = JSON.parse(json) as Claims
    if (!claims.sub || !claims.email) return null
    return { id: claims.sub, email: claims.email, role: claims.role === 'Admin' ? 'Admin' : 'Student' }
  } catch {
    return null
  }
}
