import { ButtonLink } from '../ui'
import './not-found.css'

export function NotFoundPage() {
  return (
    <div className="not-found">
      <p className="not-found__code">404</p>
      <h1>Pagina nu există</h1>
      <p className="muted">Linkul poate fi greșit sau pagina a fost ștearsă.</p>
      <ButtonLink to="/" variant="secondary">
        Înapoi la pagina principală
      </ButtonLink>
    </div>
  )
}
