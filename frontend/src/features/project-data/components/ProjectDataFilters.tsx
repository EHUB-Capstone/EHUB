import { useEffect, useId, useMemo, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { Check, ChevronDown, Search, SlidersHorizontal, X } from 'lucide-react';
import Button from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';
import { PROJECT_DATA_SEMESTER_TERMS } from '../../../types/projectData';
import {
  FILTER_LABELS,
  PROJECT_DATA_SEARCH_MAX_LENGTH,
  activeFilterKeys,
  hasActiveSearchOrFilters,
} from '../../../utils/projectData';
import { majorLabel } from '../../../utils/projectDistribution';
import type { ProjectDataFilterOptions, ProjectDataQuery } from '../../../types/projectData';

interface SelectOption {
  value: string;
  label: string;
}

interface SelectFieldProps {
  label: string;
  value: string;
  options: SelectOption[];
  onChange: (value: string) => void;
  disabled?: boolean;
  /** Renders the select on its own (accessible name from `label`) with `allLabel` as the empty choice. */
  compact?: boolean;
  allLabel?: string;
}

function SelectField({ label, value, options, onChange, disabled, compact = false, allLabel = 'All' }: SelectFieldProps) {
  const id = useId();
  // A value coming from a shared URL must stay visible even when it is not among the loaded options.
  const items = value && !options.some(option => option.value === value)
    ? [{ value, label: value }, ...options]
    : options;

  if (compact) {
    return (
      <select
        value={value}
        disabled={disabled}
        aria-label={label}
        onChange={event => onChange(event.target.value)}
        className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400"
      >
        <option value="">{allLabel}</option>
        {items.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    );
  }

  return (
    <div className="min-w-0">
      <label htmlFor={id} className="mb-1 block text-xs font-medium text-slate-500">{label}</label>
      <ListboxSelect id={id} value={value} items={items} disabled={disabled} allLabel={allLabel} onChange={onChange} />
    </div>
  );
}

interface ListboxSelectProps {
  id: string;
  value: string;
  items: SelectOption[];
  disabled?: boolean;
  allLabel: string;
  onChange: (value: string) => void;
}

/**
 * Replaces the native select for long option lists: the browser flips a native popup upwards
 * (covering the page) when it is taller than the space below, so this list always opens downwards
 * and scrolls inside a capped height.
 */
function ListboxSelect({ id, value, items, disabled, allLabel, onChange }: ListboxSelectProps) {
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const container = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const list = useRef<HTMLDivElement>(null);
  const listId = `${id}-list`;
  const entries = useMemo(() => [{ value: '', label: allLabel }, ...items], [allLabel, items]);
  const selectedLabel = entries.find(entry => entry.value === value)?.label ?? allLabel;

  useEffect(() => {
    if (!open) return undefined;
    const onPointer = (event: MouseEvent) => {
      if (!container.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onPointer);
    return () => document.removeEventListener('mousedown', onPointer);
  }, [open]);

  useEffect(() => {
    if (open) list.current?.focus();
  }, [open]);

  useEffect(() => {
    if (open) document.getElementById(`${listId}-${activeIndex}`)?.scrollIntoView({ block: 'nearest' });
  }, [open, activeIndex, listId]);

  const openList = () => {
    setActiveIndex(Math.max(0, entries.findIndex(entry => entry.value === value)));
    setOpen(true);
  };

  const close = () => {
    setOpen(false);
    trigger.current?.focus();
  };

  const choose = (index: number) => {
    const entry = entries[index];
    if (entry && entry.value !== value) onChange(entry.value);
    close();
  };

  const onListKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    switch (event.key) {
      case 'ArrowDown': setActiveIndex(index => Math.min(entries.length - 1, index + 1)); break;
      case 'ArrowUp': setActiveIndex(index => Math.max(0, index - 1)); break;
      case 'Home': setActiveIndex(0); break;
      case 'End': setActiveIndex(entries.length - 1); break;
      case 'Enter':
      case ' ': choose(activeIndex); break;
      case 'Escape': close(); break;
      case 'Tab': setOpen(false); return;
      default: return;
    }
    event.preventDefault();
  };

  return (
    <div ref={container} className="relative">
      <button
        ref={trigger}
        id={id}
        type="button"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        onClick={() => (open ? setOpen(false) : openList())}
        onKeyDown={event => {
          if (!open && (event.key === 'ArrowDown' || event.key === 'ArrowUp')) {
            event.preventDefault();
            openList();
          }
        }}
        className="flex h-10 w-full items-center justify-between gap-2 rounded-xl border border-slate-200 bg-white px-3 text-left text-sm text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400"
      >
        <span className="truncate">{selectedLabel}</span>
        <ChevronDown className={cn('h-4 w-4 shrink-0 text-slate-400 transition-transform', open && 'rotate-180')} aria-hidden="true" />
      </button>
      {open && (
        <div
          ref={list}
          id={listId}
          role="listbox"
          tabIndex={-1}
          aria-activedescendant={`${listId}-${activeIndex}`}
          onKeyDown={onListKeyDown}
          className="absolute left-0 top-full z-30 mt-1 max-h-64 min-w-full overflow-y-auto rounded-xl border border-slate-200 bg-white p-1 shadow-lg outline-none"
        >
          {entries.map((entry, index) => {
            const selected = entry.value === value;
            return (
              <div
                key={entry.value || '__all'}
                id={`${listId}-${index}`}
                role="option"
                aria-selected={selected}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => choose(index)}
                className={cn(
                  'flex cursor-pointer items-center justify-between gap-2 rounded-lg px-2.5 py-1.5 text-sm text-slate-700',
                  index === activeIndex && 'bg-slate-100',
                  selected && 'font-semibold text-primary-700',
                )}
              >
                <span className="truncate">{entry.label}</span>
                {selected && <Check className="h-4 w-4 shrink-0" aria-hidden="true" />}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

interface ProjectDataFiltersProps {
  query: ProjectDataQuery;
  searchInput: string;
  onSearchInputChange: (value: string) => void;
  onChange: (patch: Partial<ProjectDataQuery>) => void;
  onClear: () => void;
  options: ProjectDataFilterOptions | undefined;
  optionsLoading: boolean;
  optionsError: boolean;
  onRetryOptions: () => void;
}

export default function ProjectDataFilters({
  query,
  searchInput,
  onSearchInputChange,
  onChange,
  onClear,
  options,
  optionsLoading,
  optionsError,
  onRetryOptions,
}: ProjectDataFiltersProps) {
  const searchId = useId();
  const advancedId = useId();
  const [expanded, setExpanded] = useState(false);
  const activeKeys = activeFilterKeys(query);
  const advancedActive = activeKeys.some(key => !['subjectCode', 'semester', 'year'].includes(key));
  const showAdvanced = expanded || advancedActive;
  const disabled = optionsLoading || !options;

  const choices = useMemo(() => {
    const mentorsByUser = new Map<string, { name: string; slots: string[] }>();
    for (const mentor of options?.mentors ?? []) {
      const entry = mentorsByUser.get(mentor.userId) ?? { name: mentor.fullName, slots: [] };
      if (!entry.slots.includes(mentor.slot)) entry.slots.push(mentor.slot);
      mentorsByUser.set(mentor.userId, entry);
    }
    return {
      subjects: (options?.subjects ?? []).map(item => ({ value: item.code, label: item.code })),
      terms: PROJECT_DATA_SEMESTER_TERMS.map(code => ({ value: code, label: code })),
      years: (options?.years ?? []).map(year => ({ value: String(year), label: String(year) })),
      groups: (options?.groups ?? []).map(value => ({ value, label: value })),
      industries: (options?.startupIndustries ?? []).map(value => ({ value, label: value })),
      majors: (options?.majors ?? []).map(value => ({ value, label: majorLabel(value) })),
      lecturers: (options?.lecturers ?? []).map(item => ({ value: item.userId, label: item.fullName })),
      mentors: [...mentorsByUser.entries()].map(([value, entry]) => ({ value, label: `${entry.name} (${entry.slots.join(', ')})` })),
      achievements: (options?.achievements ?? []).map(value => ({ value, label: value })),
    };
  }, [options]);

  const valueLabel = (key: (typeof activeKeys)[number]): string => {
    const lookup: Record<string, SelectOption[]> = {
      subjectCode: choices.subjects,
      semester: choices.terms,
      year: choices.years,
      group: choices.groups,
      startupIndustry: choices.industries,
      major: choices.majors,
      lecturerId: choices.lecturers,
      mentorId: choices.mentors,
      achievement: choices.achievements,
    };
    const value = query[key];
    return lookup[key]?.find(option => option.value === value)?.label ?? value;
  };

  return (
    <section aria-label="Search and filters" className="mb-4 rounded-2xl border border-slate-200/60 bg-white p-4 shadow-sm">
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative min-w-[180px] flex-1">
          <label htmlFor={searchId} className="sr-only">Search projects</label>
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden="true" />
          <input
            id={searchId}
            type="search"
            value={searchInput}
            maxLength={PROJECT_DATA_SEARCH_MAX_LENGTH}
            onChange={event => onSearchInputChange(event.target.value)}
            placeholder="Search projects, classes, people…"
            title="Searches project, description, class, subject, semester, lecturer, mentor and industry"
            className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-9 pr-3 text-sm text-slate-700 outline-none placeholder:text-slate-400 focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
        </div>
        <SelectField compact label="Filter by subject" allLabel="All Subjects" value={query.subjectCode} options={choices.subjects} disabled={disabled} onChange={value => onChange({ subjectCode: value })} />
        <SelectField compact label="Filter by semester" allLabel="All Semesters" value={query.semester} options={choices.terms} onChange={value => onChange({ semester: value as ProjectDataQuery['semester'] })} />
        <SelectField compact label="Filter by year" allLabel="All Years" value={query.year} options={choices.years} disabled={disabled} onChange={value => onChange({ year: value })} />
        <Button
          variant="outline"
          icon={SlidersHorizontal}
          aria-expanded={showAdvanced}
          aria-controls={advancedId}
          onClick={() => setExpanded(current => !current)}
          className="shrink-0"
        >
          More filters
        </Button>
      </div>

      <div id={advancedId} hidden={!showAdvanced} className={cn('mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6', !showAdvanced && 'hidden')}>
        <SelectField label="Group" value={query.group} options={choices.groups} disabled={disabled} onChange={value => onChange({ group: value })} />
        <SelectField label="Startup industry" value={query.startupIndustry} options={choices.industries} disabled={disabled} onChange={value => onChange({ startupIndustry: value })} />
        <SelectField label="Major" value={query.major} options={choices.majors} disabled={disabled} onChange={value => onChange({ major: value })} />
        <SelectField label="Lecturer" value={query.lecturerId} options={choices.lecturers} disabled={disabled} onChange={value => onChange({ lecturerId: value })} />
        <SelectField label="Mentor" value={query.mentorId} options={choices.mentors} disabled={disabled} onChange={value => onChange({ mentorId: value })} />
        <SelectField label="Achievement" value={query.achievement} options={choices.achievements} disabled={disabled} onChange={value => onChange({ achievement: value as ProjectDataQuery['achievement'] })} />
      </div>

      {optionsError && (
        <p role="alert" className="mt-3 flex flex-wrap items-center gap-2 text-xs text-danger">
          Filter options could not be loaded.
          <button type="button" onClick={onRetryOptions} className="font-semibold underline underline-offset-2">Retry</button>
        </p>
      )}

      {hasActiveSearchOrFilters(query) && (
        <div className="mt-3 flex flex-wrap items-center gap-2" aria-label="Applied filters">
          {query.search && (
            <span className="inline-flex items-center gap-1 rounded-full border border-slate-200 bg-slate-50 py-0.5 pl-2.5 pr-1 text-xs text-slate-600">
              Search: “{query.search}”
              <button type="button" aria-label="Remove search" onClick={() => onChange({ search: '' })} className="rounded-full p-0.5 hover:bg-slate-200">
                <X className="h-3 w-3" />
              </button>
            </span>
          )}
          {activeKeys.map(key => (
            <span key={key} className="inline-flex items-center gap-1 rounded-full border border-primary-100 bg-primary-50 py-0.5 pl-2.5 pr-1 text-xs text-primary-700">
              {FILTER_LABELS[key]}: {valueLabel(key)}
              <button
                type="button"
                aria-label={`Remove ${FILTER_LABELS[key]} filter`}
                onClick={() => onChange({ [key]: '' } as Partial<ProjectDataQuery>)}
                className="rounded-full p-0.5 hover:bg-primary-100"
              >
                <X className="h-3 w-3" />
              </button>
            </span>
          ))}
          <Button variant="ghost" size="xs" onClick={onClear}>Clear filters</Button>
        </div>
      )}
    </section>
  );
}
