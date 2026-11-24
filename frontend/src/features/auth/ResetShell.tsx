import type { ReactNode } from 'react'
import './reset.css'

// The page frame shared by /forgot-password and /reset-password: form area on the left
// and the picture / quote panel on the right, same as RegisterPage.
export function ResetShell({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="reset">
      <div className="reset__form-side">
        <h1 className="reset__title">{title}</h1>
        {subtitle && <p className="reset__subtitle">{subtitle}</p>}
        {children}
      </div>
      <div className="reset__picture" aria-hidden="true">
        <p className="reset__quote">
          Te ajutăm să revii în cont.
          <small>campusmarket · piața studenților</small>
        </p>
      </div>
    </div>
  )
}
