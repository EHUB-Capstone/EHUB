import { useState } from 'react';
import { FileSpreadsheet, Shuffle, UserCog, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { mentorAdminApi } from '../../api/mentorAdminApi';
import type {
  MentorAllocationEdit,
  MentorAllocationPreview,
  MentorAllocationStrategy,
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

export default function MentorAdministrationCard({ semesterId, semesterLabel, temporaryMentorCount = 0, onImportCommitted }: MentorAdministrationCardProps) {
  const [allocationPreview, setAllocationPreview] = useState<MentorAllocationPreview | null>(null);
  const [strategy, setStrategy] = useState<MentorAllocationStrategy>('Balanced');
  const [includeTemporary, setIncludeTemporary] = useState(true);
  const [edits, setEdits] = useState<MentorAllocationEdit[]>([]);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [managerOpen, setManagerOpen] = useState(false);
  const [previewStale, setPreviewStale] = useState(false);
  const [confirmReplacement, setConfirmReplacement] = useState(false);
  const [busy, setBusy] = useState<'export' | 'preview-allocation' | 'refresh-allocation' | 'commit-allocation' | null>(null);

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
  const loadPreview = async (nextEdits: MentorAllocationEdit[], seed?: number) => {
    if (!semesterId) return null;
    const response = await mentorAdminApi.previewAllocation(semesterId, seed, strategy, nextEdits, includeTemporary);
    return response.data;
  };

  const previewAllocation = async () => {
    if (!semesterId) return;
    setBusy('preview-allocation');
    try {
      const preview = await loadPreview([]);
      if (!preview) return;
      setAllocationPreview(preview);
      setEdits([]);
      setPreviewStale(false);
      setPreviewOpen(true);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to preview mentor assignment').message);
    } finally {
      setBusy(null);
    }
  };

  const applyEdit = async (edit: MentorAllocationEdit) => {
    if (!allocationPreview) return;
    const requested = upsertEdit(edits, edit);
    setBusy('refresh-allocation');
    try {
      const preview = await loadPreview(requested, allocationPreview.seed);
      if (!preview) return;
      setAllocationPreview(preview);
      setEdits(keepAppliedEdits(requested, preview));
      setPreviewStale(false);
      const notApplied = (preview.conflicts ?? []).find(conflict => conflict.teamId === edit.teamId && conflict.mentorType === edit.mentorType);
      if (notApplied) toast.error(notApplied.message);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to apply the edit').message);
    } finally {
      setBusy(null);
    }
  };

  const undoEdit = async (teamId: string, mentorType: MentorType) => {
    if (!allocationPreview) return;
    const remaining = removeEdit(edits, teamId, mentorType);
    setBusy('refresh-allocation');
    try {
      const preview = await loadPreview(remaining, allocationPreview.seed);
      if (!preview) return;
      setAllocationPreview(preview);
      setEdits(keepAppliedEdits(remaining, preview));
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to undo the edit').message);
    } finally {
      setBusy(null);
    }
  };

  const closePreview = () => {
    setPreviewOpen(false);
    setAllocationPreview(null);
    setEdits([]);
    setPreviewStale(false);
  };

  const requestCommit = () => {
    if (!allocationPreview?.canCommit || previewStale) return;
    if ((allocationPreview.replacementCount ?? 0) > 0) setConfirmReplacement(true);
    else void commitAllocation();
  };

  const commitAllocation = async () => {
    if (!allocationPreview?.canCommit) return;
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

  const strategyHint = strategy === 'Balanced'
    ? 'Balanced gives each missing slot to the mentor with the fewest teams this semester.'
    : 'Random picks any eligible mentor for each missing slot, whatever their current load.';

  return (
    <section className="rounded-2xl border border-primary-100 bg-white px-4 py-3 shadow-sm dark:border-primary/35 dark:bg-[#111827] dark:shadow-[0_10px_30px_rgba(0,0,0,0.18)]">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-3">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary text-white shadow-sm ring-1 ring-primary-200 dark:ring-primary/50">
            <Users className="h-4 w-4" />
          </span>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="font-bold text-slate-900">Mentor assignment</h2>
              <span className="rounded-full bg-primary-50 px-2 py-0.5 text-[11px] font-semibold text-primary">{semesterLabel}</span>
            </div>
            <p className="text-xs text-slate-500">Keeps mentors of continuing teams, fills only missing slots. Nothing is saved before you confirm.</p>
          </div>
        </div>

        {semesterId && (
          <div className="flex flex-wrap items-center gap-2">
            <div role="group" aria-label="Assignment method" title={strategyHint} className="inline-flex rounded-lg border border-slate-200 bg-white p-0.5">
              {(['Balanced', 'Random'] as const).map(option => (
                <button
                  key={option}
                  type="button"
                  aria-pressed={strategy === option}
                  disabled={busy !== null}
                  onClick={() => setStrategy(option)}
                  className={`whitespace-nowrap rounded-md px-3 py-1 text-xs font-semibold transition-colors ${strategy === option ? 'bg-primary text-white' : 'text-slate-600 hover:text-slate-900'}`}
                >
                  {option}
                </button>
              ))}
            </div>
            <Button size="sm" className="whitespace-nowrap" icon={Shuffle} onClick={() => void previewAllocation()} isLoading={busy === 'preview-allocation'}>Preview assignment</Button>
            <Button size="sm" variant="outline" className="whitespace-nowrap" icon={UserCog} onClick={() => setManagerOpen(true)}>Manage mentors</Button>
            <Button size="sm" variant="outline" className="whitespace-nowrap" icon={FileSpreadsheet} onClick={() => void exportAssignments()} isLoading={busy === 'export'}>Export</Button>
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
          <p className="mt-2 text-[11px] text-slate-400">{strategyHint} New mentors are imported in User Management, then added with Add mentors.</p>

          <SemesterMentorManager
            isOpen={managerOpen}
            semesterId={semesterId}
            semesterLabel={semesterLabel}
            onClose={() => setManagerOpen(false)}
            onChanged={onImportCommitted}
          />
          <MentorAllocationPreviewModal
            isOpen={previewOpen}
            preview={allocationPreview}
            edits={edits}
            isRefreshing={busy === 'refresh-allocation'}
            isCommitting={busy === 'commit-allocation'}
            isStale={previewStale}
            onClose={closePreview}
            onApplyEdit={edit => void applyEdit(edit)}
            onRemoveEdit={(teamId, mentorType) => void undoEdit(teamId, mentorType)}
            onRegenerate={() => void previewAllocation()}
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
