export function LogoMark({ size = 38 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 40 40" aria-hidden="true">
      <rect className="logo__bg" width="40" height="40" rx="12" />
      <ellipse cx="20" cy="35.2" rx="6" ry="1.5" fill="#fff" opacity=".35" />
      <path d="M20 33C20 33 10 23.5 10 16.5a10 10 0 0 1 20 0C30 23.5 20 33 20 33z" fill="#fff" />
      <path className="logo__bg" d="M20 11.5l3.9 2.25v4.5L20 20.5l-3.9-2.25v-4.5z" />
      <circle cx="20" cy="16" r="1.5" fill="#fff" />
    </svg>
  );
}

export function Logo({ href = "/" }: { href?: string }) {
  return (
    <a className="logo" href={href} aria-label="GoFix, ir al inicio">
      <LogoMark />
      <span className="logo__go">Go</span>
      <span className="logo__fix">Fix</span>
    </a>
  );
}
