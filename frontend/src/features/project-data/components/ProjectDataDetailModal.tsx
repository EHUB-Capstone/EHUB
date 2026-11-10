import { useEffect, useId, useState, type ReactNode } from 'react';
import { ArrowRight, ChevronDown, RefreshCw, Rocket } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Button from '../../../components/ui/Button';
import ConfirmDialog from '../../../components/ui/ConfirmDialog';
import Modal from '../../../components/ui/Modal';
import { PROJECT_ACHIEVEMENTS } from '../../../types/projectData';
import type { ProjectAchievement, ProjectCompetitionEntryPayload, ProjectDataItem } from '../../../types/projectData';
import {
  PROJECT_ACHIEVEMENT_NOTE_MAX_LENGTH,
  PROJECT_DATA_EMPTY_VALUE,
  SHOWCASE_METRICS_REFERENCE_NOTE,
  displayShowcaseMetric,
  describeCompetitionDraft,
  describeIndustryDraft,
  displayText,
  normalizeNote,
  projectWorkspacePath,
  removedAchievements,
  sameAchievements,
  sameNote,
  toCompetitionDrafts,
  toggleAchievement,
} from '../../../utils/projectData';
import type { CompetitionDraftEntry } from '../../../utils/projectData';
import { cn } from '../../../utils/cn';
import { ACHIEVEMENT_STYLES } from '../achievementStyles';
import { useActiveStartupIndustries } from '../hooks/useActiveStartupIndustries';
import ProjectAchievementBadges from './ProjectAchievementBadges';
import ProjectAchievementHistory from './ProjectAchievementHistory';
import ProjectCompetitionBadges from './ProjectCompetitionBadges';
import ProjectCompetitionEditor from './ProjectCompetitionEditor';
import { MentorLabel } from './ProjectDataTable';
import ProjectStartupIndustryEditor from './ProjectStartupIndustryEditor';
import ProjectStartupIndustryHistory from './ProjectStartupIndustryHistory';

export interface ProjectDataSaveError {
  message: string;
  isConflict: boolean;
}

/** What a save sends: each part only when it changed, so achievements, industries and competitions stay independent. */
export interface ProjectDataChanges {
  achievements?: { labels: ProjectAchievement[]; note: string | null };
  startupIndustryIds?: string[];
  competitions?: ProjectCompetitionEntryPayload[];
}

interface ProjectDataDetailModalProps {
  item: ProjectDataItem | null;
  canManage: boolean;
  isSaving: boolean;
  isReloading: boolean;
  error: ProjectDataSaveError | null;
  onClose: () => void;
  onSave: (changes: ProjectDataChanges) => void;
  /** Error of the industries part of a save; `error` belongs to the achievements part. */
  industryError: ProjectDataSaveError | null;
  /** Error of the competitions part of a save. */
  competitionError: ProjectDataSaveError | null;
  onReload: () => void;
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-medium uppercase tracking-wider text-slate-400">{label}</dt>
      <dd className="mt-1 break-words text-sm text-slate-700">{children}</dd>
    </div>
  );
}

interface CollapsibleSectionProps {
  headingId: string;
  panelId: string;
  title: string;
  open: boolean;
  onToggle: () => void;
  /** Said next to the title while the section is closed, so a closed section still tells its state. */
  summary: ReactNode;
  /** Shown whether the section is open or not, e.g. a failed save that must not be hidden. */
  outside?: ReactNode;
  children: ReactNode;
}

/** A section whose whole title row opens and closes it. */
function CollapsibleSection({ headingId, panelId, title, open, onToggle, summary, outside, children }: CollapsibleSectionProps) {
  return (
    <section aria-labelledby={headingId}>
      <h3 className="text-xs font-medium uppercase tracking-wider text-slate-400">
        <button
          id={headingId}
          type="button"
          aria-expanded={open}
          aria-controls={panelId}
          onClick={onToggle}
          className="flex w-full items-center gap-2 rounded-lg py-1 text-left font-medium uppercase tracking-wider hover:text-slate-600 focus-visible:outline-2 focus-visible:outline-primary"
        >
          {title}
          {!open && <span className="text-xs font-normal normal-case tracking-normal text-slate-500">{summary}</span>}
          <ChevronDown className={cn('ml-auto h-4 w-4 shrink-0 transition-transform', open && 'rotate-180')} aria-hidden="true" />
        </button>
      </h3>
      <div id={panelId} hidden={!open}>{children}</div>
      {outside}
    </section>
  );
}

