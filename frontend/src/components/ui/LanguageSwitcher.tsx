import { useEffect, useId, useRef, useState } from 'react';
import { Check, ChevronDown, Globe } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useLanguage } from '../../context/LanguageContext';
import { getLanguageOption } from '../../i18n/config';
import { cn } from '../../utils/cn';
import { LanguageFlag } from './flags';

interface LanguageSwitcherProps {
  // `solid` matches the dashboard header's theme button; `outline` matches the
  // bordered theme buttons on the public pages (Home, Login, Register).
  variant?: 'solid' | 'outline';
  // `lg` matches the 40px theme buttons on the auth pages.
  size?: 'md' | 'lg';
  className?: string;
}

const triggerStyles = {
  solid:
    'rounded-lg bg-slate-100 text-slate-600 hover:bg-slate-200 hover:text-slate-900',
  outline:
    'rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/5 text-slate-500 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-white/10 dark:hover:text-white',
} as const;

const sizeStyles = { md: 'h-9', lg: 'h-10 rounded-[14px]' } as const;

const LanguageSwitcher = ({ variant = 'solid', size = 'md', className }: LanguageSwitcherProps) => {
  const { t } = useTranslation();
  const { language, languages, setLanguage } = useLanguage();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const listId = useId();
  const current = getLanguageOption(language);

  useEffect(() => {
    if (!open) return undefined;
    const handlePointerDown = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

  const select = (code: (typeof languages)[number]['code']) => {
    setLanguage(code);
    setOpen(false);
    triggerRef.current?.focus();
  };

  return (
    <div className={cn('relative', className)} ref={containerRef}>
      <button
        ref={triggerRef}
        type="button"
        onClick={() => setOpen((value) => !value)}
        className={cn(
          'flex items-center gap-1.5 px-2.5 text-body-sm font-semibold transition-colors',
          triggerStyles[variant],
          sizeStyles[size],
        )}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        aria-label={`${t('language.switch')} (${t('language.current', { language: current.label })})`}
        title={t('language.switch')}
      >
        <Globe className="h-4 w-4" aria-hidden="true" />
        <span>{current.short}</span>
        <ChevronDown
          className={cn('h-3.5 w-3.5 text-slate-400 transition-transform duration-200', open && 'rotate-180')}
          aria-hidden="true"
        />
      </button>

      {open && (
        <ul
          id={listId}
          role="listbox"
          aria-label={t('language.label')}
          className="absolute right-0 top-full z-50 mt-2 w-44 origin-top-right animate-scale-in overflow-hidden rounded-xl border border-slate-100 bg-white py-1 shadow-float"
        >
          {languages.map((option) => {
            const active = option.code === language;
            return (
              <li key={option.code} role="presentation">
                <button
                  type="button"
                  role="option"
                  aria-selected={active}
                  onClick={() => select(option.code)}
                  className={cn(
                    'flex w-full items-center gap-2.5 px-3.5 py-2.5 text-left text-body-sm transition-colors hover:bg-slate-50',
                    active ? 'bg-slate-50 font-semibold text-primary-700' : 'text-slate-700',
                  )}
                >
                  <LanguageFlag code={option.code} />
                  <span className="flex-1">{option.label}</span>
                  <span className="text-caption font-semibold text-slate-400">{option.short}</span>
                  {active && <Check className="h-4 w-4 text-primary-700" aria-hidden="true" />}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
};

export default LanguageSwitcher;
