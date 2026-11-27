import type { ReactNode } from 'react'
import { ButtonLink } from '../ui'
import './status.css'

interface StatusPageProps {
  code: string
  title: string
  text: string
  illustration: ReactNode
  backLabel: string
}

// The shared frame of the 404 and error pages: illustration, code, short message, button home.
export function StatusPage({ code, title, text, illustration, backLabel }: StatusPageProps) {
  return (
    <section className="status">
      <div className="status__art">{illustration}</div>
      <p className="status__code">{code}</p>
      <h1>{title}</h1>
      <p className="muted">{text}</p>
      <ButtonLink to="/" variant="secondary">
        {backLabel}
      </ButtonLink>
    </section>
  )
}

// Small original illustrations. Colors come from the theme variables (see status.css), so they follow dark mode.
export function LostIllustration() {
  return (
    <svg viewBox="0 0 160 120" width="160" height="120" aria-hidden="true" focusable="false">
      <rect x="34" y="14" width="72" height="92" rx="10" className="ill-blue" />
      <path d="M52 36h36M52 50h26M52 64h34" className="ill-paper-line" />
      <circle cx="106" cy="80" r="19" className="ill-paper" />
      <circle cx="106" cy="80" r="19" className="ill-ring" />
      <path d="M120 94l16 16" className="ill-handle" />
    </svg>
  )
}

export function BrokenIllustration() {
  return (
    <svg viewBox="0 0 160 120" width="160" height="120" aria-hidden="true" focusable="false">
      <rect x="28" y="18" width="104" height="84" rx="14" className="ill-yellow" />
      <path d="M80 36l24 42H56z" className="ill-triangle" />
      <path d="M80 52v14" className="ill-mark" />
      <circle cx="80" cy="73" r="2.8" className="ill-mark-dot" />
    </svg>
  )
}
