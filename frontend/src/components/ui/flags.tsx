import type { LanguageCode } from '../../i18n/config';

// Inline SVG flags: emoji flags render as plain letters on Windows, so they are drawn here.
const UnitedKingdomFlag = () => (
  <svg viewBox="0 0 60 40" className="h-full w-full" aria-hidden="true" focusable="false">
    <rect width="60" height="40" fill="#012169" />
    <path d="M0 0 60 40M60 0 0 40" stroke="#FFFFFF" strokeWidth="8" />
    <path d="M0 0 60 40M60 0 0 40" stroke="#C8102E" strokeWidth="3" />
    <path d="M30 0v40M0 20h60" stroke="#FFFFFF" strokeWidth="13" />
    <path d="M30 0v40M0 20h60" stroke="#C8102E" strokeWidth="8" />
  </svg>
);

const VietnamFlag = () => (
  <svg viewBox="0 0 60 40" className="h-full w-full" aria-hidden="true" focusable="false">
    <rect width="60" height="40" fill="#DA251D" />
    <path
      d="m30 8 3.53 10.86h11.42l-9.24 6.71 3.53 10.86L30 29.72l-9.24 6.71 3.53-10.86-9.24-6.71h11.42Z"
      fill="#FFFF00"
    />
  </svg>
);

const flags: Record<LanguageCode, () => React.ReactElement> = {
  en: UnitedKingdomFlag,
  vi: VietnamFlag,
};

export const LanguageFlag = ({ code }: { code: LanguageCode }) => {
  const Flag = flags[code];
  return (
    <span className="inline-block h-3.5 w-5 shrink-0 overflow-hidden rounded-[3px] shadow-xs ring-1 ring-slate-900/10">
      <Flag />
    </span>
  );
};
