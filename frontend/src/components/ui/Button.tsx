import { forwardRef } from 'react';
import type { ButtonHTMLAttributes, ElementType, SVGProps } from 'react';
import { cn } from '../../utils/cn';
import { Loader2 } from 'lucide-react';

type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger' | 'gradient' | 'ghost-primary';
type ButtonSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl';

const variants: Record<ButtonVariant, string> = {
  primary: 'bg-primary text-white hover:bg-primary-dark shadow-sm hover:shadow-glow-primary',
  secondary: 'bg-slate-900 text-white hover:bg-slate-800 shadow-sm',
  outline: 'border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 hover:border-slate-400',
  ghost: 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
  danger: 'bg-danger text-white hover:bg-danger-dark shadow-sm',
  gradient: 'bg-gradient-primary text-white shadow-sm hover:shadow-glow-primary hover:opacity-95',
  'ghost-primary': 'text-primary hover:bg-primary-50',
};

const sizes: Record<ButtonSize, string> = {
  xs: 'h-7 px-2.5 text-[0.75rem] gap-1',
  sm: 'h-8 px-3 text-[0.8125rem] gap-1.5',
  md: 'h-9 px-4 text-[0.875rem] gap-2',
  lg: 'h-11 px-6 text-[1rem] gap-2',
  xl: 'h-12 px-8 text-[1rem] gap-2.5',
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  isLoading?: boolean;
  icon?: ElementType<SVGProps<SVGSVGElement>>;
  iconRight?: ElementType<SVGProps<SVGSVGElement>>;
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
        'inline-flex items-center justify-center font-medium rounded-xl transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:ring-offset-1 disabled:opacity-50 disabled:pointer-events-none active:scale-[0.98]',
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
