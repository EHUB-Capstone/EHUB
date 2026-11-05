import { useEffect, useId, useMemo, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { Check, ChevronDown, Search, SlidersHorizontal, X } from 'lucide-react';
import Button from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';
import { COMPETITION_STAGE_LABELS, PROJECT_COMPETITION_STAGES, PROJECT_DATA_SEMESTER_TERMS } from '../../../types/projectData';
import {
  FILTER_LABELS,
  PROJECT_DATA_SEARCH_MAX_LENGTH,
  activeFilterKeys,
  activeFilterValues,
  hasActiveSearchOrFilters,
  removeFilterValue,
  toggleFilterValue,
} from '../../../utils/projectData';
import type { FilterKey } from '../../../utils/projectData';
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
  allLabel: string;
}

/** A native single-choice select for the short top-row lists; its accessible name comes from `label`. */
function SelectField({ label, value, options, onChange, disabled, allLabel }: SelectFieldProps) {
  // A value coming from a shared URL must stay visible even when it is not among the loaded options.
  const items = value && !options.some(option => option.value === value)
    ? [{ value, label: value }, ...options]
    : options;

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

interface MultiSelectFieldProps {
  label: string;
  values: string[];
  options: SelectOption[];
  onChange: (values: string[]) => void;
  disabled?: boolean;
}

/** A labelled filter that accepts any number of values; no value means "All". */
function MultiSelectField({ label, values, options, onChange, disabled }: MultiSelectFieldProps) {
  const id = useId();
  // Values coming from a shared URL must stay visible even when they are not among the loaded options.
  const known = new Set(options.map(option => option.value));
  const extras = values.filter(value => !known.has(value)).map(value => ({ value, label: value }));
  const items = extras.length > 0 ? [...extras, ...options] : options;

  return (
    <div className="min-w-0">
      <label htmlFor={id} className="mb-1 block text-xs font-medium text-slate-500">{label}</label>
      <MultiListbox id={id} values={values} items={items} disabled={disabled} onChange={onChange} />
    </div>
  );
}

interface MultiListboxProps {
  id: string;
  values: string[];
  items: SelectOption[];
  disabled?: boolean;
  onChange: (values: string[]) => void;
}

/**
 * A dropdown with a check box per option, for picking several values. It stays open while values are toggled and
 * closes on Escape, Tab or a click outside. A native multiple select is avoided on purpose: it needs Ctrl-click to pick
 * more than one value, and the browser flips a native popup upwards (covering the page) when it is taller than the
 * space below. This list always opens downwards and scrolls inside a capped height. The first row, "All", clears the
 * selection.
 */
function MultiListbox({ id, values, items, disabled, onChange }: MultiListboxProps) {
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const container = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const list = useRef<HTMLDivElement>(null);
  const listId = `${id}-list`;
  const entries = useMemo(() => [{ value: '', label: 'All' }, ...items], [items]);
  const selectedLabels = values.map(value => items.find(item => item.value === value)?.label ?? value);
  const triggerLabel = selectedLabels.length === 0
    ? 'All'
    : selectedLabels.length === 1 ? selectedLabels[0] : `${selectedLabels[0]} +${selectedLabels.length - 1}`;

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
    const firstSelected = entries.findIndex(entry => entry.value !== '' && values.includes(entry.value));
    setActiveIndex(Math.max(0, firstSelected));
    setOpen(true);
  };

  const close = () => {
    setOpen(false);
    trigger.current?.focus();
  };

  const toggle = (index: number) => {
    const entry = entries[index];
    if (!entry) return;
    if (entry.value === '') {
      if (values.length > 0) onChange([]);
      return;
    }
    onChange(toggleFilterValue(values, entry.value));
  };

  const onListKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    switch (event.key) {
      case 'ArrowDown': setActiveIndex(index => Math.min(entries.length - 1, index + 1)); break;
      case 'ArrowUp': setActiveIndex(index => Math.max(0, index - 1)); break;
      case 'Home': setActiveIndex(0); break;
      case 'End': setActiveIndex(entries.length - 1); break;
      case 'Enter':
      case ' ': toggle(activeIndex); break;
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
        title={selectedLabels.length > 1 ? selectedLabels.join(', ') : undefined}
        onClick={() => (open ? setOpen(false) : openList())}
        onKeyDown={event => {
          if (!open && (event.key === 'ArrowDown' || event.key === 'ArrowUp')) {
            event.preventDefault();
            openList();
          }
        }}
        className="flex h-10 w-full items-center justify-between gap-2 rounded-xl border border-slate-200 bg-white px-3 text-left text-sm text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400"
      >
        <span className="truncate">{triggerLabel}</span>
        <ChevronDown className={cn('h-4 w-4 shrink-0 text-slate-400 transition-transform', open && 'rotate-180')} aria-hidden="true" />
      </button>
      {open && (
        <div
          ref={list}
          id={listId}
          role="listbox"
          aria-multiselectable="true"
          aria-label={selectedLabels.length > 0 ? `Selected: ${selectedLabels.join(', ')}` : 'Nothing selected'}
          tabIndex={-1}
          aria-activedescendant={`${listId}-${activeIndex}`}
          onKeyDown={onListKeyDown}
          className="absolute left-0 top-full z-30 mt-1 max-h-64 min-w-full overflow-y-auto rounded-xl border border-slate-200 bg-white p-1 shadow-lg outline-none"
        >
          {entries.map((entry, index) => {
            const selected = entry.value === '' ? values.length === 0 : values.includes(entry.value);
            return (
              <div
                key={entry.value || '__all'}
                id={`${listId}-${index}`}
                role="option"
                aria-selected={selected}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => toggle(index)}
                className={cn(
                  'flex cursor-pointer items-center gap-2 rounded-lg px-2.5 py-1.5 text-sm text-slate-700',
                  index === activeIndex && 'bg-slate-100',
                  selected && 'font-semibold text-primary-700',
                )}
              >
                <span
                  aria-hidden="true"
                  className={cn(
                    'flex h-4 w-4 shrink-0 items-center justify-center rounded border',
                    selected ? 'border-primary bg-primary text-white' : 'border-slate-300 bg-white',
                  )}
                >
                  {selected && <Check className="h-3 w-3" />}
                </span>
                <span className="truncate">{entry.label}</span>
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
      competitions: (options?.competitions ?? []).map(value => ({ value, label: value })),
      competitionStages: (options?.competitionStages ?? PROJECT_COMPETITION_STAGES).map(stage => ({ value: stage, label: COMPETITION_STAGE_LABELS[stage] })),
      competitionYears: (options?.competitionYears ?? []).map(year => ({ value: String(year), label: String(year) })),
    };
  }, [options]);

  const valueLabel = (key: FilterKey, value: string): string => {
    const lookup: Record<FilterKey, SelectOption[]> = {
      subjectCode: choices.subjects,
      semester: choices.terms,
      year: choices.years,
      group: choices.groups,
      startupIndustry: choices.industries,
      major: choices.majors,
      lecturerId: choices.lecturers,
      mentorId: choices.mentors,
      achievement: choices.achievements,
      competition: choices.competitions,
      competitionStage: choices.competitionStages,
      competitionYear: choices.competitionYears,
    };
    return lookup[key].find(option => option.value === value)?.label ?? value;
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
        <SelectField label="Filter by subject" allLabel="All Subjects" value={query.subjectCode} options={choices.subjects} disabled={disabled} onChange={value => onChange({ subjectCode: value })} />
        <SelectField label="Filter by semester" allLabel="All Semesters" value={query.semester} options={choices.terms} onChange={value => onChange({ semester: value as ProjectDataQuery['semester'] })} />
        <SelectField label="Filter by year" allLabel="All Years" value={query.year} options={choices.years} disabled={disabled} onChange={value => onChange({ year: value })} />
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
        <MultiSelectField label="Group" values={query.group} options={choices.groups} disabled={disabled} onChange={values => onChange({ group: values })} />
        <MultiSelectField label="Startup industry" values={query.startupIndustry} options={choices.industries} disabled={disabled} onChange={values => onChange({ startupIndustry: values })} />
        <MultiSelectField label="Major" values={query.major} options={choices.majors} disabled={disabled} onChange={values => onChange({ major: values })} />
        <MultiSelectField label="Lecturer" values={query.lecturerId} options={choices.lecturers} disabled={disabled} onChange={values => onChange({ lecturerId: values })} />
        <MultiSelectField label="Mentor" values={query.mentorId} options={choices.mentors} disabled={disabled} onChange={values => onChange({ mentorId: values })} />
        <MultiSelectField label="Achievement" values={query.achievement} options={choices.achievements} disabled={disabled} onChange={values => onChange({ achievement: values as ProjectDataQuery['achievement'] })} />
        <MultiSelectField label="Competition" values={query.competition} options={choices.competitions} disabled={disabled} onChange={values => onChange({ competition: values })} />
        <MultiSelectField label="Competition stage" values={query.competitionStage} options={choices.competitionStages} disabled={disabled} onChange={values => onChange({ competitionStage: values as ProjectDataQuery['competitionStage'] })} />
        <MultiSelectField label="Competition year" values={query.competitionYear} options={choices.competitionYears} disabled={disabled} onChange={values => onChange({ competitionYear: values })} />
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
          {activeFilterValues(query).map(({ key, value }) => {
            const label = valueLabel(key, value);
            return (
              <span key={`${key}:${value}`} className="inline-flex items-center gap-1 rounded-full border border-primary-100 bg-primary-50 py-0.5 pl-2.5 pr-1 text-xs text-primary-700">
                {FILTER_LABELS[key]}: {label}
                <button
                  type="button"
                  aria-label={`Remove ${FILTER_LABELS[key]} filter ${label}`}
                  onClick={() => onChange(removeFilterValue(query, key, value))}
                  className="rounded-full p-0.5 hover:bg-primary-100"
                >
                  <X className="h-3 w-3" />
                </button>
              </span>
            );
          })}
          <Button variant="ghost" size="xs" onClick={onClear}>Clear filters</Button>
        </div>
      )}
    </section>
  );
}
