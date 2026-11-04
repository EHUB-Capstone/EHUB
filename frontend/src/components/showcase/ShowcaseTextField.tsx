import { useId } from 'react';
import CharacterCounter from '../ui/CharacterCounter';
import { cn } from '../../utils/cn';

interface ShowcaseTextFieldProps {
  label: string;
  value: string;
  max: number;
  onChange: (value: string) => void;
  error?: string;
  hint?: string;
  rows?: number;
  required?: boolean;
}

/** Multi-line field with a visible label, character counter and inline error. */
export default function ShowcaseTextField({ label, value, max, onChange, error, hint, rows = 4, required }: ShowcaseTextFieldProps) {
  const id = useId();
  const describedBy = [hint ? `${id}-hint` : '', error ? `${id}-error` : ''].filter(Boolean).join(' ') || undefined;

  return (
    <div>
      <div className="mb-1.5 flex items-baseline justify-between gap-3">
        <label htmlFor={id} className="text-body-sm font-semibold text-secondary">
          {label}{required && <span className="text-danger-dark" aria-hidden="true"> *</span>}
        </label>
        <CharacterCounter value={value} max={max} />
      </div>
      {hint && <p id={`${id}-hint`} className="mb-1.5 text-caption text-slate-600">{hint}</p>}
      <textarea
        id={id}
        value={value}
        rows={rows}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={Boolean(error)}
        aria-describedby={describedBy}
        className={cn(
          'w-full resize-y rounded-lg border border-slate-300 bg-white px-3.5 py-2 text-body text-slate-900 placeholder:text-slate-500 hover:border-slate-400',
          'focus:border-primary-600 focus:outline-none focus:ring-2 focus:ring-primary/30 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:opacity-60',
          error && 'border-danger focus:border-danger focus:ring-danger/25',
        )}
      />
      {error && <p id={`${id}-error`} role="alert" className="mt-1.5 text-caption text-danger-dark">{error}</p>}
    </div>
  );
}
