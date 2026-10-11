import { useEffect, useRef, useState } from 'react';
import { Check, ChevronDown, Tags, X } from 'lucide-react';
import { TAG_CATEGORIES, type TagOption } from '../../utils/mentorTags';

interface MentorTagFilterProps {
  options: TagOption[];
  /** Keys of the selected tags (see tagKey). A mentor matches when it has any of them. */
  selected: string[];
  onChange: (keys: string[]) => void;
  disabled?: boolean;
}

/** "Filter by tag" button with a grouped checklist (expertise, startup domain, technology, tag) and the chosen tags as chips. */
export default function MentorTagFilter({ options, selected, onChange, disabled = false }: MentorTagFilterProps) {
  const [open, setOpen] = useState(false);
  const container = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return undefined;
    const onPointer = (event: MouseEvent) => {
      if (!container.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.stopPropagation();
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', onPointer);
    document.addEventListener('keydown', onKey, true);
    return () => {
      document.removeEventListener('mousedown', onPointer);
      document.removeEventListener('keydown', onKey, true);
    };
  }, [open]);

  if (options.length === 0) return null;
  const chosen = options.filter(option => selected.includes(option.key));
  const toggle = (key: string) => onChange(selected.includes(key) ? selected.filter(item => item !== key) : [...selected, key]);

  return (
    <div ref={container} className="relative">
      <div className="flex flex-wrap items-center gap-1.5">
        <button
          type="button"
          disabled={disabled}
          aria-haspopup="listbox"
          aria-expanded={open}
          onClick={() => setOpen(current => !current)}
          className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-2.5 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-60"
        >
          <Tags className="h-3.5 w-3.5" /> Filter by tag
          {chosen.length > 0 && <span className="rounded-full bg-primary px-1.5 text-[10px] text-white">{chosen.length}</span>}
          <ChevronDown className="h-3.5 w-3.5" />
        </button>
        {chosen.map(option => (
          <span key={option.key} className="inline-flex items-center gap-1 rounded-full border border-primary-100 bg-primary-50 px-2 py-0.5 text-[11px] font-semibold text-primary">
            {option.label}
            <button type="button" onClick={() => toggle(option.key)} aria-label={`Remove tag filter ${option.label}`} className="rounded-full p-0.5 hover:bg-primary-100"><X className="h-3 w-3" /></button>
          </span>
        ))}
        {chosen.length > 1 && <button type="button" onClick={() => onChange([])} className="text-[11px] font-semibold text-primary hover:underline">Clear</button>}
      </div>

      {open && (
        <div role="listbox" aria-multiselectable="true" aria-label="Mentor tags" className="absolute left-0 z-30 mt-1.5 max-h-72 w-72 overflow-y-auto rounded-xl border border-slate-200 bg-white p-2 shadow-lg">
          <p className="px-2 pb-1 text-[11px] text-slate-400">Shows mentors with any of the selected tags.</p>
          {TAG_CATEGORIES.map(category => {
            const items = options.filter(option => option.category === category.key);
            if (items.length === 0) return null;
            return (
              <div key={category.key} className="pb-1">
                <p className="px-2 py-1 text-[10px] font-bold uppercase tracking-wider text-slate-400">{category.label}</p>
                {items.map(option => {
                  const checked = selected.includes(option.key);
                  return (
                    <button
                      key={option.key}
                      type="button"
                      role="option"
                      aria-selected={checked}
                      onClick={() => toggle(option.key)}
                      className="flex w-full items-center justify-between gap-2 rounded-lg px-2 py-1.5 text-left text-xs hover:bg-slate-50"
                    >
                      <span className="flex min-w-0 items-center gap-2">
                        <span className={`flex h-4 w-4 shrink-0 items-center justify-center rounded border ${checked ? 'border-primary bg-primary' : 'border-slate-300 bg-white'}`}>
                          {checked && <Check className="h-3 w-3 text-white" />}
                        </span>
                        <span className="truncate text-slate-700">{option.label}</span>
                      </span>
                      <span className="shrink-0 text-[10px] text-slate-400">{option.count}</span>
                    </button>
                  );
                })}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
