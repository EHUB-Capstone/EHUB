import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '../../utils/cn';
import { motion } from 'framer-motion';
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

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  breadcrumb?: string[];
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
          {breadcrumb.map((crumb, i) => (
            <span key={crumb} className="flex items-center gap-1">
              {i > 0 && <ChevronRight className="w-3.5 h-3.5 text-slate-300" />}
              <span className={cn(
                'text-caption font-medium',
                i === breadcrumb.length - 1
                  ? 'text-slate-600'
                  : 'text-slate-400 hover:text-slate-600 cursor-pointer transition-colors'
              )}>
                {crumb}
              </span>
            </span>
          ))}
        </nav>
      )}

      {/* Title row */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 sm:gap-4">
        <div className="flex items-start gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h1 className="text-xl sm:text-2xl font-bold text-slate-900 tracking-tight">{title}</h1>
              {badge && (
                <span className={cn(
                  'inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border',
                  badge.className || 'bg-primary-50 text-primary border-primary-100'
                )}>
                  {badge.text}
                </span>
              )}
            </div>
            {subtitle && (
              <p className="text-slate-500 mt-1 text-sm leading-relaxed">{subtitle}</p>
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
              variant={action.variant || 'gradient'}
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
