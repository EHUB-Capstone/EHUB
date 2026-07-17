import { useState, useRef } from 'react';
import toast from 'react-hot-toast';
import { X, Upload, FileSpreadsheet, Download, CheckCircle2, AlertCircle, Loader2, Info } from 'lucide-react';
import { classApi } from '../../api/classApi';

const TEMPLATE_HEADERS = 'Class,RollNumber,Email,MemberCode,FullName,ExamDate,ExamNote,Outcome 1,Outcome 1_Comment,Outcome 2,Outcome 2_Comment,Outcome 3,Outcome 3_Comment\n';
const TEMPLATE_ROW     = 'EXE201_12,SE170001,an.nguyen@fpt.edu.vn,ANNVDS170001,Nguyen Van An,07/25/2026,,,,,,,\n';

const downloadTemplate = () => {
  const blob = new Blob([TEMPLATE_HEADERS + TEMPLATE_ROW], { type: 'text/csv;charset=utf-8;' });
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href = url; a.download = 'student_import_template.csv';
  a.click(); URL.revokeObjectURL(url);
};

export default function ImportStudentsModal({ classId, onClose, onImported }) {
  const inputRef  = useRef(null);
  const [file,    setFile]    = useState(null);
  const [loading, setLoading] = useState(false);
  const [result,  setResult]  = useState(null);

  const handleFileDrop = (e) => {
    e.preventDefault();
    const dropped = e.dataTransfer?.files[0] || e.target.files[0];
    if (!dropped) return;
    if (!/\.(xlsx|xls)$/i.test(dropped.name)) {
      toast.error('Only .xlsx and .xls files are accepted');
      return;
    }
    setFile(dropped);
    setResult(null);
  };

  const handleImport = async () => {
    if (!file) { toast.error('Please select a file first'); return; }
    setLoading(true);
    try {
      const fd = new FormData();
      fd.append('file', file);
      const res = await classApi.importStudents(classId, fd);
      const data = res?.data || res;
      setResult(data);
      if (data.failedCount === 0) {
        toast.success(`All ${data.successCount} students imported!`);
      } else {
        toast(`${data.successCount} imported, ${data.failedCount} failed`, { icon: '⚠️' });
      }
    } catch (e) {
      toast.error(e?.message || 'Import failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-lg max-h-[90vh] overflow-y-auto animate-scale-in">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-slate-100 sticky top-0 bg-white rounded-t-2xl">
          <div>
            <h2 className="text-xl font-bold text-slate-900">Import Students</h2>
            <p className="text-sm text-slate-400 mt-0.5">Upload an Excel file (.xlsx / .xls)</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 space-y-5">
          {/* Template download */}
          <div className="flex items-center justify-between bg-secondary-50 rounded-xl p-3 border border-secondary-100">
            <div className="flex items-center gap-2">
              <FileSpreadsheet className="w-5 h-5 text-secondary" />
              <div>
                <p className="text-sm font-medium text-secondary">Download Template</p>
                <p className="text-xs text-slate-400">Columns: Class, RollNumber, Email, MemberCode, FullName, ExamDate, ExamNote, Outcome 1/2/3, Comments</p>
              </div>
            </div>
            <button
              onClick={downloadTemplate}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-secondary text-white rounded-lg text-xs font-medium hover:bg-secondary-dark transition-all shrink-0"
            >
              <Download className="w-3.5 h-3.5" /> Template
            </button>
          </div>

          {/* Format notice */}
          <div className="flex items-start gap-2 bg-blue-50 rounded-xl p-3 border border-blue-100">
            <Info className="w-4 h-4 text-blue-500 mt-0.5 shrink-0" />
            <p className="text-xs text-blue-700">
              <strong>Required columns:</strong> RollNumber, Email, FullName.<br />
              <strong>Optional:</strong> Class, MemberCode, ExamDate (MM/DD/YYYY), ExamNote, Outcome 1, Outcome 1_Comment, Outcome 2, Outcome 2_Comment, Outcome 3, Outcome 3_Comment.
            </p>
          </div>

          {/* Drop zone */}
          <div
            onDragOver={(e) => e.preventDefault()}
            onDrop={handleFileDrop}
            onClick={() => inputRef.current?.click()}
            className={`border-2 border-dashed rounded-2xl p-8 text-center cursor-pointer transition-all ${
              file ? 'border-primary bg-primary-50' : 'border-slate-200 hover:border-primary hover:bg-primary-50/40'
            }`}
          >
            <input ref={inputRef} type="file" accept=".xlsx,.xls" className="hidden" onChange={handleFileDrop} />
            <Upload className={`w-10 h-10 mx-auto mb-3 ${file ? 'text-primary' : 'text-slate-300'}`} />
            {file ? (
              <div>
                <p className="font-semibold text-slate-800">{file.name}</p>
                <p className="text-xs text-slate-400 mt-1">{(file.size / 1024).toFixed(1)} KB · Click to change</p>
              </div>
            ) : (
              <div>
                <p className="font-medium text-slate-500">Drop your Excel file here</p>
                <p className="text-xs text-slate-400 mt-1">or click to browse</p>
              </div>
            )}
          </div>

          {/* Result */}
          {result && (
            <div className="space-y-3">
              {/* Summary */}
              <div className="grid grid-cols-3 gap-3">
                <div className="bg-slate-50 rounded-xl p-3 text-center border border-slate-100">
                  <p className="text-2xl font-bold text-slate-900">{result.totalRows ?? 0}</p>
                  <p className="text-xs text-slate-400 mt-0.5">Total Rows</p>
                </div>
                <div className="bg-green-50 rounded-xl p-3 text-center border border-green-100">
                  <p className="text-2xl font-bold text-green-600">{result.successCount ?? 0}</p>
                  <p className="text-xs text-green-500 mt-0.5">Imported</p>
                </div>
                <div className="bg-red-50 rounded-xl p-3 text-center border border-red-100">
                  <p className="text-2xl font-bold text-red-500">{result.failedCount ?? 0}</p>
                  <p className="text-xs text-red-400 mt-0.5">Failed</p>
                </div>
              </div>

              {/* Errors list */}
              {Array.isArray(result.errors) && result.errors.length > 0 && (
                <div className="bg-red-50 rounded-xl p-4 border border-red-100 max-h-48 overflow-y-auto">
                  <p className="text-sm font-semibold text-red-600 mb-2 flex items-center gap-1.5">
                    <AlertCircle className="w-4 h-4" /> Import Errors
                  </p>
                  <ul className="space-y-1">
                    {result.errors.map((e, i) => (
                      <li key={i} className="text-xs text-red-500">
                        <span className="font-mono font-semibold">Row {e.row}:</span> {e.reason}
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {result.failedCount === 0 && (
                <div className="flex items-center gap-2 bg-green-50 rounded-xl p-3 border border-green-100">
                  <CheckCircle2 className="w-5 h-5 text-green-500" />
                  <p className="text-sm text-green-700 font-medium">All students imported successfully!</p>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex gap-3 p-6 pt-0 sticky bottom-0 bg-white rounded-b-2xl">
          {result ? (
            <button onClick={onImported} className="flex-1 px-4 py-2.5 bg-primary text-white rounded-xl text-sm font-medium hover:bg-primary-700 transition-all">
              Done
            </button>
          ) : (
            <>
              <button onClick={onClose} className="flex-1 px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-600 hover:bg-slate-50 transition-all">
                Cancel
              </button>
              <button
                onClick={handleImport}
                disabled={!file || loading}
                className="flex-1 px-4 py-2.5 bg-primary text-white rounded-xl text-sm font-medium hover:bg-primary-700 disabled:opacity-50 transition-all flex items-center justify-center gap-2"
              >
                {loading ? <><Loader2 className="w-4 h-4 animate-spin" /> Importing...</> : <><Upload className="w-4 h-4" /> Import</>}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
