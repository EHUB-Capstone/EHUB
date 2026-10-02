import { useId, useMemo, useState } from 'react';
import { Search, SlidersHorizontal, X } from 'lucide-react';
import Button from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';
import {
  FILTER_LABELS,
  PROJECT_DATA_SEARCH_MAX_LENGTH,
  activeFilterKeys,
  hasActiveSearchOrFilters,
} from '../../../utils/projectData';
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
}

function SelectField({ label, value, options, onChange, disabled }: SelectFieldProps) {
  const id = useId();
  // A value coming from a shared URL must stay visible even when it is not among the loaded options.
  const items = value && !options.some(option => option.value === value)
    ? [{ value, label: value }, ...options]
    : options;

  return (
    <div className="min-w-0">
      <label htmlFor={id} className="mb-1 block text-xs font-medium text-slate-500">{label}</label>
      <select
        id={id}
        value={value}
        disabled={disabled}
        onChange={event => onChange(event.target.value)}
        className="h-10 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400"
      >
        <option value="">All</option>
        {items.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
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
  const advancedActive = activeKeys.some(key => !['semesterId', 'subjectCode'].includes(key));
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
      semesters: (options?.semesters ?? []).map(item => ({ value: item.id, label: item.isActive ? `${item.code} (current)` : item.code })),
      subjects: (options?.subjects ?? []).map(item => ({ value: item.code, label: item.name ? `${item.code} – ${item.name}` : item.code })),
      groups: (options?.groups ?? []).map(value => ({ value, label: value })),
      industries: (options?.startupIndustries ?? []).map(value => ({ value, label: value })),
      lecturers: (options?.lecturers ?? []).map(item => ({ value: item.userId, label: item.fullName })),
      mentors: [...mentorsByUser.entries()].map(([value, entry]) => ({ value, label: `${entry.name} (${entry.slots.join(', ')})` })),
      achievements: (options?.achievements ?? []).map(value => ({ value, label: value })),
    };
  }, [options]);

  const valueLabel = (key: (typeof activeKeys)[number]): string => {
    const lookup: Record<string, SelectOption[]> = {
      semesterId: choices.semesters,
      subjectCode: choices.subjects,
      group: choices.groups,
      startupIndustry: choices.industries,
      lecturerId: choices.lecturers,
      mentorId: choices.mentors,
      achievement: choices.achievements,
    };
    const value = query[key];
    return lookup[key]?.find(option => option.value === value)?.label ?? value;
  };

  return (
    <section aria-label="Search and filters" className="mb-4 rounded-2xl border border-slate-200/60 bg-white p-4 shadow-sm">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-end">
        <div className="min-w-0 flex-1">
          <label htmlFor={searchId} className="mb-1 block text-xs font-medium text-slate-500">Search</label>
          <div className="relative">
            <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden="true" />
            <input
              id={searchId}
              type="search"
              value={searchInput}
              maxLength={PROJECT_DATA_SEARCH_MAX_LENGTH}
              onChange={event => onSearchInputChange(event.target.value)}
              placeholder="Search project, description, subject, semester, lecturer, mentor or industry"
              className="h-10 w-full rounded-xl border border-slate-200 bg-white pl-10 pr-3 text-sm text-slate-700 outline-none placeholder:text-slate-400 focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3 lg:w-[26rem]">
          <SelectField label="Semester" value={query.semesterId} options={choices.semesters} disabled={disabled} onChange={value => onChange({ semesterId: value })} />
          <SelectField label="Subject" value={query.subjectCode} options={choices.subjects} disabled={disabled} onChange={value => onChange({ subjectCode: value })} />
        </div>
        <Button
          variant="outline"
          icon={SlidersHorizontal}
          aria-expanded={showAdvanced}
          aria-controls={advancedId}
          onClick={() => setExpanded(current => !current)}
          className="h-10 shrink-0"
        >
          More filters
        </Button>
      </div>

      <div id={advancedId} hidden={!showAdvanced} className={cn('mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5', !showAdvanced && 'hidden')}>
        <SelectField label="Group" value={query.group} options={choices.groups} disabled={disabled} onChange={value => onChange({ group: value })} />
        <SelectField label="Startup industry" value={query.startupIndustry} options={choices.industries} disabled={disabled} onChange={value => onChange({ startupIndustry: value })} />
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
            <span key={key} className="inline-flex items-center gap-1 rounded-full border border-primary-100 bg-primary-50 py-0.5 pl-2.5 pr-1 text-xs text-primary">
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
