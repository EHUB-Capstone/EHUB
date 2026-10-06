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
import { startupIndustryApi } from '../../api/startupIndustryApi.ts';
import type {
  StartupIndustryImportPreviewResult,
  StartupIndustryImportResult,
  StartupIndustryImportRowPreview,
} from '../../types/startupIndustries.ts';
import { parseApiError } from '../../utils/apiError.ts';
import {
  STARTUP_INDUSTRY_IMPORT_ACCEPT,
  validateStartupIndustryImportFile,
} from '../../utils/startupIndustryImport.ts';
import Button from '../ui/Button.tsx';

interface ImportStartupIndustriesModalProps {
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

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function ImportStartupIndustriesModal({
  onClose,
  onImported,
}: ImportStartupIndustriesModalProps): React.ReactElement {
  const inputRef = useRef<HTMLInputElement>(null);
  const [phase, setPhase] = useState<ImportPhase>('upload');
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<StartupIndustryImportPreviewResult | null>(null);
  const [result, setResult] = useState<StartupIndustryImportResult | null>(null);
  const [fileError, setFileError] = useState('');
  const [analyzing, setAnalyzing] = useState(false);
  const [importing, setImporting] = useState(false);
  const [dragActive, setDragActive] = useState(false);

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !analyzing && !importing) onClose();
    };
    window.addEventListener('keydown', handleEscape);

    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener('keydown', handleEscape);
    };
  }, [analyzing, importing, onClose]);

  const resetImport = () => {
    setPhase('upload');
    setFile(null);
    setPreview(null);
    setResult(null);
    setFileError('');
    setDragActive(false);
    if (inputRef.current) inputRef.current.value = '';
  };

  const inspectFile = async (selectedFile?: File) => {
    if (!selectedFile || analyzing || importing) return;

    const validationError = validateStartupIndustryImportFile(selectedFile);
    if (validationError) {
      setFile(null);
      setPreview(null);
      setFileError(validationError);
      return;
    }

    setFile(selectedFile);
    setPreview(null);
    setFileError('');
    setAnalyzing(true);
    try {
      const response = await startupIndustryApi.previewImport(selectedFile);
      setPreview((response?.data ?? response) as StartupIndustryImportPreviewResult);
      setPhase('review');
    } catch (error) {
      setFileError(parseApiError(error, 'The file could not be read. Please check its format.').message);
    } finally {
      setAnalyzing(false);
    }
  };

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragActive(false);
    void inspectFile(event.dataTransfer.files?.[0]);
  };

  const commitImport = async () => {
    if (!file || !preview || preview.validRowsCount === 0 || preview.errorRowsCount > 0) return;

    setImporting(true);
    try {
      const response = await startupIndustryApi.import(file);
      const importResult = (response?.data ?? response) as StartupIndustryImportResult;
      setResult(importResult);
      setPhase('result');
      onImported();
      toast.success(`${importResult.importedCount} ${importResult.importedCount === 1 ? 'industry' : 'industries'} imported successfully.`);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to import startup industries. Preview the file again and retry.').message);
    } finally {
      setImporting(false);
    }
  };

  const currentStep = phaseIndex(phase);
  const isBusy = analyzing || importing;

  return (
    <div className="fixed inset-0 z-[70] flex items-end justify-center p-0 sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby="import-industries-title">
      <button
        type="button"
        className="absolute inset-0 bg-slate-900/45 backdrop-blur-sm"
        onClick={() => { if (!isBusy) onClose(); }}
        aria-label="Close import dialog"
      />

      <div className="relative flex max-h-[94vh] w-full max-w-4xl flex-col overflow-hidden rounded-t-2xl border border-slate-200/60 bg-white shadow-float animate-scale-in sm:max-h-[90vh] sm:rounded-2xl">
        <header className="shrink-0 border-b border-slate-100 px-5 py-4 sm:px-6">
          <div className="flex items-start justify-between gap-4">
            <div className="flex min-w-0 items-center gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary">
                <FileSpreadsheet className="h-5 w-5" aria-hidden="true" />
              </div>
              <div className="min-w-0">
                <h2 id="import-industries-title" className="text-lg font-bold text-slate-900">Import Industry</h2>
                <p className="truncate text-sm text-slate-500">Preview and validate the Excel file before any industry is added</p>
              </div>
            </div>
            <button
              type="button"
              onClick={onClose}
              disabled={isBusy}
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
                  <span className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-bold ${complete || active ? 'bg-primary text-white' : 'bg-slate-100 text-slate-400'}`}>
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
              <div className="flex items-start gap-2.5 rounded-xl border border-slate-200 bg-slate-50 p-3.5">
                <Info className="mt-0.5 h-4 w-4 shrink-0 text-secondary" aria-hidden="true" />
                <div className="text-xs leading-5 text-slate-600">
                  <p><strong className="text-slate-700">Required columns:</strong> Industry name (or Industry) and Description on the first sheet.</p>
                  <p><strong className="text-slate-700">Default status:</strong> Every imported industry is active.</p>
                  <p><strong className="text-slate-700">Limits:</strong> Excel .xlsx/.xls · maximum 5 MB · maximum 500 rows · names must be unique.</p>
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
                <input
                  ref={inputRef}
                  type="file"
                  accept={STARTUP_INDUSTRY_IMPORT_ACCEPT}
                  className="hidden"
                  aria-label="Startup industry Excel file"
                  onChange={(event) => {
                    void inspectFile(event.target.files?.[0]);
                    event.target.value = '';
                  }}
                />
                <div className={`mx-auto flex h-12 w-12 items-center justify-center rounded-2xl ${fileError ? 'bg-red-100 text-red-600' : 'bg-primary-50 text-primary'}`}>
                  {analyzing
                    ? <Loader2 className="h-6 w-6 animate-spin" />
                    : fileError
                      ? <AlertCircle className="h-6 w-6" />
                      : <Upload className="h-6 w-6" />}
                </div>

                {analyzing ? (
                  <>
                    <p className="mt-4 text-sm font-semibold text-slate-800">Analyzing {file?.name}</p>
                    <p className="mt-1 text-xs text-slate-500">Validating rows and checking existing industries…</p>
                  </>
                ) : (
                  <>
                    <p className="mt-4 text-sm font-semibold text-slate-800">
                      {fileError ? 'This file cannot be used' : 'Drop your industry list here'}
                    </p>
                    <p role={fileError ? 'alert' : undefined} className={`mx-auto mt-1 max-w-lg text-xs ${fileError ? 'text-red-600' : 'text-slate-500'}`}>
                      {fileError || 'Choose an Excel (.xlsx or .xls) file'}
                    </p>
                    <Button variant="outline" size="sm" className="mt-4" onClick={() => inputRef.current?.click()}>
                      {fileError ? 'Choose another file' : 'Browse files'}
                    </Button>
                  </>
                )}
              </div>
            </div>
          )}

          {phase === 'review' && preview && (
            <div className="space-y-4">
              <div className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3.5 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex min-w-0 items-center gap-3">
                  <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-white text-secondary shadow-xs">
                    <FileSpreadsheet className="h-4 w-4" />
                  </div>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-semibold text-slate-800">{file?.name}</p>
                    <p className="text-xs text-slate-500">{file ? formatFileSize(file.size) : ''} · {preview.totalRows} data rows</p>
                  </div>
                </div>
                <button type="button" onClick={resetImport} className="flex shrink-0 items-center gap-1.5 text-xs font-semibold text-primary hover:text-primary-dark">
                  <RotateCcw className="h-3.5 w-3.5" /> Choose another file
                </button>
              </div>

              <div className="grid grid-cols-3 gap-2.5">
                <SummaryCard label="Total rows" value={preview.totalRows} tone="neutral" />
                <SummaryCard label="Valid & Ready" value={preview.validRowsCount} tone="success" />
                <SummaryCard label="Errors" value={preview.errorRowsCount} tone={preview.errorRowsCount > 0 ? 'danger' : 'success'} />
              </div>

              {preview.errorRowsCount > 0 && (
                <div className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50 p-3.5 text-sm text-amber-800">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                  <p><strong>{preview.errorRowsCount} row{preview.errorRowsCount > 1 ? 's contain' : ' contains'} errors.</strong> Fix every error and preview the file again. Industry import is committed as one batch.</p>
                </div>
              )}

              <IndustryRowsTable rows={preview.rows} />
            </div>
          )}

          {phase === 'result' && result && (
            <div className="space-y-5">
              <div className="rounded-2xl border border-green-200 bg-green-50 p-5 text-center">
                <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-green-100 text-green-600">
                  <CheckCircle2 className="h-6 w-6" />
                </div>
                <h3 className="mt-3 text-lg font-bold text-slate-900">Import committed successfully</h3>
                <p className="mt-1 text-sm text-slate-600">
                  {result.importedCount} {result.importedCount === 1 ? 'industry was' : 'industries were'} added as active.
                </p>
              </div>
            </div>
          )}
        </main>

        <footer className="flex shrink-0 flex-col-reverse gap-2 border-t border-slate-100 bg-slate-50/60 px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
          {phase === 'upload' && <Button variant="outline" onClick={onClose} disabled={analyzing}>Cancel</Button>}
          {phase === 'review' && preview && (
            <>
              <Button variant="outline" icon={ArrowLeft} onClick={resetImport} disabled={importing}>Back</Button>
              <Button
                variant="gradient"
                icon={Upload}
                isLoading={importing}
                disabled={preview.validRowsCount === 0 || preview.errorRowsCount > 0}
                onClick={() => void commitImport()}
              >
                {preview.errorRowsCount > 0
                  ? `Resolve ${preview.errorRowsCount} error${preview.errorRowsCount === 1 ? '' : 's'} before import`
                  : `Import ${preview.validRowsCount} ${preview.validRowsCount === 1 ? 'industry' : 'industries'}`}
              </Button>
            </>
          )}
          {phase === 'result' && <Button variant="gradient" icon={Check} onClick={onClose}>Done</Button>}
        </footer>
      </div>
    </div>
  );
}

function SummaryCard({
  label,
  value,
  tone,
}: {
  label: string;
  value: number;
  tone: 'neutral' | 'success' | 'danger';
}): React.ReactElement {
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

function IndustryRowsTable({ rows }: { rows: StartupIndustryImportRowPreview[] }): React.ReactElement {
  return (
    <div className="overflow-hidden rounded-xl border border-slate-200">
      <div className="max-h-80 overflow-auto">
        <table className="w-full min-w-[760px] text-left text-xs">
          <thead className="sticky top-0 z-10 bg-slate-50 text-slate-500">
            <tr>
              <th className="w-16 px-3 py-2.5 font-semibold">Row</th>
              <th className="w-56 px-3 py-2.5 font-semibold">Industry name</th>
              <th className="px-3 py-2.5 font-semibold">Description</th>
              <th className="w-64 px-3 py-2.5 font-semibold">Validation</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 bg-white">
            {rows.map((row) => (
              <tr key={row.rowNumber} className={row.isValid ? 'hover:bg-slate-50/60' : 'bg-red-50/50'}>
                <td className="px-3 py-3 font-mono text-slate-400">{row.rowNumber}</td>
                <td className="px-3 py-3 font-semibold text-slate-700">{row.name || '—'}</td>
                <td className="px-3 py-3 text-slate-600">{row.description || '—'}</td>
                <td className="px-3 py-3">
                  {row.isValid ? (
                    <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2 py-1 font-semibold text-green-700">
                      <CheckCircle2 className="h-3 w-3" /> Ready
                    </span>
                  ) : (
                    <span className="font-medium text-red-600">• {row.errorMessage}</span>
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
