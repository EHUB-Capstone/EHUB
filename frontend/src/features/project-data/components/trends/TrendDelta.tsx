import { Minus, TrendingDown, TrendingUp } from 'lucide-react';
import { cn } from '../../../../utils/cn';
import { formatCountChange, formatScoreChange, trendDirection } from '../../../../utils/projectTrends';

const DIRECTION_STYLE = {
  up: { icon: TrendingUp, className: 'text-success', label: 'Up' },
  down: { icon: TrendingDown, className: 'text-danger', label: 'Down' },
  flat: { icon: Minus, className: 'text-slate-500', label: 'No change' },
  none: { icon: Minus, className: 'text-slate-500', label: 'No earlier semester' },
} as const;

/** A change since the previous semester: sign, icon and words together, so it never relies on color alone. */
export function ScoreChange({ change }: { change: number | null }) {
  const style = DIRECTION_STYLE[trendDirection(change)];
  const Icon = style.icon;
  return (
    <span className={cn('inline-flex items-center gap-1 font-semibold', style.className)} title={style.label}>
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      <span aria-label={`${style.label}: ${formatScoreChange(change)}`}>{formatScoreChange(change)}</span>
    </span>
  );
}

export function CountChange({ change }: { change: number | null }) {
  const style = DIRECTION_STYLE[trendDirection(change)];
  const Icon = style.icon;
  return (
    <span className={cn('inline-flex items-center gap-1 font-semibold', style.className)} title={style.label}>
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      <span aria-label={`${style.label}: ${formatCountChange(change)}`}>{formatCountChange(change)}</span>
    </span>
  );
}
