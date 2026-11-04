import { Plus, Trash2 } from 'lucide-react';
import Button from '../../../components/ui/Button';
import {
  COMPETITION_MAX_ENTRIES,
  COMPETITION_OTHERS,
  COMPETITION_OTHER_NAME_MAX_LENGTH,
  COMPETITION_STAGE_LABELS,
  COMPETITION_YEAR_MAX,
  COMPETITION_YEAR_MIN,
  PROJECT_COMPETITIONS,
  PROJECT_COMPETITION_STAGES,
} from '../../../types/projectData';
import type { ProjectCompetitionStage } from '../../../types/projectData';
import { newCompetitionDraft } from '../../../utils/projectData';
import type { CompetitionDraftEntry, CompetitionDraftView } from '../../../utils/projectData';

const controlClass = 'h-10 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-700 outline-none placeholder:text-slate-400 focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400';

interface ProjectCompetitionEditorProps {
  entries: readonly CompetitionDraftEntry[];
  view: CompetitionDraftView;
  disabled: boolean;
  onChange: (entries: CompetitionDraftEntry[]) => void;
}

/**
 * The rows of a project's competitions: which competition (a fixed list plus "Others" with a free-text name), the year
 * it took place, and how far the project got. The list is saved as a whole with the dialog's Save button.
 */
export default function ProjectCompetitionEditor({ entries, view, disabled, onChange }: ProjectCompetitionEditorProps) {
  const update = (key: string, patch: Partial<CompetitionDraftEntry>) =>
    onChange(entries.map(entry => (entry.key === key ? { ...entry, ...patch } : entry)));
  const atLimit = entries.length >= COMPETITION_MAX_ENTRIES;

  return (
    <div className="mt-2 space-y-2">
      {entries.length === 0 && <p className="text-sm text-slate-400">No competitions yet.</p>}

      {entries.length > 0 && (
        <ul className="space-y-2">
          {entries.map((entry, index) => {
            const error = view.errors[entry.key];
            const rowLabel = `competition ${index + 1}`;
            return (
              <li key={entry.key} className="rounded-xl border border-slate-200 p-3">
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-[minmax(0,2fr)_6rem_minmax(0,1.3fr)_auto] sm:items-center">
                  <select
                    value={entry.competition}
                    disabled={disabled}
                    aria-label={`Competition (${rowLabel})`}
                    aria-invalid={Boolean(error)}
                    onChange={event => update(entry.key, { competition: event.target.value })}
                    className={controlClass}
                  >
                    <option value="">Choose a competition…</option>
                    {PROJECT_COMPETITIONS.map(name => <option key={name} value={name}>{name}</option>)}
                  </select>
                  <input
                    value={entry.year}
                    disabled={disabled}
                    inputMode="numeric"
                    maxLength={4}
                    placeholder="Year"
                    aria-label={`Year the competition took place (${rowLabel}), ${COMPETITION_YEAR_MIN} to ${COMPETITION_YEAR_MAX}`}
                    aria-invalid={Boolean(error)}
                    onChange={event => update(entry.key, { year: event.target.value.replace(/\D/g, '') })}
                    className={controlClass}
                  />
                  <select
                    value={entry.stage}
                    disabled={disabled}
                    aria-label={`Stage reached (${rowLabel})`}
                    onChange={event => update(entry.key, { stage: event.target.value as ProjectCompetitionStage })}
                    className={controlClass}
                  >
                    {PROJECT_COMPETITION_STAGES.map(stage => <option key={stage} value={stage}>{COMPETITION_STAGE_LABELS[stage]}</option>)}
                  </select>
                  <button
                    type="button"
                    disabled={disabled}
                    aria-label={`Remove ${rowLabel}`}
                    onClick={() => onChange(entries.filter(item => item.key !== entry.key))}
                    className="inline-flex h-9 w-9 items-center justify-center justify-self-end rounded-lg text-slate-500 hover:bg-slate-100 hover:text-danger focus-visible:outline-2 focus-visible:outline-primary disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <Trash2 className="h-4 w-4" aria-hidden="true" />
                  </button>
                </div>

                {entry.competition === COMPETITION_OTHERS && (
                  <input
                    value={entry.otherName}
                    disabled={disabled}
                    maxLength={COMPETITION_OTHER_NAME_MAX_LENGTH}
                    placeholder="Competition name"
                    aria-label={`Competition name (${rowLabel})`}
                    aria-invalid={Boolean(error)}
                    onChange={event => update(entry.key, { otherName: event.target.value })}
                    className={`${controlClass} mt-2`}
                  />
                )}

                {error && <p role="alert" className="mt-1.5 text-xs text-danger">{error}</p>}
              </li>
            );
          })}
        </ul>
      )}

      {view.listError && <p role="alert" className="text-xs text-danger">{view.listError}</p>}

      <Button
        variant="outline"
        size="sm"
        icon={Plus}
        disabled={disabled || atLimit}
        onClick={() => onChange([...entries, newCompetitionDraft()])}
      >
        Add competition
      </Button>
      <p className="text-xs text-slate-400">
        Pick the furthest stage the project reached; move it on when the project advances. The year is the year the
        competition took place.
      </p>
    </div>
  );
}
