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
  up: 'text-success',
  down: 'text-danger',
  flat: 'text-slate-400',
};

const iconBgColors = {
  primary: 'bg-primary-50 text-primary',
  secondary: 'bg-secondary-50 text-secondary',
  cyan: 'bg-cyan-50 text-cyan-600',
  success: 'bg-success-50 text-success',
  warning: 'bg-warning-50 text-warning-dark',
  danger: 'bg-danger-50 text-danger',
  indigo: 'bg-indigo-50 text-indigo-600',
  violet: 'bg-violet-50 text-violet-600',
  orange: 'bg-orange-50 text-orange-600',
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
}: StatCardProps) => {
  const TrendIcon = trendIcons[trend];

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, delay }}
      className="bg-white rounded-2xl border border-slate-200/60 shadow-card p-5 flex flex-col justify-between hover:shadow-elevated transition-shadow duration-300 group"
    >
      <div className="flex justify-between items-start mb-4">
        <span className="text-overline text-slate-500 uppercase">{title}</span>
        <div className={cn('w-9 h-9 rounded-xl flex items-center justify-center transition-transform group-hover:scale-110', iconBgColors[color])}>
          <Icon className="w-[18px] h-[18px]" />
        </div>
      </div>
      <div>
        <div className="flex items-baseline gap-1.5">
          <span className="text-display text-slate-900">{value}</span>
          {suffix && <span className="text-body text-slate-400 font-medium">{suffix}</span>}
        </div>
        {change && (
          <div className={cn('flex items-center gap-1 mt-1.5 text-caption', trendColors[trend])}>
            <TrendIcon className="w-3.5 h-3.5" />
            <span>{change}</span>
          </div>
        )}
      </div>
    </motion.div>
  );
};

export default StatCard;
