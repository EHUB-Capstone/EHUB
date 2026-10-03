import { useEffect, useId, useState, type ReactNode } from 'react';
import { RefreshCw } from 'lucide-react';
import Button from '../../../components/ui/Button';
import ConfirmDialog from '../../../components/ui/ConfirmDialog';
import Modal from '../../../components/ui/Modal';
import { PROJECT_ACHIEVEMENTS } from '../../../types/projectData';
import type { ProjectAchievement, ProjectDataItem } from '../../../types/projectData';
import {
  PROJECT_DATA_EMPTY_VALUE,
  displayText,
  removedAchievements,
  sameAchievements,
  toggleAchievement,
} from '../../../utils/projectData';
import { ACHIEVEMENT_STYLES } from '../achievementStyles';
import ProjectAchievementBadges from './ProjectAchievementBadges';
import { MentorLabel } from './ProjectDataTable';

export interface ProjectDataSaveError {
  message: string;
  isConflict: boolean;
}

interface ProjectDataDetailModalProps {
  item: ProjectDataItem | null;
  canManage: boolean;
  isSaving: boolean;
  isReloading: boolean;
  error: ProjectDataSaveError | null;
  onClose: () => void;
  onSave: (achievements: ProjectAchievement[]) => void;
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
  onReload,
}: ProjectDataDetailModalProps) {
  const legendId = useId();
  // Edits belong to one version of one project; a new row version starts again from the saved labels.
  const itemKey = item ? `${item.projectId}:${item.rowVersion}` : '';
  const [draftState, setDraftState] = useState<{ key: string; value: ProjectAchievement[] } | null>(null);
  const [confirmKey, setConfirmKey] = useState<string | null>(null);
  const draft = draftState?.key === itemKey ? draftState.value : item?.achievements ?? [];
  const confirmingRemoval = confirmKey === itemKey && itemKey !== '';
  const setConfirmingRemoval = (open: boolean) => setConfirmKey(open ? itemKey : null);

  useEffect(() => {
    if (!item) return undefined;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isSaving && !confirmingRemoval) onClose();
    };
    window.addEventListener('keydown', closeOnEscape);
    return () => window.removeEventListener('keydown', closeOnEscape);
  }, [item, isSaving, confirmingRemoval, onClose]);

  if (!item) return null;

  const dirty = !sameAchievements(draft, item.achievements);
  const removed = removedAchievements(item.achievements, draft);
  const submit = () => {
    if (!dirty || isSaving) return;
    if (removed.length > 0) setConfirmingRemoval(true);
    else onSave(draft);
  };

  return (
    <>
      <Modal
        isOpen
        onClose={() => { if (!isSaving) onClose(); }}
        title={item.projectName}
        size="lg"
        submitText="Save achievements"
        isSubmitting={isSaving}
        submitDisabled={!dirty}
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
            <div className="sm:col-span-2"><Field label="Startup Industry"><Chips values={item.startupIndustries} /></Field></div>
            <div className="sm:col-span-2">
              <Field label="Description">
                <p className="whitespace-pre-wrap">{displayText(item.description)}</p>
              </Field>
            </div>
          </dl>

          <section aria-labelledby={legendId}>
            <h3 id={legendId} className="text-xs font-medium uppercase tracking-wider text-slate-400">Achievements</h3>
            {canManage ? (
              <fieldset className="mt-2 space-y-2" disabled={isSaving} aria-labelledby={legendId}>
                {PROJECT_ACHIEVEMENTS.map(name => {
                  const { icon: Icon } = ACHIEVEMENT_STYLES[name];
                  return (
                    <label key={name} className="flex cursor-pointer items-center gap-3 rounded-xl border border-slate-200 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 has-[:checked]:border-primary-100 has-[:checked]:bg-primary-50/50">
                      <input
                        type="checkbox"
                        checked={draft.includes(name)}
                        onChange={() => setDraftState({ key: itemKey, value: toggleAchievement(draft, name) })}
                        className="h-4 w-4 rounded border-slate-300 text-primary focus:ring-primary/30"
                      />
                      <Icon className="h-4 w-4 text-slate-500" aria-hidden="true" />
                      {name}
                    </label>
                  );
                })}
                <p className="text-xs text-slate-400">An unchecked label only means the project has not been tagged with it.</p>
              </fieldset>
            ) : (
              <div className="mt-2"><ProjectAchievementBadges achievements={item.achievements} /></div>
            )}

            {error && (
              <div role="alert" className="mt-3 rounded-xl border border-danger-light bg-danger-50 p-3 text-sm text-danger">
                <p>{error.message}</p>
                {error.isConflict && (
                  <Button variant="outline" size="sm" icon={RefreshCw} isLoading={isReloading} onClick={onReload} className="mt-2">
                    Reload latest data
                  </Button>
                )}
              </div>
            )}
          </section>
        </div>
      </Modal>

      <ConfirmDialog
        isOpen={confirmingRemoval}
        onClose={() => setConfirmingRemoval(false)}
        onConfirm={() => { setConfirmingRemoval(false); onSave(draft); }}
        title="Remove achievement labels?"
        description={`This removes ${removed.join(', ')} from “${item.projectName}”. The change is recorded in the project activity log.`}
        confirmText="Remove and save"
        isSubmitting={isSaving}
      />
    </>
  );
}
