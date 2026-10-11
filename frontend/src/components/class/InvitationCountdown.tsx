import { Clock } from 'lucide-react';
import { useCountdown } from '../../hooks/useCountdown';
import { countdownTone, formatCountdown, type CountdownTone } from '../../utils/teamFormation';

interface Props {
  expiresAtUtc: string | null;
  serverTimeUtc?: string;
  /** Called once when the countdown reaches zero so the parent can refetch the authoritative status. */
  onExpire?: () => void;
  className?: string;
}

const TONE_CLASSES: Record<CountdownTone, string> = {
  normal: 'bg-slate-100 text-slate-700',
  warning: 'bg-amber-100 text-amber-800',
  danger: 'bg-red-100 text-red-700',
  expired: 'bg-slate-200 text-slate-500',
};

export default function InvitationCountdown({ expiresAtUtc, serverTimeUtc, onExpire, className = '' }: Props) {
  const remaining = useCountdown(expiresAtUtc, serverTimeUtc, onExpire);
  if (remaining === null) return null;
  const tone = countdownTone(remaining);
  const label = remaining === 0 ? 'Expired' : formatCountdown(remaining);

  return (
    <span
      role="timer"
      aria-label={remaining === 0 ? 'Invitation expired' : `Time left to respond: ${label}`}
      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-xs font-semibold tabular-nums ${TONE_CLASSES[tone]} ${className}`}
    >
      <Clock className="h-3 w-3" aria-hidden="true" />
      {label}
    </span>
  );
}
