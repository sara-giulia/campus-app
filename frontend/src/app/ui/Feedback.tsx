import type { ReactNode } from 'react'
import { AlertIcon, CheckIcon, SparkleIcon } from './Icons'

type Tone = 'error' | 'success' | 'info'

const icons = { error: AlertIcon, success: CheckIcon, info: SparkleIcon }

// <Alert tone="error">Email sau parolă greșită.</Alert>
export function Alert({ tone = 'info', children }: { tone?: Tone; children: ReactNode }) {
  const Icon = icons[tone]
  return (
    <div className={`alert alert--${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
      <Icon size={18} />
      <div>{children}</div>
    </div>
  )
}

type BadgeTone = 'neutral' | 'accent' | 'success' | 'warning' | 'danger'

// <Badge tone="success">Activ</Badge>
export function Badge({ tone = 'neutral', children }: { tone?: BadgeTone; children: ReactNode }) {
  return <span className={tone === 'neutral' ? 'badge' : `badge badge--${tone}`}>{children}</span>
}

// Nothing to show yet: <EmptyState title="Niciun anunț" action={<ButtonLink ...>}>text</EmptyState>
export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="empty">
      <p className="empty__title">{title}</p>
      {children && <p>{children}</p>}
      {action}
    </div>
  )
}
