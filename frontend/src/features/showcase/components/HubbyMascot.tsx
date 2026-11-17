import { cn } from '../../../utils/cn';

interface HubbyMascotProps {
  className?: string;
  /** "thinking" speeds up the antenna and the flame while the assistant is working on an answer. */
  mood?: 'idle' | 'thinking';
  /** Turn off the idle motion where the mascot is only a small icon. */
  animated?: boolean;
}

/**
 * Hubby, the E-HUB showcase assistant: a small rocket-robot, because every showcase page is a project being launched.
 * Colors come from the design tokens (orange body, navy visor); the drawing is decorative, so it is hidden from screen readers.
 */
export default function HubbyMascot({ className, mood = 'idle', animated = true }: HubbyMascotProps): React.ReactElement {
  return (
    <svg
      viewBox="0 0 100 112"
      aria-hidden="true"
      focusable="false"
      className={cn('shrink-0 overflow-visible', animated && mood === 'thinking' && 'hubby-thinking', className)}
    >
      {/* Flame: the "launch" */}
      <g className={animated ? 'hubby-flame' : undefined}>
        <path d="M39 86 Q50 122 61 86 Z" className="fill-warning" />
        <path d="M45 86 Q50 108 55 86 Z" className="fill-warning-light" />
      </g>
      {/* Fins */}
      <path d="M22 62 L5 92 L31 84 Z" className="fill-secondary" strokeLinejoin="round" />
      <path d="M78 62 L95 92 L69 84 Z" className="fill-secondary" strokeLinejoin="round" />
      {/* Ear bolts */}
      <circle cx="14" cy="55" r="6" className="fill-secondary" />
      <circle cx="86" cy="55" r="6" className="fill-secondary" />
      <circle cx="14" cy="55" r="2" className="fill-primary-200" />
      <circle cx="86" cy="55" r="2" className="fill-primary-200" />
      {/* Antenna */}
      <g className={animated ? 'hubby-antenna' : undefined}>
        <line x1="50" y1="24" x2="50" y2="11" className="stroke-secondary" strokeWidth="3.5" strokeLinecap="round" />
        <circle cx="50" cy="8" r="5.5" className="fill-warning-light stroke-secondary" strokeWidth="3" />
      </g>
      {/* Body */}
      <rect x="16" y="22" width="68" height="64" rx="30" className="fill-primary stroke-secondary" strokeWidth="3.5" />
      <path d="M26 46 Q27 34 39 31" fill="none" className="stroke-white" strokeOpacity="0.55" strokeWidth="4" strokeLinecap="round" />
      {/* Visor, eyes and smile */}
      <rect x="25" y="38" width="50" height="32" rx="16" className="fill-secondary" />
      <ellipse cx="39" cy="52" rx="5.5" ry="6.5" className={cn('fill-white', animated && 'hubby-blink')} />
      <ellipse cx="61" cy="52" rx="5.5" ry="6.5" className={cn('fill-white', animated && 'hubby-blink')} />
      <circle cx="41" cy="50" r="1.8" className="fill-secondary" />
      <circle cx="63" cy="50" r="1.8" className="fill-secondary" />
      <path d="M43 62 Q50 68 57 62" fill="none" className="stroke-white" strokeWidth="2.8" strokeLinecap="round" />
      <circle cx="31" cy="62" r="3.2" className="fill-primary-400" fillOpacity="0.8" />
      <circle cx="69" cy="62" r="3.2" className="fill-primary-400" fillOpacity="0.8" />
      {/* Hub badge: three connected nodes */}
      <line x1="41" y1="78" x2="59" y2="78" className="stroke-white" strokeOpacity="0.7" strokeWidth="1.8" />
      <circle cx="41" cy="78" r="2.4" className="fill-white" fillOpacity="0.8" />
      <circle cx="59" cy="78" r="2.4" className="fill-white" fillOpacity="0.8" />
      <circle cx="50" cy="78" r="3.4" className="fill-white" />
    </svg>
  );
}
