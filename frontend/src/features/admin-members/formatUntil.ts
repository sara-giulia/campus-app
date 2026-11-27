import { formatDate } from '../../i18n/format'

// "4 decembrie 2026, 18:30" / "4 December 2026, 18:30", in the browser's time zone.
export function formatUntil(value: string | Date): string {
  return formatDate(value, { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}
