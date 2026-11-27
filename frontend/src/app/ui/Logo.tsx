// The CampusMarket mark: an open "C" (campus) with a dot (the market stall), in the accent colour.
export function LogoMark({ size = 30 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
      <rect width="32" height="32" rx="8" fill="var(--accent)" />
      <path d="M21.66 10.34A8 8 0 1 0 21.66 21.66" fill="none" stroke="var(--accent-ink)" strokeWidth="3.5" strokeLinecap="round" />
      <circle cx="20" cy="16" r="2" fill="var(--accent-ink)" />
    </svg>
  )
}

// The mark next to the wordmark, for the header.
export function Logo() {
  return (
    <span className="logo">
      <LogoMark size={30} />
      <span className="logo__name">campusmarket</span>
    </span>
  )
}
