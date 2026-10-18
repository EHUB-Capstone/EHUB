import { useId, useMemo, useRef, useState } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import { filterSearchOptions, moveActiveIndex, type SearchSelectOption } from '../../utils/mentorSearchSelect';

interface MentorSearchSelectProps {
  id: string;
  options: SearchSelectOption[];
  /** The chosen option id; an empty string means nothing is chosen. */
  value: string;
  onChange: (id: string) => void;
  /** When set, a first option with this text lets the user choose "no mentor" (value is then an empty string). */
  emptyLabel?: string;
  placeholder?: string;
  disabled?: boolean;
}

/** A select that can also be typed into: type part of a name to narrow the list, then pick with the mouse or Enter. */
export default function MentorSearchSelect({ id, options, value, onChange, emptyLabel, placeholder = 'Search or choose a mentor...', disabled = false }: MentorSearchSelectProps) {
  const listId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(-1);

  const items = useMemo<SearchSelectOption[]>(() => {
    const matches = filterSearchOptions(options, query);
    return emptyLabel ? [{ id: '', label: emptyLabel }, ...matches] : matches;
  }, [options, query, emptyLabel]);

  const selected = value === '' ? null : options.find(option => option.id === value) ?? null;
  const shown = open ? query : selected?.label ?? (value === '' && emptyLabel ? emptyLabel : '');

  const choose = (option: SearchSelectOption) => {
    onChange(option.id);
    setOpen(false);
    setQuery('');
    setActive(-1);
  };

  const onKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!open) setOpen(true);
      setActive(current => moveActiveIndex(current, event.key === 'ArrowDown' ? 1 : -1, items.length));
    } else if (event.key === 'Enter' && open && active >= 0 && items[active]) {
      event.preventDefault();
      choose(items[active]);
    } else if (event.key === 'Escape' && open) {
      // Closes the list only; the surrounding dialog keeps its own Escape handling for the next press.
      event.stopPropagation();
      setOpen(false);
      setQuery('');
    }
  };

  return (
    <div className="relative">
      <input
        ref={inputRef}
        id={id}
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && active >= 0 ? `${listId}-${active}` : undefined}
        autoComplete="off"
        disabled={disabled}
        value={shown}
        placeholder={placeholder}
        onFocus={() => setOpen(true)}
        onBlur={() => { setOpen(false); setQuery(''); }}
        onChange={event => { setQuery(event.target.value); setOpen(true); setActive(0 + (emptyLabel ? 1 : 0)); }}
        onKeyDown={onKeyDown}
        className="w-full rounded-lg border border-slate-200 bg-white py-1.5 pl-2 pr-7 text-xs text-slate-700 outline-none focus:border-primary disabled:opacity-60"
      />
      <ChevronDown className="pointer-events-none absolute right-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" aria-hidden="true" />
      {open && (
        <ul id={listId} role="listbox" className="absolute z-20 mt-1 max-h-56 w-full overflow-auto rounded-lg border border-slate-200 bg-white py-1 shadow-lg">
          {items.length === 0 || (items.length === 1 && emptyLabel && query.trim()) ? (
            <li role="presentation" className="px-3 py-2 text-xs text-slate-400">No mentor matches "{query.trim()}".</li>
          ) : null}
          {items.map((option, index) => {
            const isSelected = option.id === value;
            return (
              <li
                key={option.id || '__empty'}
                id={`${listId}-${index}`}
                role="option"
                aria-selected={isSelected}
                // mouseDown (not click) so the input does not lose focus and close the list before the choice registers.
                onMouseDown={event => { event.preventDefault(); choose(option); }}
                onMouseEnter={() => setActive(index)}
                className={`flex cursor-pointer items-center justify-between gap-2 px-3 py-1.5 text-xs ${index === active ? 'bg-primary-50 text-primary-800' : 'text-slate-700'} ${option.id === '' ? 'italic text-slate-500' : ''}`}
              >
                <span className="min-w-0 truncate">
                  <span className="font-medium">{option.label}</span>
                  {option.detail && <span className="ml-1.5 text-slate-400">{option.detail}</span>}
                </span>
                {isSelected && <Check className="h-3.5 w-3.5 shrink-0 text-primary" aria-hidden="true" />}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
