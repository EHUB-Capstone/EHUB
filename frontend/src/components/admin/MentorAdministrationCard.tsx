import { useRef, useState } from 'react';
import { Download, FileSpreadsheet, RefreshCw, Shuffle, Upload } from 'lucide-react';
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
    <section className="rounded-2xl border border-primary-100 bg-white p-5 shadow-sm">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h2 className="flex items-center gap-2 font-bold text-slate-900"><FileSpreadsheet className="h-5 w-5 text-primary" /> Mentor import & balanced assignment</h2>
          <p className="mt-1 text-sm text-slate-500">Selected semester: <strong>{semesterLabel}</strong>. Import always validates both required sheets before saving.</p>
        </div>
        <Button variant="outline" icon={Download} onClick={() => void downloadTemplate()} isLoading={busy === 'template'}>Download template</Button>
      </div>

      {!semesterId ? (
        <p className="mt-4 rounded-xl bg-amber-50 p-3 text-sm text-amber-800">This semester has not been planned in the system yet.</p>
      ) : (
        <div className="mt-5 grid gap-5 xl:grid-cols-2">
          <div className="rounded-xl border border-slate-200 p-4">
            <h3 className="font-semibold text-slate-800">1. Import semester mentor list</h3>
            <p className="mt-1 text-xs leading-5 text-slate-500">Required sheets: DS Mentor_FA26 (enterprise) and Mentor IT_FA26 (academic). Only .xlsx files up to 5 MB are accepted.</p>
            <div className="mt-3 flex flex-col gap-2 sm:flex-row">
              <input ref={fileInput} type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={event => { setFile(event.target.files?.[0] ?? null); setImportPreview(null); }} className="min-w-0 flex-1 rounded-xl border border-slate-200 px-3 py-2 text-sm" aria-label="Mentor workbook" />
              <Button icon={Upload} disabled={!file} onClick={() => void previewImport()} isLoading={busy === 'preview-import'}>Preview</Button>
            </div>
            {importPreview && (
              <div className="mt-4 space-y-3">
                <div className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-4">
                  <Metric label="New" value={importPreview.createCount} />
                  <Metric label="Update" value={importPreview.updateCount} />
                  <Metric label="Add to semester" value={importPreview.addToSemesterCount} />
                  <Metric label="Errors" value={importPreview.errorCount} danger={importPreview.errorCount > 0} />
                </div>
                <div className="max-h-52 overflow-auto rounded-lg border border-slate-100">
                  <table className="w-full min-w-[560px] text-left text-xs"><thead className="sticky top-0 bg-slate-50 text-slate-500"><tr><th className="px-3 py-2">Sheet / row</th><th className="px-3 py-2">Type</th><th className="px-3 py-2">Mentor</th><th className="px-3 py-2">Status</th></tr></thead><tbody>{importPreview.rows.map(row => <tr key={`${row.sheetName}-${row.rowNumber}`} className="border-t border-slate-100"><td className="px-3 py-2">{row.sheetName} · {row.rowNumber}</td><td className="px-3 py-2">{row.mentorType}</td><td className="px-3 py-2"><span className="block font-semibold">{row.fullName}</span><span className="text-slate-400">{row.email}</span></td><td className={`px-3 py-2 ${row.isValid ? 'text-green-700' : 'text-red-700'}`} title={row.message ?? undefined}>{row.status}</td></tr>)}</tbody></table>
                </div>
                <Button className="w-full" disabled={!importPreview.canCommit} onClick={() => void commitImport()} isLoading={busy === 'commit-import'}>Commit import</Button>
              </div>
            )}
          </div>

          <div className="rounded-xl border border-slate-200 p-4">
            <h3 className="font-semibold text-slate-800">2. Fill missing mentor slots</h3>
            <p className="mt-1 text-xs leading-5 text-slate-500">Uses every active class in the selected semester. Existing assignments are preserved; only missing Enterprise or Academic slots are filled with balanced loads.</p>
            <Button className="mt-3 w-full" variant="outline" icon={allocationPreview ? RefreshCw : Shuffle} onClick={() => void previewAllocation()} isLoading={busy === 'preview-allocation'}>{allocationPreview ? 'Generate another preview' : 'Preview balanced assignment'}</Button>
            {allocationPreview && (
              <div className="mt-4 space-y-3">
                <div className="grid grid-cols-3 gap-2 text-xs">
                  <Metric label="Teams" value={allocationPreview.teamCount} />
                  <Metric label="Enterprise" value={allocationPreview.missingEnterpriseCount} />
                  <Metric label="Academic" value={allocationPreview.missingAcademicCount} />
                </div>
                <p className="text-xs text-slate-400">Reproducible random seed: {allocationPreview.seed}</p>
                <div className="max-h-52 overflow-auto rounded-lg border border-slate-100">
                  <table className="w-full min-w-[520px] text-left text-xs"><thead className="sticky top-0 bg-slate-50 text-slate-500"><tr><th className="px-3 py-2">Team</th><th className="px-3 py-2">Slot</th><th className="px-3 py-2">Mentor</th><th className="px-3 py-2">Load</th></tr></thead><tbody>{allocationPreview.assignments.map(row => <tr key={`${row.teamId}-${row.mentorType}`} className="border-t border-slate-100"><td className="px-3 py-2"><span className="block font-semibold">{row.teamCode}</span><span className="text-slate-400">{row.classCode}</span></td><td className="px-3 py-2">{row.mentorType}</td><td className="px-3 py-2">{row.mentorName}</td><td className="px-3 py-2">{row.resultingSemesterLoad}</td></tr>)}</tbody></table>
                </div>
                <Button className="w-full" disabled={!allocationPreview.canCommit} onClick={() => void commitAllocation()} isLoading={busy === 'commit-allocation'}>Commit balanced assignment</Button>
              </div>
            )}
          </div>
        </div>
      )}
    </section>
  );
}

function Metric({ label, value, danger = false }: { label: string; value: number; danger?: boolean }) {
  return <div className={`rounded-lg px-3 py-2 ${danger ? 'bg-red-50 text-red-700' : 'bg-slate-50 text-slate-700'}`}><span className="block text-[10px] uppercase text-slate-400">{label}</span><strong className="text-base">{value}</strong></div>;
}
