import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '../../utils/cn';
import { motion } from 'framer-motion';
import { Link } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import Button from './Button';
import type { ButtonSize, ButtonVariant } from './Button';

/**
 * PageHeader — Reusable premium page header
 * 
 * Props:
 * - title: string (required)
 * - subtitle: string
 * - breadcrumb: string[] (e.g. ['Admin', 'Users'])
 * - action: { label, onClick, variant?, icon? }
 * - actions: array of action objects (multiple CTAs)
 * - badge: { text, variant? }
 * - className: string
 */
interface PageAction {
  label: ReactNode;
  onClick: () => void;
  variant?: ButtonVariant;
  size?: ButtonSize;
  icon?: LucideIcon;
  iconRight?: LucideIcon;
  className?: string;
}

/** A breadcrumb entry; entries with `to` (except the last, which is the current page) are links. */
export type BreadcrumbItem = string | { label: string; to?: string };

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  breadcrumb?: BreadcrumbItem[];
  action?: PageAction;
  actions?: PageAction[];
  badge?: { text: string; className?: string };
  className?: string;
  children?: ReactNode;
}

const PageHeader = ({
  title,
  subtitle,
  breadcrumb,
  action,
  actions,
  badge,
  className,
  children,
}: PageHeaderProps) => {
  return (
    <motion.div
      initial={{ opacity: 0, y: -8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
      className={cn('mb-6 sm:mb-8', className)}
    >
      {/* Breadcrumb */}
      {breadcrumb && breadcrumb.length > 0 && (
        <nav className="flex items-center gap-1 mb-2" aria-label="Breadcrumb">
          {breadcrumb.map((crumb, i) => {
            const label = typeof crumb === 'string' ? crumb : crumb.label;
            const to = typeof crumb === 'string' ? undefined : crumb.to;
            const isLast = i === breadcrumb.length - 1;
            const className = cn(
              'text-overline uppercase',
              isLast
                ? 'text-primary-700 dark:text-primary-300'
                : 'text-slate-500 hover:text-slate-700 cursor-pointer transition-colors dark:text-slate-400 dark:hover:text-slate-200'
            );
            return (
              <span key={`${label}-${i}`} className="flex items-center gap-1">
                {i > 0 && <ChevronRight className="w-3.5 h-3.5 text-slate-400" aria-hidden="true" />}
                {to && !isLast ? (
                  <Link to={to} className={cn(className, 'rounded hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600')}>{label}</Link>
                ) : (
                  <span className={className} aria-current={isLast ? 'page' : undefined}>{label}</span>
                )}
              </span>
            );
          })}
        </nav>
      )}

      {/* Title row */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 sm:gap-4">
        <div className="flex items-start gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h1 className="text-heading sm:text-display text-secondary tracking-tight dark:text-white">{title}</h1>
              {badge && (
                <span className={cn(
                  'inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border',
                  badge.className || 'bg-primary-50 text-primary-800 border-primary-200'
                )}>
                  {badge.text}
                </span>
              )}
            </div>
            {subtitle && (
              <p className="text-slate-600 mt-1 text-body leading-relaxed dark:text-slate-300">{subtitle}</p>
            )}
          </div>
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2 shrink-0">
          {children}
          {actions && actions.map((act, i) => (
            <Button
              key={i}
              variant={act.variant || 'outline'}
              size={act.size || 'md'}
              icon={act.icon}
              iconRight={act.iconRight}
              onClick={act.onClick}
              className={act.className}
            >
              {act.label}
            </Button>
          ))}
          {action && (
            <Button
              variant={action.variant || 'primary'}
              size={action.size || 'md'}
              icon={action.icon}
              iconRight={action.iconRight}
              onClick={action.onClick}
              className={action.className}
            >
              {action.label}
            </Button>
          )}
        </div>
      </div>
    </motion.div>
  );
};

export default PageHeader;
