import { useEffect, useRef, useState } from 'react';
import { FileSpreadsheet, Shuffle, Sparkles, UserCog, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { mentorAdminApi } from '../../api/mentorAdminApi';
import type {
  MentorAllocationEdit,
  MentorAllocationPreview,
  MentorAllocationStrategy,
  MentorSemesterClass,
  MentorType,
} from '../../types/mentorAdmin';
import { parseApiError } from '../../utils/apiError';
import { keepAppliedEdits, removeEdit, upsertEdit } from '../../utils/mentorAllocationPreview';
import Button from '../ui/Button';
import ConfirmDialog from '../ui/ConfirmDialog';
import MentorAllocationPreviewModal from './MentorAllocationPreviewModal';
import SemesterMentorManager from './SemesterMentorManager';

interface MentorAdministrationCardProps {
  semesterId?: string;
  semesterLabel: string;
  /** Mentors without an account that are active in this semester. */
  temporaryMentorCount?: number;
  onImportCommitted: () => Promise<void> | void;
}

interface AllocationScope {
  classIds: string[];
  label: string;
  strategy: MentorAllocationStrategy;
}

export default function MentorAdministrationCard({ semesterId, semesterLabel, temporaryMentorCount = 0, onImportCommitted }: MentorAdministrationCardProps) {
  const [allocationPreview, setAllocationPreview] = useState<MentorAllocationPreview | null>(null);
  const [strategy, setStrategy] = useState<MentorAllocationStrategy>('Ai');
  const [previewScope, setPreviewScope] = useState<AllocationScope | null>(null);
  const [previewFailure, setPreviewFailure] = useState<{ scope: AllocationScope; message: string } | null>(null);
  const [includeTemporary, setIncludeTemporary] = useState(true);
  const [edits, setEdits] = useState<MentorAllocationEdit[]>([]);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [managerOpen, setManagerOpen] = useState(false);
  const [previewStale, setPreviewStale] = useState(false);
  const [confirmReplacement, setConfirmReplacement] = useState(false);
  const [busy, setBusy] = useState<'export' | 'preview-allocation' | 'refresh-allocation' | 'commit-allocation' | null>(null);
  const previewRequest = useRef<AbortController | null>(null);

  useEffect(() => () => { previewRequest.current?.abort(); }, [semesterId]);

  const exportAssignments = async () => {
    if (!semesterId) return;
    setBusy('export');
    try {
      const blob = await mentorAdminApi.exportAssignments(semesterId);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${semesterLabel.replace(/\s+/g, '_')}_mentor_assignments.xlsx`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to export mentor assignments').message);
    } finally {
      setBusy(null);
    }
  };

  // Generates a preview. Re-using the seed and strategy keeps the proposal stable while the admin edits it by hand.
  const loadPreview = async (scope: AllocationScope, nextEdits: MentorAllocationEdit[], seed?: number) => {
    if (!semesterId) return null;
    previewRequest.current?.abort();
    const controller = new AbortController();
    previewRequest.current = controller;
    const response = await mentorAdminApi.previewAllocation(semesterId, seed, scope.strategy, nextEdits, includeTemporary, scope.classIds, controller.signal);
    if (controller.signal.aborted) return null;
    return response.data;
  };

  const previewAllocation = async (scope: AllocationScope = { classIds: [], label: semesterLabel, strategy }) => {
    if (!semesterId || busy !== null) return;
    setBusy('preview-allocation');
    setPreviewFailure(null);
    try {
      const preview = await loadPreview(scope, []);
      if (!preview) return;
      setPreviewScope(scope);
      setAllocationPreview(preview);
      setEdits([]);
      setPreviewStale(false);
      setPreviewOpen(true);
    } catch (error) {
      if (!previewRequest.current?.signal.aborted) {
        setPreviewFailure({ scope, message: parseApiError(error, 'Failed to preview mentor assignment').message });
      }
    } finally {
      setBusy(null);
    }
  };

  const previewClass = (item: MentorSemesterClass) => {
    setManagerOpen(false);
    void previewAllocation({ classIds: [item.classId], label: `${item.classCode} · ${item.subjectCode}`, strategy: 'Ai' });
  };

  const applyEdit = async (edit: MentorAllocationEdit) => {
    if (!allocationPreview || !previewScope || busy !== null) return;
    const requested = upsertEdit(edits, edit);
    setBusy('refresh-allocation');
    try {
      const preview = await loadPreview(previewScope, requested, allocationPreview.seed);
      if (!preview) return;
      setAllocationPreview(preview);
      setEdits(keepAppliedEdits(requested, preview));
      setPreviewStale(false);
      const notApplied = (preview.conflicts ?? []).find(conflict => conflict.teamId === edit.teamId && conflict.mentorType === edit.mentorType);
      if (notApplied) toast.error(notApplied.message);
    } catch (error) {
      if (!previewRequest.current?.signal.aborted) toast.error(parseApiError(error, 'Failed to apply the edit').message);
    } finally {
      setBusy(null);
    }
  };

  const undoEdit = async (teamId: string, mentorType: MentorType) => {
    if (!allocationPreview || !previewScope || busy !== null) return;
    const remaining = removeEdit(edits, teamId, mentorType);
    setBusy('refresh-allocation');
    try {
      const preview = await loadPreview(previewScope, remaining, allocationPreview.seed);
      if (!preview) return;
      setAllocationPreview(preview);
      setEdits(keepAppliedEdits(remaining, preview));
    } catch (error) {
      if (!previewRequest.current?.signal.aborted) toast.error(parseApiError(error, 'Failed to undo the edit').message);
    } finally {
      setBusy(null);
    }
  };

  const closePreview = () => {
    setPreviewOpen(false);
    setAllocationPreview(null);
    setPreviewScope(null);
    setEdits([]);
    setPreviewStale(false);
  };

  const requestCommit = () => {
    if (!allocationPreview?.canCommit || previewStale) return;
    if ((allocationPreview.replacementCount ?? 0) > 0) setConfirmReplacement(true);
    else void commitAllocation();
  };

  const commitAllocation = async () => {
    if (!allocationPreview?.canCommit || previewStale || busy !== null) return;
    setConfirmReplacement(false);
    setBusy('commit-allocation');
    try {
      const response = await mentorAdminApi.commitAllocation(allocationPreview.sessionId);
      const ended = response.data.endedCount ?? 0;
      toast.success(`Assigned ${response.data.createdCount} mentor slot${response.data.createdCount === 1 ? '' : 's'}${ended ? ` and ended ${ended} replaced assignment${ended === 1 ? '' : 's'}` : ''}.`);
      closePreview();
      await onImportCommitted();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to save the mentor assignment').message);
      // A conflict means data changed after the preview, so it must be regenerated before it can be saved.
      if ((error as { response?: { status?: number } }).response?.status === 409) setPreviewStale(true);
    } finally {
      setBusy(null);
    }
  };

  const strategyHint = strategy === 'Ai'
    ? 'AI proposes mentors using skills, experience, achievements, mentoring history and current workload. Continuing EXE201 teams keep eligible EXE101 mentors. Review the reasons and accept to save.'
    : strategy === 'Balanced'
    ? 'Balanced gives each missing slot to the mentor with the fewest teams this semester.'
    : 'Random picks any eligible mentor for each missing slot, whatever their current load.';

  return (
    <section className="rounded-2xl border border-primary-100 bg-white px-4 py-3 shadow-sm dark:border-primary/35 dark:bg-dark-surface dark:shadow-[0_10px_30px_rgba(0,0,0,0.18)]">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-3">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary text-white shadow-sm ring-1 ring-primary-200 dark:ring-primary/50">
            <Users className="h-4 w-4" />
          </span>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="font-bold text-slate-900">Mentor assignment</h2>
              <span className="rounded-full bg-primary-50 px-2 py-0.5 text-caption font-semibold text-primary-700">{semesterLabel}</span>
            </div>
            <p className="text-xs text-slate-500">Keeps mentors of continuing teams, fills only missing slots. Nothing is saved before you confirm.</p>
          </div>
        </div>

        {semesterId && (
          <div className="flex flex-wrap items-center gap-2">
            <div role="group" aria-label="Assignment method" title={strategyHint} className="inline-flex rounded-lg border border-slate-200 bg-white p-0.5">
              {(['Ai', 'Balanced', 'Random'] as const).map(option => (
                <button
                  key={option}
                  type="button"
                  aria-pressed={strategy === option}
                  disabled={busy !== null || previewOpen}
                  onClick={() => setStrategy(option)}
                  className={`whitespace-nowrap rounded-md px-3 py-1 text-caption font-semibold transition-colors ${strategy === option ? 'bg-slate-100 text-secondary' : 'text-slate-600 hover:text-slate-900'}`}
                >
                  {option === 'Ai' ? 'AI' : option}
                </button>
              ))}
            </div>
            <Button size="sm" className="whitespace-nowrap" icon={strategy === 'Ai' ? Sparkles : Shuffle} disabled={busy !== null || previewOpen} onClick={() => void previewAllocation()} isLoading={busy === 'preview-allocation'}>{strategy === 'Ai' ? 'AI preview · all classes' : 'Preview assignment'}</Button>
            <Button size="sm" variant="outline" className="whitespace-nowrap" icon={UserCog} disabled={busy !== null || previewOpen} onClick={() => setManagerOpen(true)}>Manage by class</Button>
            <Button size="sm" variant="outline" className="whitespace-nowrap" icon={FileSpreadsheet} disabled={busy !== null || previewOpen} onClick={() => void exportAssignments()} isLoading={busy === 'export'}>Export</Button>
          </div>
        )}
      </div>

      {!semesterId ? (
        <p className="mt-3 rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">This semester has not been planned in the system yet.</p>
      ) : (
        <>
          {temporaryMentorCount > 0 && (
            <label className="mt-2 flex w-fit cursor-pointer items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-2.5 py-1 text-xs font-bold text-amber-900">
              <input
                type="checkbox"
                checked={includeTemporary}
                disabled={busy !== null || previewOpen}
                onChange={event => setIncludeTemporary(event.target.checked)}
                className="h-3.5 w-3.5 rounded border-slate-300 accent-primary"
              />
              Include {temporaryMentorCount} mentor{temporaryMentorCount === 1 ? '' : 's'} without an account yet
            </label>
          )}
          <p className="mt-2 text-caption text-slate-400">{strategyHint} New mentors are imported in User Management, then added with Add mentors.</p>
          {busy === 'preview-allocation' && <p role="status" className="mt-2 text-body-sm text-slate-600">Preparing the proposed assignments…</p>}
          {previewFailure && (
            <div role="alert" className="mt-3 rounded-xl bg-danger-50 px-3 py-2 text-body-sm text-danger-dark">
              <p>{previewFailure.scope.label}: {previewFailure.message}</p>
              <Button size="sm" variant="outline" className="mt-2" disabled={busy !== null} onClick={() => void previewAllocation(previewFailure.scope)}>Try again</Button>
            </div>
          )}

          <SemesterMentorManager
            isOpen={managerOpen}
            semesterId={semesterId}
            semesterLabel={semesterLabel}
            onClose={() => setManagerOpen(false)}
            onChanged={onImportCommitted}
            onAiPreview={previewClass}
          />
          <MentorAllocationPreviewModal
            key={previewOpen ? previewScope?.classIds.join(',') || 'semester' : 'closed'}
            isOpen={previewOpen}
            preview={allocationPreview}
            scopeLabel={previewScope?.label}
            errorMessage={previewFailure?.message}
            edits={edits}
            isRefreshing={busy === 'refresh-allocation' || busy === 'preview-allocation'}
            isCommitting={busy === 'commit-allocation'}
            isStale={previewStale}
            onClose={closePreview}
            onApplyEdit={edit => void applyEdit(edit)}
            onRemoveEdit={(teamId, mentorType) => void undoEdit(teamId, mentorType)}
            onRegenerate={() => previewScope && void previewAllocation(previewScope)}
            onConfirm={requestCommit}
          />
          <ConfirmDialog
            isOpen={confirmReplacement}
            onClose={() => setConfirmReplacement(false)}
            onConfirm={() => void commitAllocation()}
            title="Replace current mentors?"
            description={`${allocationPreview?.replacementCount ?? 0} current mentor assignment(s) will end and the chosen mentors take over. The history of the ended assignments is kept.`}
            confirmText="Replace and save"
            isSubmitting={busy === 'commit-allocation'}
          />
        </>
      )}
    </section>
  );
}
