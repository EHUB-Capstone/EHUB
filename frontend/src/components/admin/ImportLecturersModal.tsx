import { useEffect, useRef, useState } from 'react';
import {
  AlertCircle,
  ArrowLeft,
  Check,
  CheckCircle2,
  FileSpreadsheet,
  Info,
  Loader2,
  RotateCcw,
  Upload,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { userApi } from '../../api/userApi';
import { parseApiError } from '../../utils/apiError';
import Button from '../ui/Button';

interface ImportLecturersModalProps {
  onClose: () => void;
  onImported: () => void;
}

interface LecturerImportRow {
  rowNumber: number;
  fullName: string;
  position?: string | null;
  contactEmail?: string | null;
  googleEmail: string;
  status: 'Ready' | 'WillActivate' | 'AlreadyExists' | 'Invalid' | 'Conflict';
  isValid: boolean;
  message?: string | null;
}

interface LecturerImportPreview {
  sessionId: string;
  totalRows: number;
  readyCount: number;
  willActivateCount: number;
  existingCount: number;
  errorCount: number;
  canCommit: boolean;
  rows: LecturerImportRow[];
}

interface LecturerImportCommitError {
  rowNumber: number;
  googleEmail: string;
  errorMessage: string;
}

interface LecturerImportCommitResult {
  createdCount: number;
  activatedCount: number;
  skippedCount: number;
  errorCount: number;
  errors: LecturerImportCommitError[];
}

type ImportPhase = 'upload' | 'review' | 'result';

const steps = [
  { key: 'upload', label: 'Select file' },
  { key: 'review', label: 'Review data' },
  { key: 'result', label: 'Import result' },
] as const;

const maximumFileSize = 5 * 1024 * 1024;

function unwrap<T>(response: unknown): T {
  const value = response as { data?: unknown };
  const first = value?.data ?? response;
  const nested = first as { data?: unknown };
  return (nested?.data ?? first) as T;
}

function validateFile(file: File): string | null {
  const extension = file.name.split('.').pop()?.toLowerCase();
  if (extension !== 'xlsx' && extension !== 'xls') return 'Only .xlsx and .xls Excel files are accepted.';
  if (file.size === 0) return 'The selected file is empty.';
  if (file.size > maximumFileSize) return 'The file may not exceed 5 MB.';
  return null;
}

export default function ImportLecturersModal({ onClose, onImported }: ImportLecturersModalProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [phase, setPhase] = useState<ImportPhase>('upload');
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<LecturerImportPreview | null>(null);
  const [result, setResult] = useState<LecturerImportCommitResult | null>(null);
  const [error, setError] = useState('');
  const [analyzing, setAnalyzing] = useState(false);
  const [importing, setImporting] = useState(false);
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

  const inspectFile = async (selectedFile?: File) => {
    if (!selectedFile) return;
    const validationError = validateFile(selectedFile);
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
      const formData = new FormData();
      formData.append('file', selectedFile);
      const response = await userApi.previewLecturerImport(formData);
      setPreview(unwrap<LecturerImportPreview>(response));
      setPhase('review');
    } catch (requestError: unknown) {
      setError(parseApiError(requestError, 'The lecturer file could not be analyzed.').message);
    } finally {
      setAnalyzing(false);
    }
  };

  const commit = async () => {
    if (!preview?.canCommit || !preview.sessionId) return;
    setImporting(true);
    try {
      const response = await userApi.commitLecturerImport({ sessionId: preview.sessionId });
      const commitResult = unwrap<LecturerImportCommitResult>(response);
      setResult(commitResult);
      setPhase('result');
      onImported();
      toast.success(`Created ${commitResult.createdCount} and activated ${commitResult.activatedCount} Lecturer account(s).`);
    } catch (requestError: unknown) {
      toast.error(parseApiError(requestError, 'Failed to import Lecturer accounts.').message);
    } finally {
      setImporting(false);
    }
  };

  const currentStep = steps.findIndex((step) => step.key === phase);
  const changedCount = (result?.createdCount ?? 0) + (result?.activatedCount ?? 0);

  return (
    <div className="fixed inset-0 z-[70] flex items-end justify-center p-0 sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby="import-lecturers-title">
      <button type="button" className="absolute inset-0 cursor-default bg-slate-900/45 backdrop-blur-sm" onClick={importing ? undefined : onClose} aria-label="Close import dialog" />

      <div className="relative flex max-h-[94vh] w-full max-w-5xl flex-col overflow-hidden rounded-t-2xl border border-slate-200/60 bg-white shadow-float animate-scale-in sm:max-h-[90vh] sm:rounded-2xl">
        <header className="shrink-0 border-b border-slate-100 px-5 py-4 sm:px-6">
          <div className="flex items-start justify-between gap-4">
            <div className="flex min-w-0 items-center gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary"><FileSpreadsheet className="h-5 w-5" /></div>
              <div>
                <h2 id="import-lecturers-title" className="text-lg font-bold text-slate-900">Import Lecturer accounts</h2>
                <p className="text-sm text-slate-500">Validate the Excel file before creating active accounts</p>
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
                <div>
                  <p><strong>Use the lecturer list without changing its layout.</strong> Required information: lecturer name, a valid login email, and role Lecturer.</p>
                  <p>For the supplied legacy layout, column E is preferred for Google login; column D is used as fallback. Any valid email domain is accepted.</p>
                  <p>Limits: Excel .xlsx/.xls · maximum 5 MB · maximum 500 lecturer rows.</p>
                </div>
              </div>

              <div
                onDragEnter={() => setDragActive(true)}
                onDragLeave={() => setDragActive(false)}
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => { event.preventDefault(); setDragActive(false); void inspectFile(event.dataTransfer.files?.[0]); }}
                className={`rounded-2xl border-2 border-dashed px-5 py-10 text-center transition-colors ${error ? 'border-red-300 bg-red-50/60' : dragActive || file ? 'border-primary bg-primary-50' : 'border-slate-300 hover:border-primary hover:bg-primary-50/40'}`}
              >
                <input ref={inputRef} type="file" accept=".xlsx,.xls" className="hidden" onChange={(event) => { void inspectFile(event.target.files?.[0]); event.target.value = ''; }} />
                <div className={`mx-auto flex h-12 w-12 items-center justify-center rounded-2xl ${error ? 'bg-red-100 text-red-600' : 'bg-primary-50 text-primary'}`}>
                  {analyzing ? <Loader2 className="h-6 w-6 animate-spin" /> : error ? <AlertCircle className="h-6 w-6" /> : <Upload className="h-6 w-6" />}
                </div>
                <p className="mt-4 text-sm font-semibold text-slate-800">{analyzing ? `Analyzing ${file?.name}` : error ? 'This file cannot be used' : 'Drop the lecturer list here'}</p>
                <p className={`mx-auto mt-1 max-w-xl text-xs ${error ? 'text-red-600' : 'text-slate-500'}`}>{error || 'The file is inspected safely before any account is created.'}</p>
                {!analyzing && <Button variant="outline" size="sm" className="mt-4" onClick={() => inputRef.current?.click()}>{error ? 'Choose another file' : 'Browse files'}</Button>}
              </div>
            </div>
          )}

          {phase === 'review' && preview && (
            <div className="space-y-4">
              <div className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3.5">
                <div className="min-w-0"><p className="truncate text-sm font-semibold text-slate-800">{file?.name}</p><p className="text-xs text-slate-500">{preview.totalRows} lecturer row(s)</p></div>
                <button type="button" onClick={reset} className="flex shrink-0 items-center gap-1.5 text-xs font-semibold text-primary"><RotateCcw className="h-3.5 w-3.5" /> Choose another file</button>
              </div>

              <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-5">
                <SummaryCard label="Total" value={preview.totalRows} tone="neutral" />
                <SummaryCard label="Create" value={preview.readyCount} tone="success" />
                <SummaryCard label="Activate" value={preview.willActivateCount} tone="warning" />
                <SummaryCard label="Existing" value={preview.existingCount} tone="neutral" />
                <SummaryCard label="Errors" value={preview.errorCount} tone="danger" />
              </div>

              {preview.errorCount > 0 && <Notice tone="danger" text="Resolve every invalid or conflicting row in the source file, then preview again. No account has been changed." />}
              {!preview.canCommit && preview.errorCount === 0 && <Notice tone="warning" text="All Lecturer accounts already exist, so there is nothing to import." />}
              <LecturerRowsTable rows={preview.rows} />
            </div>
          )}

          {phase === 'result' && result && (
            <div className="space-y-5">
              <div className={`rounded-2xl border p-5 text-center ${changedCount > 0 ? 'border-green-200 bg-green-50' : 'border-amber-200 bg-amber-50'}`}>
                <div className={`mx-auto flex h-12 w-12 items-center justify-center rounded-full ${changedCount > 0 ? 'bg-green-100 text-green-600' : 'bg-amber-100 text-amber-600'}`}>
                  {changedCount > 0 ? <CheckCircle2 className="h-6 w-6" /> : <AlertCircle className="h-6 w-6" />}
                </div>
                <h3 className="mt-3 text-lg font-bold text-slate-900">{changedCount > 0 ? 'Lecturer import completed' : 'No account was changed'}</h3>
                <p className="mt-1 text-sm text-slate-600">Created {result.createdCount}, activated {result.activatedCount}, skipped {result.skippedCount}.</p>
              </div>
              {result.errors.length > 0 && <Notice tone="danger" text={`${result.errorCount} row(s) changed after preview and could not be imported. Preview the file again.`} />}
            </div>
          )}
        </main>

        <footer className="flex shrink-0 flex-col-reverse gap-2 border-t border-slate-100 bg-slate-50/60 px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
          {phase === 'upload' && <Button variant="outline" onClick={onClose}>Cancel</Button>}
          {phase === 'review' && <><Button variant="outline" icon={ArrowLeft} onClick={reset} disabled={importing}>Back</Button><Button variant="gradient" icon={Upload} isLoading={importing} disabled={!preview?.canCommit} onClick={() => void commit()}>Import {preview ? preview.readyCount + preview.willActivateCount : 0} account(s)</Button></>}
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

