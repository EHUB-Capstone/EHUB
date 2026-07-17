import type { ReactNode } from 'react';
import { cn } from '../../utils/cn';

const statusColors = {
  Draft: 'bg-slate-100 text-slate-600 border-slate-200',
  Submitted: 'bg-primary-50 text-primary border-primary-100',
  Reviewed: 'bg-cyan-50 text-cyan-700 border-cyan-100',
  Improving: 'bg-warning-50 text-warning-dark border-warning-light',
  Approved: 'bg-success-50 text-success-dark border-success-light',
  Overdue: 'bg-danger-50 text-danger border-danger-light',
  Active: 'bg-success-50 text-success-dark border-success-light',
  Inactive: 'bg-slate-100 text-slate-500 border-slate-200',
  'On Track': 'bg-success-50 text-success-dark border-success-light',
  'At Risk': 'bg-warning-50 text-warning-dark border-warning-light',
  Review: 'bg-primary-50 text-primary border-primary-100',
  Completed: 'bg-slate-100 text-slate-600 border-slate-200',
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
    sm: 'text-[11px] px-2 py-0.5',
    md: 'text-caption px-2.5 py-1',
  };

  return (
    <span className={cn(
      'inline-flex items-center gap-1 font-semibold rounded-full border',
      statusColors[variant as keyof typeof statusColors] || statusColors.Draft,
      sizeClasses[size],
      className
    )}>
      {dot && <span className={cn('w-1.5 h-1.5 rounded-full', {
        'bg-slate-400': variant === 'Draft' || variant === 'Inactive' || variant === 'Completed',
        'bg-primary': variant === 'Submitted' || variant === 'Review',
        'bg-cyan-500': variant === 'Reviewed',
        'bg-warning': variant === 'Improving' || variant === 'At Risk',
        'bg-success': variant === 'Approved' || variant === 'Active' || variant === 'On Track',
        'bg-danger': variant === 'Overdue',
      })} />}
      {children}
    </span>
  );
};

export default Badge;
