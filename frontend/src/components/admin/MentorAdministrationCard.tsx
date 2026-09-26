import { useRef, useState } from 'react';
import { ChevronDown, Download, FileSpreadsheet, RefreshCw, Shuffle, Upload, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { mentorAdminApi } from '../../api/mentorAdminApi';
import type { MentorAllocationPreview, MentorImportPreview } from '../../types/mentorAdmin';
import { parseApiError } from '../../utils/apiError';
import Button from '../ui/Button';

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
  const [busy, setBusy] = useState<'template' | 'preview-import' | 'commit-import' | 'preview-allocation' | 'commit-allocation' | null>(null);

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
      toast.success(`Imported ${response.data.createdCount} new and updated ${response.data.updatedCount} mentor accounts. New mentors can use Forgot Password to set their first password.`);
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

  const previewAllocation = async () => {
    if (!semesterId) return;
    setBusy('preview-allocation');
    setAllocationPreview(null);
    try {
      const response = await mentorAdminApi.previewAllocation(semesterId);
      setAllocationPreview(response.data);
      if (response.data.warnings.length) response.data.warnings.forEach(message => toast.error(message));
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to preview balanced allocation').message);
    } finally {
      setBusy(null);
    }
  };

  const commitAllocation = async () => {
    if (!allocationPreview?.canCommit) return;
    setBusy('commit-allocation');
    try {
      const response = await mentorAdminApi.commitAllocation(allocationPreview.sessionId);
      toast.success(`Assigned ${response.data.createdCount} missing mentor slots.`);
      setAllocationPreview(null);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to commit balanced allocation').message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <section className="rounded-2xl border border-primary-100 bg-white p-4 shadow-sm">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary">
            <FileSpreadsheet className="h-4.5 w-4.5" />
          </span>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="font-bold text-slate-900">Mentor import & balanced assignment</h2>
              <span className="rounded-full bg-primary-50 px-2 py-0.5 text-[11px] font-semibold text-primary">{semesterLabel}</span>
            </div>
            <p className="mt-0.5 text-xs text-slate-500">Import mentor rosters or fill missing team mentor slots.</p>
          </div>
        </div>
        <Button size="sm" variant="outline" icon={Download} onClick={() => void downloadTemplate()} isLoading={busy === 'template'}>Download template</Button>
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
              title="Balanced assignment"
              description="Fill only the mentor slots still missing"
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
                  <div className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-4">
                    <Metric label="New" value={importPreview.createCount} />
                    <Metric label="Update" value={importPreview.updateCount} />
                    <Metric label="Add to semester" value={importPreview.addToSemesterCount} />
                    <Metric label="Errors" value={importPreview.errorCount} danger={importPreview.errorCount > 0} />
                  </div>
                  <div className="max-h-52 overflow-auto rounded-lg border border-slate-200 bg-white">
                    <table className="w-full min-w-[560px] text-left text-xs"><thead className="sticky top-0 bg-slate-50 text-slate-500"><tr><th className="px-3 py-2">Sheet / row</th><th className="px-3 py-2">Type</th><th className="px-3 py-2">Mentor</th><th className="px-3 py-2">Status</th></tr></thead><tbody>{importPreview.rows.map(row => <tr key={`${row.sheetName}-${row.rowNumber}`} className="border-t border-slate-100"><td className="px-3 py-2">{row.sheetName} · {row.rowNumber}</td><td className="px-3 py-2">{row.mentorType}</td><td className="px-3 py-2"><span className="block font-semibold">{row.fullName}</span><span className="text-slate-400">{row.email}</span></td><td className={`px-3 py-2 ${row.isValid ? 'text-green-700' : 'text-red-700'}`} title={row.message ?? undefined}>{row.status}</td></tr>)}</tbody></table>
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
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-slate-800">Fill missing mentor slots</h3>
                  <p className="mt-1 max-w-3xl text-xs leading-5 text-slate-500">Existing assignments are preserved. Only missing Enterprise or Academic slots in active classes are filled with balanced loads.</p>
                </div>
                <Button size="sm" className="shrink-0" variant="outline" icon={allocationPreview ? RefreshCw : Shuffle} onClick={() => void previewAllocation()} isLoading={busy === 'preview-allocation'}>{allocationPreview ? 'Generate another preview' : 'Preview assignment'}</Button>
              </div>
              {allocationPreview && (
                <div className="mt-4 space-y-3 border-t border-slate-200 pt-4">
                  <div className="grid grid-cols-3 gap-2 text-xs">
                    <Metric label="Teams" value={allocationPreview.teamCount} />
                    <Metric label="Enterprise" value={allocationPreview.missingEnterpriseCount} />
                    <Metric label="Academic" value={allocationPreview.missingAcademicCount} />
                  </div>
                  <p className="text-xs text-slate-400">Reproducible random seed: {allocationPreview.seed}</p>
                  <div className="max-h-52 overflow-auto rounded-lg border border-slate-200 bg-white">
                    <table className="w-full min-w-[520px] text-left text-xs"><thead className="sticky top-0 bg-slate-50 text-slate-500"><tr><th className="px-3 py-2">Team</th><th className="px-3 py-2">Slot</th><th className="px-3 py-2">Mentor</th><th className="px-3 py-2">Load</th></tr></thead><tbody>{allocationPreview.assignments.map(row => <tr key={`${row.teamId}-${row.mentorType}`} className="border-t border-slate-100"><td className="px-3 py-2"><span className="block font-semibold">{row.teamCode}</span><span className="text-slate-400">{row.classCode}</span></td><td className="px-3 py-2">{row.mentorType}</td><td className="px-3 py-2">{row.mentorName}</td><td className="px-3 py-2">{row.resultingSemesterLoad}</td></tr>)}</tbody></table>
                  </div>
                  <div className="flex justify-end">
                    <Button size="sm" disabled={!allocationPreview.canCommit} onClick={() => void commitAllocation()} isLoading={busy === 'commit-allocation'}>Commit balanced assignment</Button>
                  </div>
                </div>
              )}
            </div>
          )}
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
      className={`group flex w-full items-center gap-3 rounded-xl border px-3.5 py-2.5 text-left transition-colors ${active ? 'border-primary-300 bg-primary-50/70' : 'border-slate-200 bg-slate-50/60 hover:border-primary-200 hover:bg-white'}`}
    >
      <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${active ? 'bg-primary text-white' : 'bg-white text-slate-500 shadow-sm group-hover:text-primary'}`}>
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

function Metric({ label, value, danger = false }: { label: string; value: number; danger?: boolean }) {
  return <div className={`rounded-lg px-3 py-2 ${danger ? 'bg-red-50 text-red-700' : 'bg-slate-50 text-slate-700'}`}><span className="block text-[10px] uppercase text-slate-400">{label}</span><strong className="text-base">{value}</strong></div>;
}
