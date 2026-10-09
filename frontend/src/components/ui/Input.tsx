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
        <label htmlFor={inputId} className="block text-caption font-medium text-slate-600 mb-1.5 uppercase tracking-wider">
          {label}
        </label>
      )}
      <div className="relative">
        {Icon && (
          <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
            <Icon className="h-4 w-4 text-slate-400" />
          </div>
        )}
        <input
          id={inputId}
          ref={ref}
          className={cn(
            'flex h-10 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-body text-slate-900 placeholder:text-slate-400',
            'focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all duration-200',
            'disabled:cursor-not-allowed disabled:opacity-50 disabled:bg-slate-50',
            Icon && 'pl-10',
            error && 'border-danger focus:ring-danger/20 focus:border-danger',
            className
          )}
          {...props}
        />
      </div>
      {error && (
        <p className="mt-1.5 text-caption text-danger">{error}</p>
      )}
    </div>
  );
});

Input.displayName = 'Input';
export default Input;