function Notice({ tone, text }: { tone: 'danger' | 'warning'; text: string }) {
  const style = tone === 'danger' ? 'border-red-200 bg-red-50 text-red-700' : 'border-amber-200 bg-amber-50 text-amber-800';
  return <div className={`flex items-start gap-2 rounded-xl border p-3.5 text-sm ${style}`}><AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /><p>{text}</p></div>;
}

function LecturerRowsTable({ rows }: { rows: LecturerImportRow[] }) {
  const statusStyle: Record<LecturerImportRow['status'], string> = {
    Ready: 'bg-green-100 text-green-700', WillActivate: 'bg-orange-100 text-orange-700', AlreadyExists: 'bg-slate-100 text-slate-600', Invalid: 'bg-red-100 text-red-700', Conflict: 'bg-red-100 text-red-700'
  };
  return (
    <div className="overflow-hidden rounded-xl border border-slate-200"><div className="max-h-80 overflow-auto">
      <table className="w-full min-w-[920px] text-left text-xs">
        <thead className="sticky top-0 z-10 bg-slate-50 text-slate-500"><tr><th className="px-3 py-2.5">Row</th><th className="px-3 py-2.5">Lecturer</th><th className="px-3 py-2.5">Position</th><th className="px-3 py-2.5">Google login email</th><th className="px-3 py-2.5">Contact email</th><th className="px-3 py-2.5">Result</th></tr></thead>
        <tbody className="divide-y divide-slate-100 bg-white">{rows.map((row) => <tr key={row.rowNumber} className={!row.isValid ? 'bg-red-50/50' : 'hover:bg-slate-50'}><td className="px-3 py-3 font-mono text-slate-400">{row.rowNumber}</td><td className="px-3 py-3 font-semibold text-slate-700">{row.fullName || '—'}</td><td className="px-3 py-3 text-slate-600">{row.position || '—'}</td><td className="px-3 py-3 text-slate-700">{row.googleEmail || '—'}</td><td className="px-3 py-3 text-slate-600">{row.contactEmail || '—'}</td><td className="px-3 py-3"><span className={`inline-flex rounded-full px-2 py-1 font-semibold ${statusStyle[row.status]}`}>{row.status}</span>{row.message && <p className="mt-1 max-w-xs leading-4 text-slate-500">{row.message}</p>}</td></tr>)}</tbody>
      </table>
    </div></div>
  );
}