function Chips({ values }: { values: readonly string[] }) {
  if (values.length === 0) return <span className="text-slate-400">{PROJECT_DATA_EMPTY_VALUE}</span>;
  return (
    <span className="flex flex-wrap gap-1.5">
      {values.map(value => <span key={value} className="rounded-md bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{value}</span>)}
    </span>
  );
}

export default function ProjectDataDetailModal({
  item,
  canManage,
  isSaving,
  isReloading,
  error,
  onClose,
  onSave,
  industryError,
  competitionError,
  onReload,
}: ProjectDataDetailModalProps) {
  const legendId = useId();
  const achievementsPanelId = useId();
  const competitionsHeadingId = useId();
  const competitionsPanelId = useId();
  const navigate = useNavigate();
  // The achievements section starts closed for every project; the id of the project it was opened for is kept, so
  // switching to another project (or closing the dialog) closes it again.
  const [achievementsOpenFor, setAchievementsOpenFor] = useState<string | null>(null);
  const [competitionsOpenFor, setCompetitionsOpenFor] = useState<string | null>(null);
  // Edits belong to one version of one project; a new row version starts again from the saved labels.
  const itemKey = item ? `${item.projectId}:${item.rowVersion}` : '';
  const [draftState, setDraftState] = useState<{ key: string; labels: ProjectAchievement[]; note: string } | null>(null);
  const [confirmKey, setConfirmKey] = useState<string | null>(null);
  // The industry pick is kept per project, not per version: saving the achievements first moves the version, and a
  // pick that then fails to save must not vanish.
  const [industryDraft, setIndustryDraft] = useState<{ projectId: string; ids: string[] } | null>(null);
  // Same for the competition rows: kept per project, so a list that fails to save is not lost when another part saves.
  const [competitionDraft, setCompetitionDraft] = useState<{ projectId: string; entries: CompetitionDraftEntry[] } | null>(null);
  const savedNote = item?.achievementNote ?? '';
  const draft = draftState?.key === itemKey ? draftState.labels : item?.achievements ?? [];
  const draftNote = draftState?.key === itemKey ? draftState.note : savedNote;
  // A note explains labels, so without any label it is empty (and cleared on save).
  const effectiveNote = draft.length > 0 ? draftNote : '';
  const setDraft = (labels: ProjectAchievement[], note: string) => setDraftState({ key: itemKey, labels, note });
  const confirmingRemoval = confirmKey === itemKey && itemKey !== '';
  const setConfirmingRemoval = (open: boolean) => setConfirmKey(open ? itemKey : null);
  // A closed dialog keeps no pick, so reopening the project starts from its saved industries.
  if (!item && industryDraft) setIndustryDraft(null);
  if (!item && achievementsOpenFor) setAchievementsOpenFor(null);
  if (!item && competitionsOpenFor) setCompetitionsOpenFor(null);
  if (!item && competitionDraft) setCompetitionDraft(null);
  const industryOptions = useActiveStartupIndustries(canManage && Boolean(item));
  const industryView = describeIndustryDraft(
    item?.startupIndustries ?? [],
    industryOptions.industries,
    industryDraft && industryDraft.projectId === item?.projectId ? industryDraft.ids : null,
    industryOptions.isLoaded,
  );

  // Rows the user has not touched come from the saved list; a draft of another project is never shown.
  const competitionEntries = competitionDraft && competitionDraft.projectId === item?.projectId
    ? competitionDraft.entries
    : null;

  useEffect(() => {
    if (!item) return undefined;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isSaving && !confirmingRemoval) onClose();
    };
    window.addEventListener('keydown', closeOnEscape);
    return () => window.removeEventListener('keydown', closeOnEscape);
  }, [item, isSaving, confirmingRemoval, onClose]);

  if (!item) return null;

  const achievementsDirty = !sameAchievements(draft, item.achievements) || !sameNote(effectiveNote, savedNote);
  const industriesDirty = industryView.dirty;
  const competitionRows = competitionEntries ?? toCompetitionDrafts(item.competitions);
  const competitionView = describeCompetitionDraft(item.competitions, competitionRows);
  const competitionsDirty = competitionEntries !== null && competitionView.dirty;
  const dirty = achievementsDirty || industriesDirty || competitionsDirty;
  // An invalid industry pick or competition row blocks the whole save, so a half-applied change never needs explaining.
  const blocked = Boolean(industryView.validationMessage) || (competitionsDirty && competitionView.blocked);
  const removed = removedAchievements(item.achievements, draft);
  const achievementsOpen = achievementsOpenFor === item.projectId;
  const competitionsOpen = competitionsOpenFor === item.projectId;
  // Reloading shows what others saved, so a pick made against the old data is dropped like the label draft is.
  const reload = () => { setIndustryDraft(null); setCompetitionDraft(null); onReload(); };
  const save = () => onSave({
    ...(achievementsDirty ? { achievements: { labels: draft, note: normalizeNote(effectiveNote) } } : {}),
    ...(industriesDirty ? { startupIndustryIds: industryView.selectedIds } : {}),
    ...(competitionsDirty ? { competitions: competitionView.payload } : {}),
  });
  const submit = () => {
    if (!dirty || blocked || isSaving) return;
    if (achievementsDirty && removed.length > 0) setConfirmingRemoval(true);
    else save();
  };

  return (
    <>
      <Modal
        isOpen
        onClose={() => { if (!isSaving) onClose(); }}
        title={item.projectName}
        size="lg"
        submitText="Save changes"
        isSubmitting={isSaving}
        submitDisabled={!dirty || blocked}
        onSubmit={canManage ? submit : undefined}
      >
        <div className="space-y-6">
          <dl className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="Semester">{displayText(item.semesterCode)}</Field>
            <Field label="Class Code">{displayText(item.classCode)}</Field>
            <Field label="Group"><Chips values={item.groups} /></Field>
            <Field label="Lecturer">{displayText(item.lecturer?.fullName)}</Field>
            <Field label="Mentor"><MentorLabel mentor={item.mentor} /></Field>
            <Field label="Mentor - GV"><MentorLabel mentor={item.academicMentor} /></Field>
            <div className="sm:col-span-2">
              <ProjectStartupIndustryEditor
                item={item}
                canManage={canManage}
                industries={industryOptions.industries}
                industriesLoading={industryOptions.isLoading}
                industriesError={industryOptions.errorMessage}
                onRetryIndustries={industryOptions.retry}
                selectedIds={industryView.selectedIds}
                onChange={ids => setIndustryDraft({ projectId: item.projectId, ids })}
                retired={industryView.retired}
                validationMessage={industryView.validationMessage}
                isSaving={isSaving}
                isReloading={isReloading}
                error={industryError}
                onReload={reload}
              />
            </div>
            <div className="sm:col-span-2">
              <Field label="Description">
                <p className="whitespace-pre-wrap">{displayText(item.description)}</p>
              </Field>
            </div>
          </dl>

          <section aria-label="Showcase interaction reference" className="space-y-3">
            <h3 className="text-subheading font-semibold text-secondary">Tương tác Showcase</h3>
            <p className="text-caption text-slate-600">{SHOWCASE_METRICS_REFERENCE_NOTE}</p>
            <p className="text-caption text-slate-600">Tổng tương tác hiện tại của dự án qua các học kỳ, gồm cả trang chưa công khai hoặc đã ẩn.</p>
            <dl className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <Field label="Showcase views">{displayShowcaseMetric(item.showcaseMetrics?.viewCount)}</Field>
              <Field label="Showcase upvotes">{displayShowcaseMetric(item.showcaseMetrics?.upvoteCount)}</Field>
              <Field label="Showcase comments">{displayShowcaseMetric(item.showcaseMetrics?.commentCount)}</Field>
            </dl>
          </section>

          {canManage && <ProjectStartupIndustryHistory key={item.projectId} projectId={item.projectId} rowVersion={item.rowVersion} />}

          <CollapsibleSection
            headingId={legendId}
            panelId={achievementsPanelId}
            title="Achievements"
            open={achievementsOpen}
            onToggle={() => setAchievementsOpenFor(achievementsOpen ? null : item.projectId)}
            summary={`${draft.length > 0 ? `${draft.length} selected` : 'None selected'}${achievementsDirty ? ' · unsaved changes' : ''}`}
            outside={error && (
              <div role="alert" className="mt-3 rounded-xl border border-danger-light bg-danger-50 p-3 text-sm text-danger">
                <p>{error.message}</p>
                {error.isConflict && (
                  <Button variant="outline" size="sm" icon={RefreshCw} isLoading={isReloading} onClick={reload} className="mt-2">
                    Reload latest data
                  </Button>
                )}
              </div>
            )}
          >
            {canManage ? (
              <fieldset className="mt-2 space-y-2" disabled={isSaving} aria-labelledby={legendId}>
                {PROJECT_ACHIEVEMENTS.map(name => {
                  const { icon: Icon } = ACHIEVEMENT_STYLES[name];
                  return (
                    <label key={name} className="flex cursor-pointer items-center gap-3 rounded-xl border border-slate-200 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 has-[:checked]:border-primary-100 has-[:checked]:bg-primary-50/50">
                      <input
                        type="checkbox"
                        checked={draft.includes(name)}
                        onChange={() => setDraft(toggleAchievement(draft, name), draftNote)}
                        className="h-4 w-4 rounded border-slate-300 text-primary-700 focus:ring-primary/30"
                      />
                      <Icon className="h-4 w-4 text-slate-500" aria-hidden="true" />
                      {name}
                    </label>
                  );
                })}
                <p className="text-xs text-slate-400">An unchecked label only means the project has not been tagged with it.</p>
                <label className="block pt-1">
                  <span className="text-xs font-medium text-slate-500">Note (optional)</span>
                  <textarea
                    value={effectiveNote}
                    disabled={draft.length === 0}
                    maxLength={PROJECT_ACHIEVEMENT_NOTE_MAX_LENGTH}
                    rows={3}
                    onChange={event => setDraft(draft, event.target.value)}
                    placeholder={draft.length > 0 ? 'Why does this project carry these labels?' : 'Select an achievement to add a note'}
                    className="mt-1 w-full resize-none rounded-xl border border-slate-200 px-3 py-2 text-sm text-slate-700 outline-none placeholder:text-slate-400 focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:bg-slate-50"
                  />
                  <span className="block text-right text-xs text-slate-400">{effectiveNote.length}/{PROJECT_ACHIEVEMENT_NOTE_MAX_LENGTH}</span>
                </label>
              </fieldset>
            ) : (
              <div className="mt-2 space-y-2">
                <ProjectAchievementBadges achievements={item.achievements} />
                {item.achievementNote && <p className="whitespace-pre-wrap break-words rounded-xl bg-slate-50 px-3 py-2 text-sm text-slate-600">{item.achievementNote}</p>}
              </div>
            )}

            {item.achievementsUpdatedAtUtc && (
              <ProjectAchievementHistory key={item.projectId} projectId={item.projectId} rowVersion={item.rowVersion} />
            )}
          </CollapsibleSection>

          <CollapsibleSection
            headingId={competitionsHeadingId}
            panelId={competitionsPanelId}
            title="Competitions"
            open={competitionsOpen}
            onToggle={() => setCompetitionsOpenFor(competitionsOpen ? null : item.projectId)}
            summary={`${competitionRows.length > 0 ? `${competitionRows.length} ${competitionRows.length === 1 ? 'entry' : 'entries'}` : 'None'}${competitionsDirty ? (competitionView.blocked ? ' · fix the errors' : ' · unsaved changes') : ''}`}
            outside={competitionError && (
              <div role="alert" className="mt-3 rounded-xl border border-danger-light bg-danger-50 p-3 text-sm text-danger">
                <p>{competitionError.message}</p>
                {competitionError.isConflict && (
                  <Button variant="outline" size="sm" icon={RefreshCw} isLoading={isReloading} onClick={reload} className="mt-2">
                    Reload latest data
                  </Button>
                )}
              </div>
            )}
          >
            {canManage ? (
              <ProjectCompetitionEditor
                entries={competitionRows}
                view={competitionView}
                disabled={isSaving}
                onChange={entries => setCompetitionDraft({ projectId: item.projectId, entries })}
              />
            ) : (
              <div className="mt-2">
                <ProjectCompetitionBadges competitions={item.competitions} visible={item.competitions.length} />
              </div>
            )}
          </CollapsibleSection>

          <div className="border-t border-slate-100 pt-4">
            <Button
              variant="primary"
              icon={Rocket}
              iconRight={ArrowRight}
              disabled={isSaving}
              onClick={() => navigate(projectWorkspacePath(item.teamId))}
            >
              Open Startup Workspace
            </Button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        isOpen={confirmingRemoval}
        onClose={() => setConfirmingRemoval(false)}
        onConfirm={() => { setConfirmingRemoval(false); save(); }}
        title="Remove achievement labels?"
        description={`This removes ${removed.join(', ')} from “${item.projectName}”${draft.length === 0 && item.achievementNote ? ' together with its note' : ''}. The change is recorded in the project activity log.`}
        confirmText="Remove and save"
        isSubmitting={isSaving}
      />
    </>
  );
}
