import { formatDate, formatRelative } from '../../i18n/format'

// Relative time in the current language, used by the notifications list ("acum 5 minute" / "5 minutes ago",
// "ieri" / "yesterday"). Not part of the shared app/ui on purpose: only this feature needs it for now; move it
// there when a second page needs the same wording.
export function formatRelativeTime(iso: string, now: Date = new Date()): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso

  const diffSec = Math.round((date.getTime() - now.getTime()) / 1000)
  const abs = Math.abs(diffSec)

  if (abs < 60) return formatRelative(diffSec, 'second')
  if (abs < 3600) return formatRelative(Math.round(diffSec / 60), 'minute')
  if (abs < 86_400) return formatRelative(Math.round(diffSec / 3600), 'hour')
  if (abs < 7 * 86_400) return formatRelative(Math.round(diffSec / 86_400), 'day')
  if (abs < 30 * 86_400) return formatRelative(Math.round(diffSec / (7 * 86_400)), 'week')
  if (abs < 365 * 86_400) return formatRelative(Math.round(diffSec / (30 * 86_400)), 'month')
  return formatRelative(Math.round(diffSec / (365 * 86_400)), 'year')
}

export function formatAbsoluteDateTime(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime())
    ? iso
    : formatDate(date, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}
