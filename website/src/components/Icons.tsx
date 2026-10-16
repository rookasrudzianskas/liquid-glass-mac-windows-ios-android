import type { SVGProps } from "react";

type IconProps = SVGProps<SVGSVGElement> & { size?: number };

function Icon({ size = 20, children, ...rest }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...rest}
    >
      {children}
    </svg>
  );
}

export const SearchIcon = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="11" cy="11" r="6.5" />
    <path d="m16 16 4.5 4.5" />
  </Icon>
);
export const SunIcon = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="12" cy="12" r="4" />
    <path d="M12 2.5v2M12 19.5v2M4.6 4.6 6 6M18 18l1.4 1.4M2.5 12h2M19.5 12h2M4.6 19.4 6 18M18 6l1.4-1.4" />
  </Icon>
);
export const MoonIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5Z" />
  </Icon>
);
export const SystemIcon = (p: IconProps) => (
  <Icon {...p}>
    <rect x="3" y="4.5" width="18" height="12" rx="2.5" />
    <path d="M8.5 20h7M12 16.5V20" />
  </Icon>
);
export const MenuIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4 8h16M4 16h16" />
  </Icon>
);
export const CloseIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M6 6l12 12M18 6 6 18" />
  </Icon>
);
export const CopyIcon = (p: IconProps) => (
  <Icon {...p}>
    <rect x="8.5" y="8.5" width="11" height="11" rx="2.5" />
    <path d="M15.5 8.5V6.5a2 2 0 0 0-2-2h-7a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h2" />
  </Icon>
);
export const CheckIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="m5 12.5 4.5 4.5L19 7.5" />
  </Icon>
);
export const DownloadIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 4v11M7 10.5l5 5 5-5M5 19.5h14" />
  </Icon>
);
export const ArrowRightIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M5 12h14M13 6l6 6-6 6" />
  </Icon>
);
export const ArrowUpRightIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M7 17 17 7M8.5 7H17v8.5" />
  </Icon>
);
export const ChevronDownIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="m6 9.5 6 6 6-6" />
  </Icon>
);
export const DropIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 3.5s6.5 7 6.5 11.25a6.5 6.5 0 0 1-13 0C5.5 10.5 12 3.5 12 3.5Z" />
    <path d="M9 15a3 3 0 0 0 3 3" />
  </Icon>
);
export const PaletteIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 3.5a8.5 8.5 0 0 0 0 17c1.4 0 2-1 1.6-2.1-.5-1.3.3-2.4 1.7-2.4h2.1a3.1 3.1 0 0 0 3.1-3.1C20.5 7.3 16.7 3.5 12 3.5Z" />
    <circle cx="7.8" cy="11" r="1.1" fill="currentColor" stroke="none" />
    <circle cx="10.5" cy="7.3" r="1.1" fill="currentColor" stroke="none" />
    <circle cx="15" cy="7.6" r="1.1" fill="currentColor" stroke="none" />
  </Icon>
);
export const SparkleIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 3.5c.6 4.4 2.2 6 6.5 6.6-4.3.6-5.9 2.2-6.5 6.6-.6-4.4-2.2-6-6.5-6.6 4.3-.6 5.9-2.2 6.5-6.6Z" />
    <path d="M18.5 15.5c.2 1.6.8 2.2 2.4 2.4-1.6.2-2.2.8-2.4 2.4-.2-1.6-.8-2.2-2.4-2.4 1.6-.2 2.2-.8 2.4-2.4Z" />
  </Icon>
);
export const HandIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M9 11V5.5a1.5 1.5 0 0 1 3 0V11m0-1.5V4a1.5 1.5 0 0 1 3 0v6.5m0-3a1.5 1.5 0 0 1 3 0V14a6.5 6.5 0 0 1-6.5 6.5h-.6a6 6 0 0 1-4.6-2.2L4.6 15.2a1.6 1.6 0 0 1 2.3-2.2L9 15" />
  </Icon>
);
export const PhoneIcon = (p: IconProps) => (
  <Icon {...p}>
    <rect x="6.5" y="2.5" width="11" height="19" rx="3" />
    <path d="M10.5 5.5h3" />
  </Icon>
);
export const LaptopIcon = (p: IconProps) => (
  <Icon {...p}>
    <rect x="4.5" y="5" width="15" height="10" rx="1.8" />
    <path d="M2.5 18.5h19" />
  </Icon>
);
export const AndroidIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M5 16.5a7 7 0 0 1 14 0Z" />
    <path d="m7.5 9.5-1.6-2.6M16.5 9.5l1.6-2.6" />
    <circle cx="9.3" cy="13.2" r=".9" fill="currentColor" stroke="none" />
    <circle cx="14.7" cy="13.2" r=".9" fill="currentColor" stroke="none" />
  </Icon>
);
export const WindowIcon = (p: IconProps) => (
  <Icon {...p}>
    <rect x="3" y="4" width="18" height="16" rx="2.5" />
    <path d="M3 8.5h18M6.5 6.3h.01M9 6.3h.01" />
  </Icon>
);
export const BookIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4.5 5.5A2 2 0 0 1 6.5 3.5H19.5v15H6.5a2 2 0 0 0-2 2Z" />
    <path d="M4.5 20.5a2 2 0 0 1 2-2" />
    <path d="M9 8h6.5" />
  </Icon>
);
export const TerminalIcon = (p: IconProps) => (
  <Icon {...p}>
    <rect x="3" y="4.5" width="18" height="15" rx="2.5" />
    <path d="m7 9.5 3 2.5-3 2.5M12.5 15h4.5" />
  </Icon>
);
export const PackageIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 3 20 7.5v9L12 21l-8-4.5v-9Z" />
    <path d="m4 7.5 8 4.5 8-4.5M12 12v9" />
  </Icon>
);
export const ShieldIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 3.5 19 6v5.5c0 4.4-3 7.8-7 9-4-1.2-7-4.6-7-9V6Z" />
    <path d="m9 12 2.2 2.2L15.5 10" />
  </Icon>
);
export const GaugeIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4.5 17a8.5 8.5 0 1 1 15 0" />
    <path d="m12 13 3.5-4.5" />
    <circle cx="12" cy="13" r="1.2" fill="currentColor" stroke="none" />
  </Icon>
);
export const LayersIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="m12 3.5 9 4.75-9 4.75-9-4.75Z" />
    <path d="m3 12.5 9 4.75 9-4.75" />
    <path d="m3 16.5 9 4.75 9-4.75" opacity=".5" />
  </Icon>
);
export const GitHubIcon = ({ size = 20, ...rest }: IconProps) => (
  <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true" focusable="false" {...rest}>
    <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8Z" />
  </svg>
);

/** The Liquid Glass Engineering mark: a lens magnifying a wave. */
export function LogoMark({ size = 28 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false" className="logo-mark">
      <defs>
        <linearGradient id="lm-a" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#3ad9e6" />
          <stop offset=".5" stopColor="#7a5cff" />
          <stop offset="1" stopColor="#ff5fa2" />
        </linearGradient>
        <linearGradient id="lm-r" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#fff" stopOpacity="1" />
          <stop offset=".5" stopColor="#fff" stopOpacity=".1" />
          <stop offset="1" stopColor="#fff" stopOpacity=".85" />
        </linearGradient>
      </defs>
      <rect x="1" y="1" width="30" height="30" rx="9.5" fill="url(#lm-a)" />
      <path d="M1 18.5c5-3 9.5 2.6 15-.4s9.6-2.6 15-.1V22c-5.4-2.5-9.5-2.2-15 .8S6 21.5 1 24.5Z" fill="#fff" opacity=".35" />
      <circle cx="16" cy="15.5" r="8" fill="#fff" fillOpacity=".18" stroke="url(#lm-r)" strokeWidth="1.4" />
    </svg>
  );
}
