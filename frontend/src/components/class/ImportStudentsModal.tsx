import { useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';
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
import Button from '../ui/Button';
import { classApi } from '../../api/classApi';

interface ImportStudentsModalProps {
  classId?: string;
  onClose: () => void;
  onImported: () => void;
}

type ImportPhase = 'upload' | 'review' | 'result';

const IMPORT_STEPS = [
  { key: 'upload', label: 'Select file' },
  { key: 'review', label: 'Review data' },
  { key: 'result', label: 'Import result' },
] as const;

const phaseIndex = (phase: ImportPhase) => IMPORT_STEPS.findIndex((step) => step.key === phase);

const formatFileSize = (bytes: number) => {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

export default function ImportStudentsModal({
  classId,
  onClose,
  onImported,
}: ImportStudentsModalProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [phase, setPhase] = useState<ImportPhase>('upload');
  const [file, setFile] = useState<File | null>(null);
  const [previewData, setPreviewData] = useState<any | null>(null);
  const [commitResult, setCommitResult] = useState<any | null>(null);
  const [fileError, setFileError] = useState('');
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

  const resetImport = () => {
    setPhase('upload');
    setFile(null);
    setPreviewData(null);
    setCommitResult(null);
    setFileError('');
    setDragActive(false);
    if (inputRef.current) inputRef.current.value = '';
  };

  const inspectFile = async (selectedFile?: File) => {
    if (!selectedFile) return;

    const extension = selectedFile.name.split('.').pop()?.toLowerCase();
    if (extension !== 'xlsx') {
      setFile(null);
      setPreviewData(null);
      setFileError('Unsupported file type. Please choose an .xlsx file.');
      return;
    }

    if (selectedFile.size > 10 * 1024 * 1024) {
      setFile(null);
      setPreviewData(null);
      setFileError('The file is larger than 10 MB. Please upload a smaller student list.');
      return;
    }

    setFile(selectedFile);
    setPreviewData(null);
    setFileError('');
    setAnalyzing(true);

    try {
      const formData = new FormData();
      formData.append('file', selectedFile);

      const res = await classApi.previewImportStudents(classId, formData);
      const data = res?.data || res;

      setPreviewData(data);
      setPhase('review');
    } catch (error: any) {
      setFileError(error?.response?.data?.message || error?.message || 'The file could not be read. Please check its format.');
    } finally {
      setAnalyzing(false);
    }
  };

  const handleFileInput = (event: React.ChangeEvent<HTMLInputElement>) => {
    void inspectFile(event.target.files?.[0]);
    event.target.value = '';
  };

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragActive(false);
    void inspectFile(event.dataTransfer.files?.[0]);
  };

  const handleDownloadTemplate = async () => {
    try {
      const response = await classApi.getImportTemplate();
      const blob = new Blob([response.data || response], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'Student_Import_Template.xlsx';
      link.click();
      window.URL.revokeObjectURL(url);
      toast.success('Template downloaded');
    } catch {
      toast.error('Unable to download template');
    }
  };

  const handleImport = async () => {
    if (!previewData || !previewData.sessionId || previewData.validRowsCount === 0) return;

    setImporting(true);
    try {
      const res = await classApi.commitImportStudents(classId, { sessionId: previewData.sessionId });
      const result = res?.data || res;
      setCommitResult(result);
      setPhase('result');
      onImported();
      toast.success(`Successfully imported ${result.insertedCount + result.updatedCount} students.`);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || err?.message || 'Failed to commit student import.');
    } finally {
      setImporting(false);
    }
  };

  const currentStep = phaseIndex(phase);
  const totalRows = previewData?.totalRows ?? 0;
  const successCount = previewData?.validRowsCount ?? 0;
  const failedCount = previewData?.errorRowsCount ?? 0;

  return (
    <div className="fixed inset-0 z-[70] flex items-end justify-center p-0 sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby="import-students-title">
      <button
        type="button"
        className="absolute inset-0 cursor-default bg-slate-900/45 backdrop-blur-sm"
        onClick={importing ? undefined : onClose}
        aria-label="Close import dialog"
      />

      <div className="relative flex max-h-[94vh] w-full max-w-4xl flex-col overflow-hidden rounded-t-2xl border border-slate-200/60 bg-white shadow-float animate-scale-in sm:max-h-[90vh] sm:rounded-2xl">
        <header className="shrink-0 border-b border-slate-100 px-5 py-4 sm:px-6">
          <div className="flex items-start justify-between gap-4">
            <div className="flex min-w-0 items-center gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary">
                <FileSpreadsheet className="h-5 w-5" />
              </div>
              <div className="min-w-0">
                <h2 id="import-students-title" className="text-lg font-bold text-slate-900">Import students (Excel)</h2>
                <p className="truncate text-sm text-slate-500">Preview &amp; commit student roster from Excel</p>
              </div>
            </div>
            <button
              type="button"
              onClick={onClose}
              disabled={importing}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700 disabled:pointer-events-none disabled:opacity-50"
              aria-label="Close"
            >
              <X className="h-5 w-5" />
            </button>
          </div>

          <ol className="mt-4 grid grid-cols-3 gap-2" aria-label="Import progress">
            {IMPORT_STEPS.map((step, index) => {
              const complete = index < currentStep;
              const active = index === currentStep;
              return (
                <li key={step.key} className="flex min-w-0 items-center gap-2">
                  <span className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-bold ${
                    complete || active ? 'bg-primary text-white' : 'bg-slate-100 text-slate-400'
                  }`}>
                    {complete ? <Check className="h-3.5 w-3.5" /> : index + 1}
                  </span>
                  <span className={`truncate text-xs font-semibold sm:text-sm ${active ? 'text-slate-900' : 'text-slate-400'}`}>
                    {step.label}
                  </span>
                  {index < IMPORT_STEPS.length - 1 && <span className="hidden h-px flex-1 bg-slate-200 sm:block" />}
                </li>
              );
            })}
          </ol>
        </header>

        <main className="flex-1 overflow-y-auto px-5 py-5 sm:px-6">
          {phase === 'upload' && (
            <div className="space-y-4">
              <div className="flex flex-col gap-3 rounded-xl border border-secondary-100 bg-secondary-50 p-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-start gap-3">
                  <Download className="mt-0.5 h-5 w-5 shrink-0 text-secondary" />
                  <div>
                    <p className="text-sm font-semibold text-secondary-dark">Start with the official EHUB template</p>
                    <p className="mt-0.5 text-xs text-slate-500">Includes exact columns: StudentCode, FullName, Email, MajorCode.</p>
                  </div>
                </div>
                <Button variant="outline" size="sm" icon={Download} onClick={() => void handleDownloadTemplate()} className="shrink-0 border-secondary-200 text-secondary">
                  Download template (.xlsx)
                </Button>
              </div>

              <div className="flex items-start gap-2.5 rounded-xl border border-slate-200 bg-slate-50 p-3.5">
                <Info className="mt-0.5 h-4 w-4 shrink-0 text-secondary" />
                <div className="text-xs leading-5 text-slate-600">
                  <p><strong className="text-slate-700">Required columns:</strong> StudentCode (RollNumber), FullName, Email</p>
                  <p><strong className="text-slate-700">Optional:</strong> MajorCode · Max file size 10 MB · Validated line by line without immediate DB write</p>
                </div>
              </div>

              <div
                onDragEnter={() => setDragActive(true)}
                onDragLeave={() => setDragActive(false)}
                onDragOver={(event) => event.preventDefault()}
                onDrop={handleDrop}
                className={`rounded-2xl border-2 border-dashed px-5 py-10 text-center transition-colors ${
                  fileError
                    ? 'border-red-300 bg-red-50/60'
                    : dragActive || file
                      ? 'border-primary bg-primary-50'
                      : 'border-slate-300 bg-white hover:border-primary hover:bg-primary-50/40'
                }`}
              >
                <input ref={inputRef} type="file" accept=".xlsx" className="hidden" onChange={handleFileInput} />
                <div className={`mx-auto flex h-12 w-12 items-center justify-center rounded-2xl ${fileError ? 'bg-red-100 text-red-600' : 'bg-primary-50 text-primary'}`}>
                  {analyzing ? <Loader2 className="h-6 w-6 animate-spin" /> : fileError ? <AlertCircle className="h-6 w-6" /> : <Upload className="h-6 w-6" />}
                </div>

                {analyzing ? (
                  <>
                    <p className="mt-4 text-sm font-semibold text-slate-800">Analyzing {file?.name}</p>
                    <p className="mt-1 text-xs text-slate-500">Validating rows and checking cross-class conflicts…</p>
                  </>
                ) : (
                  <>
                    <p className="mt-4 text-sm font-semibold text-slate-800">
                      {fileError ? 'This file cannot be used' : 'Drop your student list here'}
                    </p>
                    <p className={`mx-auto mt-1 max-w-lg text-xs ${fileError ? 'text-red-600' : 'text-slate-500'}`}>
                      {fileError || 'Choose an Excel (.xlsx) file'}
                    </p>
                    <Button variant="outline" size="sm" className="mt-4" onClick={() => inputRef.current?.click()}>
                      {fileError ? 'Choose another file' : 'Browse files'}
                    </Button>
                  </>
                )}
              </div>
            </div>
          )}

          {phase === 'review' && previewData && (
            <div className="space-y-4">
              <div className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3.5 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex min-w-0 items-center gap-3">
                  <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-white text-secondary shadow-xs">
                    <FileSpreadsheet className="h-4 w-4" />
                  </div>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-semibold text-slate-800">{file?.name}</p>
                    <p className="text-xs text-slate-500">{file ? formatFileSize(file.size) : ''} · {totalRows} data rows</p>
                  </div>
                </div>
                <button type="button" onClick={resetImport} className="flex shrink-0 items-center gap-1.5 text-xs font-semibold text-primary hover:text-primary-dark">
                  <RotateCcw className="h-3.5 w-3.5" /> Choose another file
                </button>
              </div>

              <div className="grid grid-cols-3 gap-2.5">
                <SummaryCard label="Total rows" value={totalRows} tone="neutral" />
                <SummaryCard label="Valid &amp; Ready" value={successCount} tone="success" />
                <SummaryCard label="Errors / Skip" value={failedCount} tone="danger" />
              </div>

              {failedCount > 0 && (
                <div className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50 p-3.5 text-sm text-amber-800">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                  <p><strong>{failedCount} row{failedCount > 1 ? 's have' : ' has'} validation errors.</strong> Invalid rows will be skipped; valid rows will be committed safely.</p>
                </div>
              )}

              <StudentRowsTable rows={previewData.rows || []} />
            </div>
          )}

          {phase === 'result' && commitResult && (
            <div className="space-y-5">
              <div className="rounded-2xl border border-green-200 bg-green-50 p-5 text-center">
                <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-green-100 text-green-600">
                  <CheckCircle2 className="h-6 w-6" />
                </div>
                <h3 className="mt-3 text-lg font-bold text-slate-900">
                  Import committed successfully
                </h3>
                <p className="mt-1 text-sm text-slate-600">
                  {commitResult.insertedCount} new inserted, {commitResult.updatedCount} updated, {commitResult.skippedCount} skipped.
                </p>
              </div>

              <div className="grid grid-cols-3 gap-2.5">
                <SummaryCard label="Inserted" value={commitResult.insertedCount} tone="success" />
                <SummaryCard label="Updated" value={commitResult.updatedCount} tone="neutral" />
                <SummaryCard label="Skipped" value={commitResult.skippedCount} tone="danger" />
              </div>
            </div>
          )}
        </main>

        <footer className="flex shrink-0 flex-col-reverse gap-2 border-t border-slate-100 bg-slate-50/60 px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
          {phase === 'upload' && (
            <Button variant="outline" onClick={onClose}>Cancel</Button>
          )}

          {phase === 'review' && (
            <>
              <Button variant="outline" icon={ArrowLeft} onClick={resetImport} disabled={importing}>Back</Button>
              <Button
                variant="gradient"
                icon={Upload}
                isLoading={importing}
                disabled={successCount === 0}
                onClick={() => void handleImport()}
              >
                {successCount === 0 ? 'No valid rows to commit' : `Commit ${successCount} valid student${successCount > 1 ? 's' : ''}`}
              </Button>
            </>
          )}

          {phase === 'result' && (
            <>
              <Button variant="gradient" icon={Check} onClick={onClose}>Done</Button>
            </>
          )}
        </footer>
      </div>
    </div>
  );
}

function SummaryCard({ label, value, tone }: { label: string; value: number; tone: 'neutral' | 'success' | 'danger' }) {
  const styles = {
    neutral: 'border-slate-200 bg-slate-50 text-slate-900',
    success: 'border-green-200 bg-green-50 text-green-700',
    danger: 'border-red-200 bg-red-50 text-red-600',
  };

  return (
    <div className={`rounded-xl border p-3 text-center ${styles[tone]}`}>
      <p className="text-xl font-bold sm:text-2xl">{value}</p>
      <p className="mt-0.5 text-[11px] font-medium sm:text-xs">{label}</p>
    </div>
  );
}

function StudentRowsTable({ rows, compact = false }: { rows: any[]; compact?: boolean }) {
  return (
    <div className="overflow-hidden rounded-xl border border-slate-200">
      <div className={`${compact ? 'max-h-52' : 'max-h-72'} overflow-auto`}>
        <table className="w-full min-w-[720px] text-left text-xs">
          <thead className="sticky top-0 z-10 bg-slate-50 text-slate-500">
            <tr>
              <th className="w-16 px-3 py-2.5 font-semibold">Row</th>
              <th className="px-3 py-2.5 font-semibold">Student code</th>
              <th className="px-3 py-2.5 font-semibold">Full name</th>
              <th className="px-3 py-2.5 font-semibold">Email</th>
              <th className="px-3 py-2.5 font-semibold">Major</th>
              <th className="w-40 px-3 py-2.5 font-semibold">Validation</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 bg-white">
            {rows.map((row) => (
              <tr key={row.rowNumber} className={row.isValid ? 'hover:bg-slate-50/60' : 'bg-red-50/50'}>
                <td className="px-3 py-3 font-mono text-slate-400">{row.rowNumber}</td>
                <td className="px-3 py-3 font-semibold text-slate-700">{row.studentCode || '—'}</td>
                <td className="px-3 py-3 text-slate-700">{row.fullName || '—'}</td>
                <td className="px-3 py-3 text-slate-600">{row.email || '—'}</td>
                <td className="px-3 py-3 text-slate-600">{row.majorCode || '—'}</td>
                <td className="px-3 py-3">
                  {row.isValid ? (
                    <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2 py-1 font-semibold text-green-700">
                      <CheckCircle2 className="h-3 w-3" /> Ready
                    </span>
                  ) : (
                    <span className="text-red-600 font-medium">
                      • {row.errorMessage}
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
