import type { ReactNode } from 'react';
import { cn } from '../../utils/cn';

// Semantic tones shared by every module. Status text is always rendered, so color
// is never the only signal.
export const badgeTones = {
  success: 'bg-success-50 text-success-dark border-success-light',
  warning: 'bg-warning-50 text-warning-dark border-warning-light',
  danger: 'bg-danger-50 text-danger-dark border-danger-light',
  info: 'bg-info-50 text-info-dark border-info-light',
  neutral: 'bg-slate-100 text-slate-600 border-slate-200',
  brand: 'bg-primary-50 text-primary-800 border-primary-200',
} as const;

export type BadgeTone = keyof typeof badgeTones;

const statusColors: Record<string, string> = {
  ...badgeTones,
  Draft: badgeTones.neutral,
  Submitted: badgeTones.brand,
  Reviewed: badgeTones.info,
  Improving: badgeTones.warning,
  Approved: badgeTones.success,
  Overdue: badgeTones.danger,
  Active: badgeTones.success,
  Inactive: badgeTones.neutral,
  'On Track': badgeTones.success,
  'At Risk': badgeTones.warning,
  Review: badgeTones.brand,
  Completed: badgeTones.neutral,
  Pending: badgeTones.warning,
  Rejected: badgeTones.danger,
  Published: badgeTones.info,
  Potential: badgeTones.brand,
  'Internally Funded': badgeTones.success,
  'Externally Funded': badgeTones.info,
  Awarded: badgeTones.warning,
};

type BadgeSize = 'xs' | 'sm' | 'md';

interface BadgeProps {
  children: ReactNode;
  variant?: string;
  className?: string;
  dot?: boolean;
  size?: BadgeSize;
}

const Badge = ({ children, variant = 'Draft', className, dot = false, size = 'sm' }: BadgeProps) => {
  const sizeClasses: Record<BadgeSize, string> = {
    xs: 'text-[10px] px-1.5 py-0.5',
    sm: 'text-caption font-semibold px-2 py-0.5',
    md: 'text-caption px-2.5 py-1',
  };

  return (
    <span className={cn(
      'inline-flex items-center gap-1 font-semibold rounded-md border',
      statusColors[variant] || statusColors.Draft,
      sizeClasses[size],
      className
    )}>
      {dot && <span className={cn('w-1.5 h-1.5 rounded-full', {
        'bg-slate-400': statusColors[variant] === badgeTones.neutral || !statusColors[variant],
        'bg-primary': statusColors[variant] === badgeTones.brand,
        'bg-info': statusColors[variant] === badgeTones.info,
        'bg-warning': statusColors[variant] === badgeTones.warning,
        'bg-success': statusColors[variant] === badgeTones.success,
        'bg-danger': statusColors[variant] === badgeTones.danger,
      })} />}
      {children}
    </span>
  );
};

export default Badge;
