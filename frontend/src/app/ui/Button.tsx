import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Link } from 'react-router'
import { Spinner } from './Spinner'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'
type Size = 'sm' | 'md' | 'lg'

interface CommonProps {
  variant?: Variant
  size?: Size
  block?: boolean
  children: ReactNode
}

function classes(variant: Variant, size: Size, block: boolean, extra?: string): string {
  return ['btn', `btn--${variant}`, size !== 'md' && `btn--${size}`, block && 'btn--block', extra]
    .filter(Boolean)
    .join(' ')
}

type ButtonProps = CommonProps & ButtonHTMLAttributes<HTMLButtonElement> & { loading?: boolean }

// <Button>Salvează</Button>, <Button variant="secondary" size="sm">, <Button loading>…
export function Button({ variant = 'primary', size = 'md', block = false, loading = false, className, children, disabled, type = 'button', ...props }: ButtonProps) {
  return (
    <button type={type} className={classes(variant, size, block, className)} disabled={disabled || loading} aria-busy={loading || undefined} {...props}>
      {loading && <Spinner />}
      {children}
    </button>
  )
}

// A link that looks like a button: <ButtonLink to="/login">Intră în cont</ButtonLink>
export function ButtonLink({ variant = 'primary', size = 'md', block = false, to, children }: CommonProps & { to: string }) {
  return (
    <Link to={to} className={classes(variant, size, block)}>
      {children}
    </Link>
  )
}
