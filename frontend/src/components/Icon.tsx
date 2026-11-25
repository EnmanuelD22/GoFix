const PATHS = {
  bolt: "M13 2 4 14h7l-1 8 9-12h-7z",
  drop: "M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11z",
  snow: "M12 2v20M4 7l16 10M20 7 4 17",
  hammer: "M14 4l6 6-3 3-6-6zM11 7 3 15l3 3 8-8",
  paint: "M4 4h13v6H4zM17 7h3v5h-8v3M11 15v6",
  tv: "M3 5h18v12H3zM8 21h8M12 17v4",
  search: "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM21 21l-5-5",
  check: "M5 12l4 4 10-10",
  wrench: "M14 6a4 4 0 0 0 5 5l-9 9a2.1 2.1 0 0 1-3-3l9-9a4 4 0 0 0-2-2z",
  home: "M3 11l9-8 9 8v9H3zM9 20v-6h6v6",
  list: "M8 6h13M8 12h13M8 18h13M3 6h1M3 12h1M3 18h1",
  user: "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4 21a8 8 0 0 1 16 0",
  cal: "M4 6h16v14H4zM4 10h16M8 3v4M16 3v4",
  clock: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 7v5l3 2",
  play: "M7 4l13 8-13 8z",
  arrow: "M5 12h14M13 6l6 6-6 6",
  star: "M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1L3.2 9.5l6.1-.9z",
} as const;

export type IconName = keyof typeof PATHS;

type IconProps = { name: IconName; size?: number; filled?: boolean };

export function Icon({ name, size = 22, filled = false }: IconProps) {
  return (
    <svg className="icon" width={size} height={size} viewBox="0 0 24 24" fill={filled ? "currentColor" : "none"} stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={PATHS[name]} />
    </svg>
  );
}
