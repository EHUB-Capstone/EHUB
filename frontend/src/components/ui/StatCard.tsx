import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '../../utils/cn';
import { motion } from 'framer-motion';
import { TrendingUp, TrendingDown, Minus } from 'lucide-react';

const trendIcons = {
  up: TrendingUp,
  down: TrendingDown,
  flat: Minus,
};

const trendColors = {
  up: 'text-success-dark',
  down: 'text-danger-dark',
  flat: 'text-slate-500',
};

// indigo, violet, cyan and orange are kept as accepted values for existing call
// sites; they resolve to the palette's navy, info and brand tones.
const NAVY = 'bg-secondary-50 text-secondary';
const INFO = 'bg-info-50 text-info-dark';
const BRAND = 'bg-primary-50 text-primary-700';
const iconBgColors = {
  primary: BRAND,
  orange: BRAND,
  navy: NAVY,
  secondary: NAVY,
  indigo: NAVY,
  violet: NAVY,
  cyan: INFO,
  info: INFO,
  success: 'bg-success-50 text-success-dark',
  warning: 'bg-warning-50 text-warning-dark',
  danger: 'bg-danger-50 text-danger-dark',
};

const accentBars: Record<keyof typeof iconBgColors, string> = {
  primary: 'bg-primary', orange: 'bg-primary', navy: 'bg-secondary', secondary: 'bg-secondary',
  indigo: 'bg-secondary', violet: 'bg-secondary', cyan: 'bg-info', info: 'bg-info',
  success: 'bg-success', warning: 'bg-warning', danger: 'bg-danger',
};

type StatTrend = keyof typeof trendIcons;
type StatColor = keyof typeof iconBgColors;

interface StatCardProps {
  title: ReactNode;
  value: ReactNode;
  change?: ReactNode;
  trend?: StatTrend;
  icon: LucideIcon;
  color?: StatColor;
  suffix?: ReactNode;
  delay?: number;
  tone?: 'default' | 'featured';
  /** Makes the whole card one button (stretched over the card); the title becomes its label. */
  onSelect?: () => void;
  /** Pressed state for a card that acts as a toggle. Only meaningful together with onSelect. */
  selected?: boolean;
}

const StatCard = ({
  title,
  value,
  change,
  trend = 'up',
  icon: Icon,
  color = 'primary',
  suffix,
  delay = 0,
  tone = 'default',
  onSelect,
  selected = false,
}: StatCardProps) => {
  const TrendIcon = trendIcons[trend];
  const featured = tone === 'featured';

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, delay }}
      className={cn(
        'relative overflow-hidden rounded-2xl border p-5 flex flex-col justify-between transition-shadow duration-300 group hover:shadow-elevated',
        featured
          ? 'bg-secondary border-secondary-700 text-white shadow-elevated'
          : 'bg-white border-slate-200 shadow-card',
        onSelect && 'cursor-pointer has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-primary/40',
        selected && 'border-primary ring-2 ring-primary/30',
      )}
    >
      <span aria-hidden="true" className={cn('absolute inset-x-0 top-0 h-[3px]', featured ? 'bg-primary' : accentBars[color])} />
      <div className="flex justify-between items-start mb-4">
        {onSelect ? (
          <button
            type="button"
            aria-pressed={selected}
            onClick={onSelect}
            className={cn('text-left text-overline uppercase outline-none after:absolute after:inset-0', featured ? 'text-white/75' : 'text-slate-600')}
          >
            {title}
          </button>
        ) : (
          <span className={cn('text-overline uppercase', featured ? 'text-white/75' : 'text-slate-600')}>{title}</span>
        )}
        <div className={cn('w-9 h-9 rounded-lg flex items-center justify-center', featured ? 'bg-white/10 text-white' : iconBgColors[color])}>
          <Icon className="w-[18px] h-[18px]" />
        </div>
      </div>
      <div>
        <div className="flex items-baseline gap-1.5">
          <span className={cn('text-display', featured ? 'text-white' : 'text-secondary')}>{value}</span>
          {suffix && <span className={cn('text-body font-medium', featured ? 'text-white/70' : 'text-slate-500')}>{suffix}</span>}
        </div>
        {change && (
          <div className={cn('flex items-center gap-1 mt-1.5 text-caption', featured ? 'text-white/80' : trendColors[trend])}>
            <TrendIcon className="w-3.5 h-3.5" />
            <span>{change}</span>
          </div>
        )}
      </div>
    </motion.div>
  );
};

export default StatCard;
