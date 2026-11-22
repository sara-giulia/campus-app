import type { ReactNode } from 'react'

// A white panel with a border: <Card>…</Card>
export function Card({ className, children }: { className?: string; children: ReactNode }) {
  return <section className={className ? `card ${className}` : 'card'}>{children}</section>
}

// Title of a page, with a small label above it and actions on the right.
// <PageHeader eyebrow="Anunțuri" title="Anunțurile mele" lead="…" actions={<Button>…</Button>} />
export function PageHeader({ eyebrow, title, lead, actions }: { eyebrow?: string; title: string; lead?: ReactNode; actions?: ReactNode }) {
  return (
    <header className="page-header">
      <div>
        {eyebrow && <p className="page-header__eyebrow">{eyebrow}</p>}
        <h1>{title}</h1>
        {lead && <p className="page-header__lead">{lead}</p>}
      </div>
      {actions}
    </header>
  )
}

// A rounded label, for categories and filters: <Chip>Manuale</Chip>, <Chip active>…</Chip>
export function Chip({ active = false, children }: { active?: boolean; children: ReactNode }) {
  return <span className={active ? 'chip chip--active' : 'chip'}>{children}</span>
}
