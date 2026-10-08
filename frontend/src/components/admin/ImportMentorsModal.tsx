import { useEffect, useRef, useState } from 'react';
import {
  AlertCircle,
  ArrowLeft,
  Check,
  CheckCircle2,
  Download,
  FileSpreadsheet,
  Info,
  Loader2,
  RotateCcw,
  Upload,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { mentorAdminApi } from '../../api/mentorAdminApi';
import type { MentorImportCommitResult, MentorImportPreview } from '../../types/mentorAdmin';
import { parseApiError } from '../../utils/apiError';
import {
  countMasterImportChanges,
  describeMasterImportResult,
  emailCellText,
  validateMentorWorkbook,
} from '../../utils/mentorMasterImport';
import Button from '../ui/Button';

interface ImportMentorsModalProps {
  onClose: () => void;
  onImported: () => void;
}

type ImportPhase = 'upload' | 'review' | 'result';

const steps = [
  { key: 'upload', label: 'Select file' },
  { key: 'review', label: 'Review data' },
  { key: 'result', label: 'Import result' },
] as const;

export default function ImportMentorsModal({ onClose, onImported }: ImportMentorsModalProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [phase, setPhase] = useState<ImportPhase>('upload');
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<MentorImportPreview | null>(null);
  const [result, setResult] = useState<MentorImportCommitResult | null>(null);
  const [error, setError] = useState('');
  const [analyzing, setAnalyzing] = useState(false);
  const [importing, setImporting] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [dragActive, setDragActive] = useState(false);

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !importing) onClose();
    };
    window.addEventListener('keydown', handleEscape);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener('keydown', handleEscape);
    };
  }, [importing, onClose]);

  const reset = () => {
    setPhase('upload');
    setFile(null);
    setPreview(null);
    setResult(null);
    setError('');
    setDragActive(false);
    if (inputRef.current) inputRef.current.value = '';
  };

  const downloadTemplate = async () => {
    setDownloading(true);
    try {
      const blob = await mentorAdminApi.downloadTemplate();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'Danh_sach_Mentor_mau.xlsx';
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (requestError: unknown) {
      toast.error(parseApiError(requestError, 'Failed to download mentor template').message);
    } finally {
      setDownloading(false);
    }
  };

  const inspectFile = async (selectedFile?: File) => {
    if (!selectedFile) return;
    const validationError = validateMentorWorkbook(selectedFile);
    if (validationError) {
      setFile(null);
      setPreview(null);
      setError(validationError);
      return;
    }

    setFile(selectedFile);
    setError('');
    setAnalyzing(true);
    try {
      const response = await mentorAdminApi.previewMasterImport(selectedFile);
      setPreview(response.data);
      setPhase('review');
    } catch (requestError: unknown) {
      setError(parseApiError(requestError, 'The mentor file could not be analyzed.').message);
    } finally {
      setAnalyzing(false);
    }
  };

  const commit = async () => {
    if (!preview?.canCommit || importing) return;
    setImporting(true);
    try {
      const response = await mentorAdminApi.commitImport(preview.sessionId);
      setResult(response.data);
      setPhase('result');
      onImported();
      toast.success(describeMasterImportResult(response.data));
    } catch (requestError: unknown) {
      toast.error(parseApiError(requestError, 'Failed to import mentors.').message);
    } finally {
      setImporting(false);
    }
  };

  const currentStep = steps.findIndex((step) => step.key === phase);
  const changeCount = preview ? countMasterImportChanges(preview) : 0;

  return (
    <div className="fixed inset-0 z-[70] flex items-end justify-center p-0 sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby="import-mentors-title">
      <button type="button" className="absolute inset-0 cursor-default bg-slate-900/45 backdrop-blur-sm" onClick={importing ? undefined : onClose} aria-label="Close import dialog" />

      <div className="relative flex max-h-[94vh] w-full max-w-5xl flex-col overflow-hidden rounded-t-2xl border border-slate-200/60 bg-white shadow-float animate-scale-in sm:max-h-[90vh] sm:rounded-2xl">
        <header className="shrink-0 border-b border-slate-100 px-5 py-4 sm:px-6">
          <div className="flex items-start justify-between gap-4">
            <div className="flex min-w-0 items-center gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary"><FileSpreadsheet className="h-5 w-5" /></div>
              <div>
                <h2 id="import-mentors-title" className="text-lg font-bold text-slate-900">Import Mentor accounts</h2>
                <p className="text-sm text-slate-500">Add mentors to the master list. Missing information is allowed and can be completed later.</p>
              </div>
            </div>
            <button type="button" onClick={onClose} disabled={importing} className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 disabled:opacity-50" aria-label="Close"><X className="h-5 w-5" /></button>
          </div>

          <ol className="mt-4 grid grid-cols-3 gap-2" aria-label="Import progress">
            {steps.map((step, index) => (
              <li key={step.key} className="flex min-w-0 items-center gap-2">
                <span className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-bold ${index <= currentStep ? 'bg-primary text-white' : 'bg-slate-100 text-slate-400'}`}>
                  {index < currentStep ? <Check className="h-3.5 w-3.5" /> : index + 1}
                </span>
                <span className={`truncate text-xs font-semibold sm:text-sm ${index === currentStep ? 'text-slate-900' : 'text-slate-400'}`}>{step.label}</span>
                {index < steps.length - 1 && <span className="hidden h-px flex-1 bg-slate-200 sm:block" />}
              </li>
            ))}
          </ol>
        </header>

        <main className="flex-1 overflow-y-auto px-5 py-5 sm:px-6">
          {phase === 'upload' && (
            <div className="space-y-4">
              <div className="flex items-start gap-2.5 rounded-xl border border-blue-200 bg-blue-50 p-3.5 text-xs leading-5 text-slate-700">
                <Info className="mt-0.5 h-4 w-4 shrink-0 text-blue-600" />
                <div className="space-y-1">
                  <p><strong>Use the mentor workbook template.</strong> Required sheets: DS Mentor_FA26 (enterprise mentors) and Mentor IT_FA26 (lecturer mentors).</p>
                  <p>Only the mentor name is required. Mentors without an email are saved as <strong>incomplete</strong>; import a file with their email later to create the account. Existing mentors with the same email are updated.</p>
                  <p>Nobody is added to a semester here — use Add mentors in Subject Management.</p>
                  <p>Limits: Excel .xlsx · maximum 5 MB.</p>
                  <button type="button" onClick={() => void downloadTemplate()} disabled={downloading} className="mt-1 inline-flex items-center gap-1.5 font-semibold text-primary disabled:opacity-60">
                    {downloading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />} Download template
                  </button>
                </div>
              </div>

              <div
                onDragEnter={() => setDragActive(true)}
                onDragLeave={() => setDragActive(false)}
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => { event.preventDefault(); setDragActive(false); void inspectFile(event.dataTransfer.files?.[0]); }}
                className={`rounded-2xl border-2 border-dashed px-5 py-10 text-center transition-colors ${error ? 'border-red-300 bg-red-50/60' : dragActive || file ? 'border-primary bg-primary-50' : 'border-slate-300 hover:border-primary hover:bg-primary-50/40'}`}
              >
                <input ref={inputRef} type="file" accept=".xlsx" className="hidden" aria-label="Mentor workbook" onChange={(event) => { void inspectFile(event.target.files?.[0]); event.target.value = ''; }} />
                <div className={`mx-auto flex h-12 w-12 items-center justify-center rounded-2xl ${error ? 'bg-red-100 text-red-600' : 'bg-primary-50 text-primary'}`}>
                  {analyzing ? <Loader2 className="h-6 w-6 animate-spin" /> : error ? <AlertCircle className="h-6 w-6" /> : <Upload className="h-6 w-6" />}
                </div>
                <p className="mt-4 text-sm font-semibold text-slate-800">{analyzing ? `Analyzing ${file?.name}` : error ? 'This file cannot be used' : 'Drop the mentor workbook here'}</p>
                <p className={`mx-auto mt-1 max-w-xl text-xs ${error ? 'text-red-600' : 'text-slate-500'}`}>{error || 'The file is inspected safely before any account is created.'}</p>
                {!analyzing && <Button variant="outline" size="sm" className="mt-4" onClick={() => inputRef.current?.click()}>{error ? 'Choose another file' : 'Browse files'}</Button>}
              </div>
            </div>
          )}

          {phase === 'review' && preview && (
            <div className="space-y-4">
              <div className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3.5">
                <div className="min-w-0"><p className="truncate text-sm font-semibold text-slate-800">{file?.name}</p><p className="text-xs text-slate-500">{preview.totalRows} mentor row(s)</p></div>
                <button type="button" onClick={reset} className="flex shrink-0 items-center gap-1.5 text-xs font-semibold text-primary"><RotateCcw className="h-3.5 w-3.5" /> Choose another file</button>
              </div>

              <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-5">
                <SummaryCard label="Total" value={preview.totalRows} tone="neutral" />
                <SummaryCard label="Create" value={preview.createCount} tone="success" />
                <SummaryCard label="Update" value={preview.updateCount} tone="warning" />
                <SummaryCard label="Incomplete" value={preview.needsCompletionCount} tone="warning" />
                <SummaryCard label="Errors" value={preview.errorCount} tone="danger" />
              </div>

              {preview.needsCompletionCount > 0 && <Notice text={`${preview.needsCompletionCount} mentor(s) have no login email yet. They will be saved as incomplete and no account is created until an email is provided.`} />}
              {preview.errorCount > 0 && <Notice text="Resolve every invalid row in the source file, then preview again. No account has been changed." />}
              {!preview.canCommit && preview.errorCount === 0 && <Notice text="There is nothing to import from this file." />}
              <MentorRowsTable rows={preview.rows} />
            </div>
          )}

          {phase === 'result' && result && (
            <div className="rounded-2xl border border-green-200 bg-green-50 p-5 text-center">
              <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-green-100 text-green-600"><CheckCircle2 className="h-6 w-6" /></div>
              <h3 className="mt-3 text-lg font-bold text-slate-900">Mentor import completed</h3>
              <p className="mt-1 text-sm text-slate-600">Created {result.createdCount}, updated {result.updatedCount}, saved as incomplete {result.draftSavedCount}, completed {result.draftCompletedCount}.</p>
              {result.draftSavedCount > 0 && <p className="mt-2 text-xs text-slate-500">Incomplete mentors are listed in the Needs information tab of User Management. Import a file with their email to create their accounts.</p>}
              {result.createdCount > 0 && <p className="mt-2 text-xs text-slate-500">New accounts can use Forgot Password to set their first password. Add them to a semester from Subject Management.</p>}
            </div>
          )}
        </main>

        <footer className="flex shrink-0 flex-col-reverse gap-2 border-t border-slate-100 bg-slate-50/60 px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
          {phase === 'upload' && <Button variant="outline" onClick={onClose}>Cancel</Button>}
          {phase === 'review' && <><Button variant="outline" icon={ArrowLeft} onClick={reset} disabled={importing}>Back</Button><Button variant="gradient" icon={Upload} isLoading={importing} disabled={!preview?.canCommit} onClick={() => void commit()}>Import {changeCount} mentor(s)</Button></>}
          {phase === 'result' && <Button variant="gradient" icon={Check} onClick={onClose}>Done</Button>}
        </footer>
      </div>
    </div>
  );
}

function SummaryCard({ label, value, tone }: { label: string; value: number; tone: 'neutral' | 'success' | 'danger' | 'warning' }) {
  const styles = { neutral: 'border-slate-200 bg-slate-50 text-slate-900', success: 'border-green-200 bg-green-50 text-green-700', danger: 'border-red-200 bg-red-50 text-red-600', warning: 'border-orange-200 bg-orange-50 text-orange-700' };
  return <div className={`rounded-xl border p-3 text-center ${styles[tone]}`}><p className="text-xl font-bold">{value}</p><p className="mt-0.5 text-xs font-medium">{label}</p></div>;
}

function Notice({ text }: { text: string }) {
  return <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3.5 text-sm text-amber-800"><AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /><p>{text}</p></div>;
}

function MentorRowsTable({ rows }: { rows: MentorImportPreview['rows'] }) {
  return (
    <div className="overflow-hidden rounded-xl border border-slate-200"><div className="max-h-80 overflow-auto">
      <table className="w-full min-w-[680px] text-left text-xs">
        <thead className="sticky top-0 z-10 bg-slate-50 text-slate-500"><tr><th className="px-3 py-2.5">Sheet / row</th><th className="px-3 py-2.5">Type</th><th className="px-3 py-2.5">Mentor</th><th className="px-3 py-2.5">Result</th></tr></thead>
        <tbody className="divide-y divide-slate-100 bg-white">{rows.map((row) => (
          <tr key={`${row.sheetName}-${row.rowNumber}`} className={!row.isValid ? 'bg-red-50/50' : 'hover:bg-slate-50'}>
            <td className="px-3 py-3 text-slate-600">{row.sheetName} · {row.rowNumber}</td>
            <td className="px-3 py-3 text-slate-600">{row.mentorType === 'Enterprise' ? 'Enterprise' : 'Lecturer'}</td>
            <td className="px-3 py-3"><span className="block font-semibold text-slate-700">{row.fullName || '—'}</span><span className={emailCellText(row.email).provided ? 'text-slate-400' : 'font-medium text-amber-600'}>{emailCellText(row.email).text}</span>{row.missingFields.length > 0 && <span className="mt-1 block max-w-sm text-[11px] text-slate-400">Missing: {row.missingFields.join(', ')}</span>}</td>
            <td className={`px-3 py-3 ${row.isValid ? 'text-green-700' : 'text-red-700'}`}><span className="block font-semibold">{!row.isValid ? 'Invalid' : row.status === 'NeedsCompletion' || row.status === 'UpdateIncomplete' ? 'Incomplete' : row.status === 'CompleteIncomplete' ? 'Complete record' : row.status}</span>{row.message && <span className="mt-1 block max-w-xs text-[11px] leading-4 text-slate-500">{row.message}</span>}</td>
          </tr>
        ))}</tbody>
      </table>
    </div></div>
  );
}
