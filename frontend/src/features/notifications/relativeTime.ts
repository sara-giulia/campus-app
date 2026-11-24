// Romanian relative time, used by the notifications list ("acum 5 minute", "ieri", "acum 3 zile").
// Not part of the shared app/ui on purpose: only this feature needs it for now; move it there
// when a second page needs the same wording.
const rtf = new Intl.RelativeTimeFormat('ro-RO', { numeric: 'auto' })

const dateTimeFormatter = new Intl.DateTimeFormat('ro-RO', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export function formatRelativeTime(iso: string, now: Date = new Date()): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso

  const diffSec = Math.round((date.getTime() - now.getTime()) / 1000)
  const abs = Math.abs(diffSec)

  if (abs < 60) return rtf.format(diffSec, 'second')
  if (abs < 3600) return rtf.format(Math.round(diffSec / 60), 'minute')
  if (abs < 86_400) return rtf.format(Math.round(diffSec / 3600), 'hour')
  if (abs < 7 * 86_400) return rtf.format(Math.round(diffSec / 86_400), 'day')
  if (abs < 30 * 86_400) return rtf.format(Math.round(diffSec / (7 * 86_400)), 'week')
  if (abs < 365 * 86_400) return rtf.format(Math.round(diffSec / (30 * 86_400)), 'month')
  return rtf.format(Math.round(diffSec / (365 * 86_400)), 'year')
}

export function formatAbsoluteDateTime(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : dateTimeFormatter.format(date)
}
