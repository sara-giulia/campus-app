// The CampusMarket wordmark (as in the sketches) and a small square mark for compact places.
export function LogoMark({ size = 30 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true">
      <rect width="32" height="32" rx="8" fill="var(--accent)" />
      <path d="M9 16.5V10a1 1 0 0 1 1-1h6.5l7 7-7.5 7.5-7-7Z" fill="none" stroke="var(--accent-ink)" strokeWidth="2" strokeLinejoin="round" />
      <circle cx="13" cy="13" r="1.6" fill="var(--accent-ink)" />
    </svg>
  )
}

export function Logo() {
  return <span className="logo">campusmarket</span>
}
