import { useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { AlertTriangle, CheckCircle2, FileSpreadsheet, Loader2, Upload, X } from 'lucide-react';
import { classApi } from '../../api/classApi';
import type { SemesterGroupImportResponse, SemesterGroupImportRow } from '../../types/classes';
import { parseApiError } from '../../utils/apiError';
import ConfirmDialog from '../ui/ConfirmDialog';

interface ImportSemesterGroupsModalProps {
  classId: string;
  onClose: () => void;
  onImported?: () => void;
}

const statusTone: Record<string, string> = {
  Changed: 'bg-indigo-100 text-indigo-700',
  Unchanged: 'bg-green-100 text-green-700',
  Invalid: 'bg-red-100 text-red-700',
  Duplicate: 'bg-red-100 text-red-700',
  NotFound: 'bg-amber-100 text-amber-700',
};

function unwrapResponse(response: unknown): SemesterGroupImportResponse {
  const outer = response as { data?: unknown };
  const payload = outer?.data ?? response;
  const envelope = payload as { data?: SemesterGroupImportResponse };
  return envelope?.data ?? payload as SemesterGroupImportResponse;
}

export default function ImportSemesterGroupsModal({
  classId,
  onClose,
  onImported,
}: ImportSemesterGroupsModalProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [report, setReport] = useState<SemesterGroupImportResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [importing, setImporting] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [applied, setApplied] = useState(false);

  const selectFile = (selectedFile?: File) => {
    if (!selectedFile) return;
    if (!/\.(xlsx|xls)$/i.test(selectedFile.name)) {
      toast.error('Only .xlsx or .xls files are supported.');
      return;
    }
    setFile(selectedFile);
    setReport(null);
    setApplied(false);
    setShowConfirm(false);
  };

  const preview = async () => {
    if (!file) {
      toast.error('Please select an Excel file.');
      return;
    }

    setLoading(true);
    try {
      const formData = new FormData();
      formData.append('file', file);
      const response = await classApi.previewSemesterGroups(classId, formData);
      setReport(unwrapResponse(response));
      toast.success('Preview ready. No semester groups have been changed.');
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to preview semester groups.').message);
    } finally {
      setLoading(false);
    }
  };

  const confirmImport = async () => {
    if (!file || !report || report.errorRowsCount > 0 || importing) return;

    setImporting(true);
    try {
      const formData = new FormData();
      formData.append('file', file);
      const response = await classApi.importSemesterGroups(classId, formData);
      const result = unwrapResponse(response);
      setReport(result);
      setApplied(true);
      setShowConfirm(false);
      toast.success(`Imported ${result.updatedCount} semester group value(s).`);
      onImported?.();
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to import semester groups.').message);
    } finally {
      setImporting(false);
    }
  };

  return (
    <>
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
        <button type="button" aria-label="Close" className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
        <div className="relative flex max-h-[92vh] w-full max-w-4xl flex-col rounded-2xl bg-white shadow-float">
          <div className="flex items-start justify-between border-b border-slate-100 p-6">
            <div>
              <h2 className="text-xl font-bold text-slate-900">Import semester groups</h2>
              <p className="mt-1 text-sm text-slate-500">
                Match students by RollNumber, preview the semester group values, then confirm the import.
              </p>
            </div>
            <button type="button" onClick={onClose} className="rounded-xl p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="flex-1 space-y-5 overflow-y-auto p-6">
            <div className="rounded-xl border border-indigo-100 bg-indigo-50 p-3 text-sm text-indigo-800">
              Required columns: <strong>RollNumber</strong> and the exact semester column
              {report ? <> <strong>{report.expectedColumnName}</strong></> : <> such as <strong>Group FA26</strong> or <strong>Group SP27</strong></>}.
              The semester column is detected from this class&apos;s semester; GroupName and Team Name are ignored.
            </div>

            <div
              role="button"
              tabIndex={0}
              onClick={() => inputRef.current?.click()}
              onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') inputRef.current?.click(); }}
              onDragOver={(event) => event.preventDefault()}
              onDrop={(event) => {
                event.preventDefault();
                selectFile(event.dataTransfer.files[0]);
              }}
              className={`cursor-pointer rounded-2xl border-2 border-dashed p-8 text-center transition ${file ? 'border-primary bg-primary-50' : 'border-slate-200 hover:border-primary hover:bg-primary-50/40'}`}
            >
              <input
                ref={inputRef}
                type="file"
                accept=".xlsx,.xls"
                className="hidden"
                onChange={(event) => selectFile(event.target.files?.[0])}
              />
              <FileSpreadsheet className={`mx-auto mb-3 h-10 w-10 ${file ? 'text-primary' : 'text-slate-300'}`} />
              <p className="font-semibold text-slate-700">{file?.name || 'Drop an Excel file here or click to select'}</p>
              {file && <p className="mt-1 text-xs text-slate-400">{(file.size / 1024).toFixed(1)} KB · Click to choose another file</p>}
            </div>

            {report && (
              <div className="space-y-4">
                <div className={`rounded-xl border p-3 text-sm ${report.errorRowsCount > 0 ? 'border-red-200 bg-red-50 text-red-800' : 'border-green-200 bg-green-50 text-green-800'}`}>
                  {applied
                    ? `Import completed: ${report.updatedCount} value(s) updated.`
                    : report.errorRowsCount > 0
                      ? `Preview found ${report.errorRowsCount} error row(s). Fix the file and preview again before importing.`
                      : `Preview only — ${report.changedRowsCount} value(s) will change and ${report.unchangedRowsCount} are already current.`}
                </div>

                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  {[
                    ['Rows', report.totalRows],
                    ['Will change', report.changedRowsCount],
                    ['Unchanged', report.unchangedRowsCount],
                    ['Errors', report.errorRowsCount],
                  ].map(([label, value]) => (
                    <div key={String(label)} className="rounded-xl border border-slate-100 bg-slate-50 p-3 text-center">
                      <p className="text-2xl font-bold text-slate-800">{value}</p>
                      <p className="text-xs text-slate-500">{label}</p>
                    </div>
                  ))}
                </div>

                <div className="overflow-x-auto rounded-xl border border-slate-200">
                  <table className="w-full text-sm">
                    <thead className="border-b border-slate-200 bg-slate-50">
                      <tr>
                        <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-500">Row</th>
                        <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-500">RollNumber</th>
                        <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-500">Student</th>
                        <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-500">Current</th>
                        <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-500">From {report.expectedColumnName}</th>
                        <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-500">Status</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {report.rows.map((row: SemesterGroupImportRow) => (
                        <tr key={`${row.rowNumber}-${row.rollNumber}`} className="align-top hover:bg-slate-50">
                          <td className="px-3 py-2.5 text-xs text-slate-400">{row.rowNumber}</td>
                          <td className="px-3 py-2.5 font-mono text-xs text-slate-700">{row.rollNumber || '-'}</td>
                          <td className="px-3 py-2.5 text-xs text-slate-700">{row.fullName || '-'}</td>
                          <td className="px-3 py-2.5 text-xs text-slate-600">{row.currentGroupName || '-'}</td>
                          <td className="px-3 py-2.5 text-xs font-semibold text-slate-800">{row.importedGroupName || '-'}</td>
                          <td className="px-3 py-2.5">
                            <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${statusTone[row.status] || 'bg-slate-100 text-slate-600'}`}>
                              {row.status}
                            </span>
                            {row.message && <p className="mt-1 max-w-[240px] text-xs text-red-600">{row.message}</p>}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>

          <div className="flex items-center justify-between gap-3 border-t border-slate-100 p-5">
            <button type="button" onClick={onClose} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50">
              {applied ? 'Close' : 'Cancel'}
            </button>
            <div className="flex items-center gap-2">
              {!applied && (
                <button
                  type="button"
                  onClick={preview}
                  disabled={!file || loading || importing}
                  className="inline-flex items-center gap-2 rounded-xl border border-primary-200 bg-primary-50 px-4 py-2 text-sm font-semibold text-primary hover:bg-primary-100 disabled:opacity-50"
                >
                  {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
                  Preview
                </button>
              )}
              {!applied && report && (
                <button
                  type="button"
                  onClick={() => setShowConfirm(true)}
                  disabled={report.errorRowsCount > 0 || report.changedRowsCount === 0 || loading || importing}
                  className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-white hover:bg-primary-600 disabled:opacity-50"
                >
                  {report.errorRowsCount > 0 ? <AlertTriangle className="h-4 w-4" /> : <CheckCircle2 className="h-4 w-4" />}
                  Confirm import
                </button>
              )}
            </div>
          </div>
        </div>
      </div>

      <ConfirmDialog
        isOpen={showConfirm}
        onClose={() => setShowConfirm(false)}
        onConfirm={confirmImport}
        isSubmitting={importing}
        title="Import semester group values?"
        description={`Update ${report?.changedRowsCount || 0} active enrollment(s) using RollNumber and ${report?.expectedColumnName || 'the semester group column'}?`}
        confirmText="Import groups"
        cancelText="Back to preview"
      />
    </>
  );
}
