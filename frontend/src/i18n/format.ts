import { getLocale } from './store'

// Dates and prices in the current language's locale (ro-RO or en-GB).
export function formatDate(value: string | number | Date, options: Intl.DateTimeFormatOptions): string {
  return new Intl.DateTimeFormat(getLocale(), options).format(new Date(value))
}

export function formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
  return value.toLocaleString(getLocale(), options)
}

// 1250 → "1.250 RON" (ro) / "1,250 RON" (en). The currency stays RON.
export function formatRon(amount: number): string {
  return `${formatNumber(amount)} RON`
}

export function formatRelative(value: number, unit: Intl.RelativeTimeFormatUnit): string {
  return new Intl.RelativeTimeFormat(getLocale(), { numeric: 'auto' }).format(value, unit)
}
