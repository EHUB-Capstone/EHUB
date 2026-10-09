import { useId, useState, type KeyboardEvent } from 'react';
import { X } from 'lucide-react';

interface TagInputProps {
  label: string;
  value: string[];
  onChange: (tags: string[]) => void;
  /** Validates and normalizes one new tag; returns the new list or the reason it was refused. */
  addTag: (current: readonly string[], raw: string) => { tags: string[]; error: string | null };
  placeholder?: string;
  hint?: string;
  /** Tags already used elsewhere; offered while typing so the same word is spelled the same way. */
  suggestions?: readonly string[];
  disabled?: boolean;
}

/** A field that turns typed words into removable chips. Enter or comma adds, Backspace on an empty box removes the last chip. */
export default function TagInput({ label, value, onChange, addTag, placeholder, hint, suggestions = [], disabled = false }: TagInputProps) {
  const id = useId();
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);

  const commit = (raw: string) => {
    if (raw.trim() === '') return;
    const result = addTag(value, raw);
    setError(result.error);
    if (!result.error) {
      onChange(result.tags);
      setDraft('');
    }
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      commit(draft);
    } else if (event.key === 'Backspace' && draft === '' && value.length > 0) {
      onChange(value.slice(0, -1));
    }
  };

  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-xs font-semibold text-slate-700">{label}</label>
      <div className={`flex min-h-[42px] flex-wrap items-center gap-1.5 rounded-xl border bg-white px-2.5 py-1.5 focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/20 ${error ? 'border-red-300' : 'border-slate-200'} ${disabled ? 'opacity-60' : ''}`}>
        {value.map(tag => (
          <span key={tag} className="inline-flex items-center gap-1 rounded-full border border-primary-100 bg-primary-50 px-2.5 py-0.5 text-xs font-semibold text-primary">
            {tag}
            {!disabled && (
              <button type="button" onClick={() => onChange(value.filter(item => item !== tag))} aria-label={`Remove ${tag}`} className="rounded-full p-0.5 hover:bg-primary-100">
                <X className="h-3 w-3" />
              </button>
            )}
          </span>
        ))}
        <input
          id={id}
          value={draft}
          disabled={disabled}
          onChange={event => { setDraft(event.target.value); if (error) setError(null); }}
          onKeyDown={onKeyDown}
          onBlur={() => commit(draft)}
          placeholder={value.length === 0 ? placeholder : undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={`${id}-help`}
          list={suggestions.length > 0 ? `${id}-suggestions` : undefined}
          className="min-w-[120px] flex-1 border-0 bg-transparent px-1 py-1 text-sm outline-none"
        />
      </div>
      {suggestions.length > 0 && (
        <datalist id={`${id}-suggestions`}>
          {suggestions.filter(item => !value.some(tag => tag.toLowerCase() === item.toLowerCase())).slice(0, 50).map(item => <option key={item} value={item} />)}
        </datalist>
      )}
      <p id={`${id}-help`} role={error ? 'alert' : undefined} className={`mt-1 text-[11px] ${error ? 'text-red-600' : 'text-slate-400'}`}>
        {error ?? hint ?? 'Press Enter or comma to add.'}
      </p>
    </div>
  );
}
