import { useRef, useState } from 'react';
import { ChevronDown, Download, FileSpreadsheet, Shuffle, Upload, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { mentorAdminApi } from '../../api/mentorAdminApi';
import type {
  MentorAllocationEdit,
  MentorAllocationPreview,
  MentorAllocationStrategy,
  MentorImportPreview,
  MentorType,
} from '../../types/mentorAdmin';
import { parseApiError } from '../../utils/apiError';
import { keepAppliedEdits, removeEdit, upsertEdit } from '../../utils/mentorAllocationPreview';
import Button from '../ui/Button';
import ConfirmDialog from '../ui/ConfirmDialog';
import MentorAllocationPreviewModal from './MentorAllocationPreviewModal';

interface MentorAdministrationCardProps {
  semesterId?: string;
  semesterLabel: string;
  onImportCommitted: () => Promise<void> | void;
}

export default function MentorAdministrationCard({ semesterId, semesterLabel, onImportCommitted }: MentorAdministrationCardProps) {
  const fileInput = useRef<HTMLInputElement>(null);
  const [activePanel, setActivePanel] = useState<'import' | 'allocation' | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [importPreview, setImportPreview] = useState<MentorImportPreview | null>(null);
  const [allocationPreview, setAllocationPreview] = useState<MentorAllocationPreview | null>(null);
  const [strategy, setStrategy] = useState<MentorAllocationStrategy>('Balanced');
  const [edits, setEdits] = useState<MentorAllocationEdit[]>([]);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewStale, setPreviewStale] = useState(false);
  const [confirmReplacement, setConfirmReplacement] = useState(false);
  const [busy, setBusy] = useState<'template' | 'export' | 'preview-import' | 'commit-import' | 'preview-allocation' | 'refresh-allocation' | 'commit-allocation' | null>(null);

  const downloadTemplate = async () => {
    setBusy('template');
    try {
      const blob = await mentorAdminApi.downloadTemplate();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'Danh_sach_Mentor_FA26_mau.xlsx';
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to download mentor template').message);
    } finally {
      setBusy(null);
    }
  };

  const previewImport = async () => {
    if (!semesterId || !file) return;
    setBusy('preview-import');
    setImportPreview(null);
    try {
      const response = await mentorAdminApi.previewImport(semesterId, file);
      setImportPreview(response.data);
      if (!response.data.canCommit) toast.error('Preview contains errors. Correct the workbook and upload it again.');
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to preview mentor import').message);
    } finally {
      setBusy(null);
    }
  };

  const commitImport = async () => {
    if (!importPreview?.canCommit) return;
    setBusy('commit-import');
    try {
      const response = await mentorAdminApi.commitImport(importPreview.sessionId);
      const parts = [
        `${response.data.createdCount} new account${response.data.createdCount === 1 ? '' : 's'}`,
        `${response.data.updatedCount} updated`,
        `${response.data.draftSavedCount} saved for later completion`,
        `${response.data.draftCompletedCount} completed`,
      ];
      const accountHint = response.data.createdCount > 0
        ? ' New accounts can use Forgot Password to set their first password.'
        : '';
      toast.success(`Mentor import completed: ${parts.join(', ')}.${accountHint}`);
      setFile(null);
      setImportPreview(null);
      if (fileInput.current) fileInput.current.value = '';
      await onImportCommitted();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to commit mentor import').message);
    } finally {
      setBusy(null);
    }
  };

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
    const response = await mentorAdminApi.previewAllocation(semesterId, seed, strategy, nextEdits);
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

  return (
    <section className="rounded-2xl border border-primary-100 bg-white p-4 shadow-sm dark:border-primary/35 dark:bg-[#111827] dark:shadow-[0_10px_30px_rgba(0,0,0,0.18)]">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary text-white shadow-sm ring-1 ring-primary-200 dark:ring-primary/50">
            <FileSpreadsheet className="h-4.5 w-4.5" />
          </span>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="font-bold text-slate-900">Mentor import & assignment</h2>
              <span className="rounded-full bg-primary-50 px-2 py-0.5 text-[11px] font-semibold text-primary">{semesterLabel}</span>
            </div>
            <p className="mt-0.5 text-xs text-slate-500">Import mentor rosters, assign mentors to teams and export the result.</p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {semesterId && <Button size="sm" variant="outline" icon={FileSpreadsheet} onClick={() => void exportAssignments()} isLoading={busy === 'export'}>Export assignments</Button>}
          <Button size="sm" variant="outline" icon={Download} onClick={() => void downloadTemplate()} isLoading={busy === 'template'}>Download template</Button>
        </div>
      </div>

      {!semesterId ? (
        <p className="mt-3 rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">This semester has not been planned in the system yet.</p>
      ) : (
        <>
          <div className="mt-3 grid gap-2 md:grid-cols-2">
            <ActionToggle
              active={activePanel === 'import'}
              icon={FileSpreadsheet}
              title="Import mentor list"
              description="Validate and import both mentor sheets"
              controls="mentor-import-panel"
              onClick={() => setActivePanel(current => current === 'import' ? null : 'import')}
            />
            <ActionToggle
              active={activePanel === 'allocation'}
              icon={Users}
              title="Assign mentors"
              description="Keep mentors of continuing teams, then fill what is missing"
              controls="mentor-allocation-panel"
              onClick={() => setActivePanel(current => current === 'allocation' ? null : 'allocation')}
            />
          </div>

          {activePanel === 'import' && (
            <div id="mentor-import-panel" className="mt-3 rounded-xl border border-slate-200 bg-slate-50/50 p-4">
              <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
                <div className="min-w-0">
                  <h3 className="text-sm font-semibold text-slate-800">Import semester mentor list</h3>
                  <p className="mt-1 text-xs leading-5 text-slate-500">Required sheets: DS Mentor_FA26 (enterprise) and Mentor IT_FA26 (academic). Accepts .xlsx files up to 5 MB.</p>
                </div>
                <div className="flex min-w-0 flex-col gap-2 sm:flex-row lg:w-[520px]">
                  <input ref={fileInput} type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={event => { setFile(event.target.files?.[0] ?? null); setImportPreview(null); }} className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm" aria-label="Mentor workbook" />
                  <Button size="sm" icon={Upload} disabled={!file} onClick={() => void previewImport()} isLoading={busy === 'preview-import'}>Preview</Button>
                </div>
              </div>
              {importPreview && (
                <div className="mt-4 space-y-3 border-t border-slate-200 pt-4">
                  <div className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-3 lg:grid-cols-6">
                    <Metric label="New" value={importPreview.createCount} />
                    <Metric label="Update" value={importPreview.updateCount} />
                    <Metric label="Add to semester" value={importPreview.addToSemesterCount} />
                    <Metric label="Needs information" value={importPreview.needsCompletionCount} warning={importPreview.needsCompletionCount > 0} />
                    <Metric label="Complete draft" value={importPreview.completeDraftCount} />
                    <Metric label="Errors" value={importPreview.errorCount} danger={importPreview.errorCount > 0} />
                  </div>
                  <div className="max-h-52 overflow-auto rounded-lg border border-slate-200 bg-white">
                    <table className="w-full min-w-[680px] text-left text-xs"><thead className="sticky top-0 bg-slate-50 text-slate-500"><tr><th className="px-3 py-2">Sheet / row</th><th className="px-3 py-2">Type</th><th className="px-3 py-2">Mentor</th><th className="px-3 py-2">Status</th></tr></thead><tbody>{importPreview.rows.map(row => {
                      const incomplete = row.status === 'NeedsCompletion' || row.status === 'UpdateIncomplete';
                      return <tr key={`${row.sheetName}-${row.rowNumber}`} className="border-t border-slate-100 align-top"><td className="px-3 py-2">{row.sheetName} · {row.rowNumber}</td><td className="px-3 py-2">{row.mentorType}</td><td className="px-3 py-2"><span className="block font-semibold">{row.fullName}</span><span className={row.email ? 'text-slate-400' : 'font-medium text-amber-600'}>{row.email || 'No email yet'}</span>{row.missingFields.length > 0 && <span className="mt-1 block max-w-sm text-[11px] text-slate-400">Missing: {row.missingFields.join(', ')}</span>}</td><td className={`px-3 py-2 ${!row.isValid ? 'text-red-700' : incomplete ? 'text-amber-700' : 'text-green-700'}`}><span className="block font-semibold">{incomplete ? 'Needs information' : row.status}</span>{row.message && <span className="mt-1 block max-w-xs text-[11px] leading-4 text-slate-500">{row.message}</span>}</td></tr>;
                    })}</tbody></table>
                  </div>
                  <div className="flex justify-end">
                    <Button size="sm" disabled={!importPreview.canCommit} onClick={() => void commitImport()} isLoading={busy === 'commit-import'}>Commit import</Button>
                  </div>
                </div>
              )}
            </div>
          )}

          {activePanel === 'allocation' && (
            <div id="mentor-allocation-panel" className="mt-3 rounded-xl border border-slate-200 bg-slate-50/50 p-4">
              <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-slate-800">Assign mentors to teams</h3>
                  <p className="mt-1 max-w-3xl text-xs leading-5 text-slate-500">
                    Mentors of teams that continue from the previous semester stay with them when they are active. Only missing industry or lecturer slots are filled, existing mentors are never replaced automatically, and nothing is saved before you confirm the preview.
                  </p>
                </div>
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
                  <div role="group" aria-label="Assignment method" className="inline-flex rounded-xl border border-slate-200 bg-white p-0.5">
                    {(['Balanced', 'Random'] as const).map(option => (
                      <button
                        key={option}
                        type="button"
                        aria-pressed={strategy === option}
                        disabled={busy !== null}
                        onClick={() => setStrategy(option)}
                        className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors ${strategy === option ? 'bg-primary text-white' : 'text-slate-600 hover:text-slate-900'}`}
                      >
                        {option}{option === 'Balanced' ? ' (recommended)' : ''}
                      </button>
                    ))}
                  </div>
                  <Button size="sm" className="shrink-0" icon={Shuffle} onClick={() => void previewAllocation()} isLoading={busy === 'preview-allocation'}>Preview assignment</Button>
                </div>
              </div>
              <p className="mt-2 text-xs text-slate-500">
                {strategy === 'Balanced'
                  ? 'Balanced gives each missing slot to the mentor with the fewest teams in this semester.'
                  : 'Random picks any eligible mentor for each missing slot, regardless of how many teams they already have.'}
              </p>
            </div>
          )}

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

function ActionToggle({
  active,
  icon: Icon,
  title,
  description,
  controls,
  onClick,
}: {
  active: boolean;
  icon: typeof FileSpreadsheet;
  title: string;
  description: string;
  controls: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      aria-expanded={active}
      aria-controls={controls}
      onClick={onClick}
      className={`group flex w-full items-center gap-3 rounded-xl border px-3.5 py-2.5 text-left transition-colors ${active ? 'border-primary-300 bg-primary-50/70 dark:border-primary/50 dark:bg-primary/15' : 'border-slate-200 bg-slate-50/60 hover:border-primary-200 hover:bg-white dark:border-white/12 dark:bg-white/[0.035] dark:hover:border-primary/40 dark:hover:bg-white/[0.06]'}`}
    >
      <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg shadow-sm ring-1 ring-inset ${active ? 'bg-primary text-white ring-primary/50' : 'bg-white text-slate-600 ring-slate-200 group-hover:text-primary dark:bg-slate-700 dark:text-slate-100 dark:ring-white/15'}`}>
        <Icon className="h-4 w-4" />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-semibold text-slate-800">{title}</span>
        <span className="block truncate text-xs text-slate-500">{description}</span>
      </span>
      <ChevronDown className={`h-4 w-4 shrink-0 text-slate-400 transition-transform ${active ? 'rotate-180 text-primary' : ''}`} />
    </button>
  );
}

function Metric({ label, value, danger = false, warning = false }: { label: string; value: number; danger?: boolean; warning?: boolean }) {
  return <div className={`rounded-lg px-3 py-2 ${danger ? 'bg-red-50 text-red-700' : warning ? 'bg-amber-50 text-amber-700' : 'bg-slate-50 text-slate-700'}`}><span className="block text-[10px] uppercase text-slate-400">{label}</span><strong className="text-base">{value}</strong></div>;
}
