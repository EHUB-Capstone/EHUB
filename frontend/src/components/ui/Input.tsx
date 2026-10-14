import { forwardRef, useId, type InputHTMLAttributes } from 'react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '../../utils/cn';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  icon?: LucideIcon;
}

const Input = forwardRef<HTMLInputElement, InputProps>(({ className, label, error, icon: Icon, id, ...props }, ref) => {
  const generatedId = useId();
  const inputId = id || generatedId;

  return (
    <div className="w-full">
      {label && (
        <label htmlFor={inputId} className="block text-body-sm font-semibold text-secondary mb-1.5">
          {label}
        </label>
      )}
      <div className="relative">
        {Icon && (
          <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
            <Icon className="h-4 w-4 text-slate-500" />
          </div>
        )}
        <input
          id={inputId}
          ref={ref}
          className={cn(
            'flex h-10 w-full rounded-lg border border-slate-300 bg-white px-3.5 py-2 text-body text-slate-900 placeholder:text-slate-500 hover:border-slate-400',
            'focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary-600 transition-colors duration-200',
            'disabled:cursor-not-allowed disabled:opacity-60 disabled:bg-slate-100 read-only:bg-slate-50',
            Icon && 'pl-10',
            error && 'border-danger focus:ring-danger/25 focus:border-danger',
            className
          )}
          {...props}
        />
      </div>
      {error && (
        <p role="alert" className="mt-1.5 text-caption text-danger-dark">{error}</p>
      )}
    </div>
  );
});

Input.displayName = 'Input';
export default Input;
