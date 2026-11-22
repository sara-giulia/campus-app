import { api } from './client'
import { getTokens, setTokens, type Tokens } from './tokens'

// The /api/v1/auth endpoints. Pages do not call these directly:
// login and logout go through useAuth(), so the menu updates too.

export interface RegisterRequest {
  name: string
  email: string
  password: string
  confirmPassword: string
}

export interface RegisterResponse {
  id: string
  name: string
  email: string
  role: 'Student' | 'Admin'
  createdAt: string
}

export async function login(email: string, password: string): Promise<void> {
  const tokens = await api.post<Tokens>('/auth/login', { email, password }, { auth: false })
  setTokens(tokens)
}

export function register(body: RegisterRequest): Promise<RegisterResponse> {
  return api.post<RegisterResponse>('/auth/register', body, { auth: false })
}

export interface ForgotPasswordResponse {
  message: string
}

export function forgotPassword(email: string): Promise<ForgotPasswordResponse> {
  return api.post<ForgotPasswordResponse>('/auth/forgot-password', { email }, { auth: false })
}

export interface ResetPasswordRequest {
  token: string
  newPassword: string
  confirmNewPassword: string
}

export function resetPassword(body: ResetPasswordRequest): Promise<void> {
  return api.post<void>('/auth/reset-password', body, { auth: false })
}

// Revokes the refresh token on the server, then forgets the tokens locally.
// The local logout happens even if the server call fails (expired token, API down).
export async function logout(): Promise<void> {
  const tokens = getTokens()
  try {
    if (tokens) await api.post<void>('/auth/logout', { refreshToken: tokens.refreshToken }, { auth: false })
  } catch {
    // Nothing to do: the user is logged out locally anyway.
  } finally {
    setTokens(null)
  }
}
