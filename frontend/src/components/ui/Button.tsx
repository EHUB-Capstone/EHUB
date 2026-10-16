import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '../../utils/cn';
import { Loader2 } from 'lucide-react';

const variants = {
  // Brand orange by product decision; white on it is 3.0:1 (below AA for small text).
  primary: 'bg-primary text-white hover:bg-primary-600 active:bg-primary-700 shadow-sm',
  secondary: 'bg-secondary text-white hover:bg-secondary-700 shadow-sm dark:bg-secondary-500 dark:hover:bg-secondary-400',
  outline: 'border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 hover:border-slate-400',
  ghost: 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
  danger: 'bg-danger-dark text-white hover:bg-red-800 shadow-sm',
  'danger-outline': 'border border-red-300 bg-white text-danger-dark hover:bg-danger-50',
  // Kept so existing call sites keep working; it renders identically to primary.
  gradient: 'bg-primary text-white hover:bg-primary-600 active:bg-primary-700 shadow-sm',
  'ghost-primary': 'text-primary-700 hover:bg-primary-50',
};

const sizes = {
  xs: 'h-7 px-2.5 text-caption gap-1',
  sm: 'h-8 px-3 text-body-sm gap-1.5',
  md: 'h-9 px-4 text-body gap-2',
  lg: 'h-11 px-6 text-body-lg gap-2',
  xl: 'h-12 px-8 text-body-lg gap-2.5',
};

export type ButtonVariant = keyof typeof variants;
export type ButtonSize = keyof typeof sizes;

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  isLoading?: boolean;
  icon?: LucideIcon;
  iconRight?: LucideIcon;
  children?: ReactNode;
}

const Button = forwardRef<HTMLButtonElement, ButtonProps>(({
  className,
  variant = 'primary',
  size = 'md',
  isLoading = false,
  children,
  disabled,
  type = 'button',
  icon: Icon,
  iconRight: IconRight,
  ...props
}, ref) => {
  return (
    <button
      ref={ref}
      type={type}
      disabled={disabled || isLoading}
      className={cn(
        'inline-flex items-center justify-center font-medium rounded-lg transition-colors duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 focus-visible:ring-offset-2 disabled:opacity-50 disabled:pointer-events-none',
        variants[variant],
        sizes[size],
        className
      )}
      {...props}>
      {isLoading && <Loader2 className="h-4 w-4 animate-spin" />}
      {!isLoading && Icon && <Icon className="h-4 w-4" />}
      {children}
      {IconRight && <IconRight className="h-4 w-4" />}
    </button>
  );
});

Button.displayName = 'Button';
export default Button;
