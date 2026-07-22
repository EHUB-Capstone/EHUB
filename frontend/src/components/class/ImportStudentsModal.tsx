import { useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import type { SheetData } from 'write-excel-file/browser';
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
import {
  readStudentImportFile,
  STUDENT_IMPORT_ACCEPT,
  STUDENT_IMPORT_MAX_SIZE,
  validateStudentRows,
  type ExistingStudentRecord,
  type StudentImportRecord,
  type StudentImportValidation,
} from '../../utils/studentImport';

interface ImportStudentsModalProps {
  onClose: () => void;
  onImported: (students: StudentImportRecord[]) => void;
  existingStudents?: ExistingStudentRecord[];
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

const downloadTemplate = async () => {
  const { default: writeXlsxFile } = await import('write-excel-file/browser');
  const headerStyle = {
    fontWeight: 'bold' as const,
    backgroundColor: '#FFF1E6',
    color: '#9A4310',
    align: 'center' as const,
  };
  const data: SheetData = [
    [
      { value: 'StudentCode', ...headerStyle },
      { value: 'FullName', ...headerStyle },
      { value: 'Email', ...headerStyle },
      { value: 'Major', ...headerStyle },
    ],
    [
      { value: 'SE170001' },
      { value: 'Nguyen Van An' },
      { value: 'an.nguyen@fpt.edu.vn' },
      { value: 'SE' },
    ],
  ];

  await writeXlsxFile(data, {
    columns: [{ width: 18 }, { width: 28 }, { width: 34 }, { width: 16 }],
  }).toFile('ehub_student_import_template.xlsx');
};

export default function ImportStudentsModal({
  onClose,
  onImported,
  existingStudents = [],
}: ImportStudentsModalProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [phase, setPhase] = useState<ImportPhase>('upload');
  const [file, setFile] = useState<File | null>(null);
  const [validation, setValidation] = useState<StudentImportValidation | null>(null);
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
    setValidation(null);
    setFileError('');
    setDragActive(false);
    if (inputRef.current) inputRef.current.value = '';
  };

  const inspectFile = async (selectedFile?: File) => {
    if (!selectedFile) return;

    const extension = selectedFile.name.split('.').pop()?.toLowerCase();
    if (extension !== 'xlsx' && extension !== 'csv') {
      setFile(null);
      setValidation(null);
      setFileError('Unsupported file type. Please choose an .xlsx or .csv file.');
      return;
    }

    if (selectedFile.size > STUDENT_IMPORT_MAX_SIZE) {
      setFile(null);
      setValidation(null);
      setFileError('The file is larger than 5 MB. Please upload a smaller student list.');
      return;
    }

    setFile(selectedFile);
    setValidation(null);
    setFileError('');
    setAnalyzing(true);

    try {
      const rows = await readStudentImportFile(selectedFile);
      const nextValidation = validateStudentRows(rows, existingStudents);
      setValidation(nextValidation);

      if (nextValidation.fileErrors.length > 0) {
        setFileError(nextValidation.fileErrors[0]);
      } else {
        setPhase('review');
      }
    } catch (error) {
      setFileError(error instanceof Error ? error.message : 'The file could not be read. Please check its contents.');
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
      await downloadTemplate();
      toast.success('Template downloaded');
    } catch {
      toast.error('Unable to download the template');
    }
  };

  const handleImport = async () => {
    if (!validation || validation.validRows.length === 0) return;

    setImporting(true);
    await new Promise((resolve) => window.setTimeout(resolve, 450));
    onImported(validation.validRows);
    setImporting(false);
    setPhase('result');

    if (validation.invalidRows.length === 0) {
      toast.success(`${validation.validRows.length} students are ready to use`);
    } else {
      toast.success(`${validation.validRows.length} valid students imported`);
    }
  };

  const handleDone = () => {
    onClose();
  };

  const currentStep = phaseIndex(phase);
  const totalRows = validation?.rows.length ?? 0;
  const successCount = validation?.validRows.length ?? 0;
  const failedCount = validation?.invalidRows.length ?? 0;

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
                <h2 id="import-students-title" className="text-lg font-bold text-slate-900">Import students</h2>
                <p className="truncate text-sm text-slate-500">Create multiple student profiles from one file</p>
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
                    <p className="text-sm font-semibold text-secondary-dark">Start with the EHUB template</p>
                    <p className="mt-0.5 text-xs text-slate-500">Includes the correct column names and one example row.</p>
                  </div>
                </div>
                <Button variant="outline" size="sm" icon={Download} onClick={() => void handleDownloadTemplate()} className="shrink-0 border-secondary-200 text-secondary">
                  Download template
                </Button>
              </div>

              <div className="flex items-start gap-2.5 rounded-xl border border-slate-200 bg-slate-50 p-3.5">
                <Info className="mt-0.5 h-4 w-4 shrink-0 text-secondary" />
                <div className="text-xs leading-5 text-slate-600">
                  <p><strong className="text-slate-700">Required:</strong> StudentCode, FullName, Email</p>
                  <p><strong className="text-slate-700">Optional:</strong> Major · First row must contain column names · Maximum file size 5 MB</p>
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
                <input ref={inputRef} type="file" accept={STUDENT_IMPORT_ACCEPT} className="hidden" onChange={handleFileInput} />
                <div className={`mx-auto flex h-12 w-12 items-center justify-center rounded-2xl ${fileError ? 'bg-red-100 text-red-600' : 'bg-primary-50 text-primary'}`}>
                  {analyzing ? <Loader2 className="h-6 w-6 animate-spin" /> : fileError ? <AlertCircle className="h-6 w-6" /> : <Upload className="h-6 w-6" />}
                </div>

                {analyzing ? (
                  <>
                    <p className="mt-4 text-sm font-semibold text-slate-800">Checking {file?.name}</p>
                    <p className="mt-1 text-xs text-slate-500">Reading rows and validating duplicates…</p>
                  </>
                ) : (
                  <>
                    <p className="mt-4 text-sm font-semibold text-slate-800">
                      {fileError ? 'This file cannot be used' : 'Drop your student list here'}
                    </p>
                    <p className={`mx-auto mt-1 max-w-lg text-xs ${fileError ? 'text-red-600' : 'text-slate-500'}`}>
                      {fileError || 'Choose an Excel (.xlsx) or CSV (.csv) file'}
                    </p>
                    <Button variant="outline" size="sm" className="mt-4" onClick={() => inputRef.current?.click()}>
                      {fileError ? 'Choose another file' : 'Browse files'}
                    </Button>
                  </>
                )}
              </div>
            </div>
          )}

          {phase === 'review' && validation && (
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
                <SummaryCard label="Ready" value={successCount} tone="success" />
                <SummaryCard label="Has errors" value={failedCount} tone="danger" />
              </div>

              {failedCount > 0 && (
                <div className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50 p-3.5 text-sm text-amber-800">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                  <p><strong>{failedCount} row{failedCount > 1 ? 's have' : ' has'} errors.</strong> Invalid rows will be skipped; valid rows can still be imported.</p>
                </div>
              )}

              <StudentRowsTable validation={validation} />
            </div>
          )}

          {phase === 'result' && validation && (
            <div className="space-y-5">
              <div className={`rounded-2xl border p-5 text-center ${failedCount === 0 ? 'border-green-200 bg-green-50' : 'border-amber-200 bg-amber-50'}`}>
                <div className={`mx-auto flex h-12 w-12 items-center justify-center rounded-full ${failedCount === 0 ? 'bg-green-100 text-green-600' : 'bg-amber-100 text-amber-600'}`}>
                  {failedCount === 0 ? <CheckCircle2 className="h-6 w-6" /> : <AlertCircle className="h-6 w-6" />}
                </div>
                <h3 className="mt-3 text-lg font-bold text-slate-900">
                  {failedCount === 0 ? 'Import completed successfully' : 'Import completed with some errors'}
                </h3>
                <p className="mt-1 text-sm text-slate-600">
                  {successCount} of {totalRows} student records were imported.
                </p>
              </div>

              <div className="grid grid-cols-3 gap-2.5">
                <SummaryCard label="Processed" value={totalRows} tone="neutral" />
                <SummaryCard label="Successful" value={successCount} tone="success" />
                <SummaryCard label="Failed" value={failedCount} tone="danger" />
              </div>

              {failedCount > 0 && (
                <div>
                  <h4 className="mb-2 flex items-center gap-2 text-sm font-semibold text-slate-800">
                    <AlertCircle className="h-4 w-4 text-red-500" /> Failed rows
                  </h4>
                  <StudentRowsTable validation={{ ...validation, rows: validation.invalidRows }} compact />
                </div>
              )}
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
                {successCount === 0 ? 'No valid rows to import' : `Import ${successCount} valid student${successCount > 1 ? 's' : ''}`}
              </Button>
            </>
          )}

          {phase === 'result' && (
            <>
              <Button variant="outline" icon={RotateCcw} onClick={resetImport}>Import another file</Button>
              <Button variant="gradient" icon={Check} onClick={handleDone}>Done</Button>
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

function StudentRowsTable({ validation, compact = false }: { validation: StudentImportValidation; compact?: boolean }) {
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
              <th className="w-40 px-3 py-2.5 font-semibold">Validation</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 bg-white">
            {validation.rows.map((row) => (
              <tr key={row.rowNumber} className={row.isValid ? 'hover:bg-slate-50/60' : 'bg-red-50/50'}>
                <td className="px-3 py-3 font-mono text-slate-400">{row.rowNumber}</td>
                <td className="px-3 py-3 font-semibold text-slate-700">{row.studentCode || '—'}</td>
                <td className="px-3 py-3 text-slate-700">{row.fullName || '—'}</td>
                <td className="px-3 py-3 text-slate-600">{row.email || '—'}</td>
                <td className="px-3 py-3">
                  {row.isValid ? (
                    <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2 py-1 font-semibold text-green-700">
                      <CheckCircle2 className="h-3 w-3" /> Ready
                    </span>
                  ) : (
                    <ul className="space-y-1 text-red-600">
                      {row.errors.map((error) => <li key={error}>• {error}</li>)}
                    </ul>
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
