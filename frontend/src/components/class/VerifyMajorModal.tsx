import { useState, useRef, useCallback } from 'react';
import toast from 'react-hot-toast';
import {
  X, Upload, FileSpreadsheet, Loader2, CheckCircle2,
  AlertTriangle, HelpCircle, Search, ChevronDown
} from 'lucide-react';
import { classApi } from '../../api/classApi';
import { PROGRAM_GROUPS, getMajorName } from '../../constants/majors';
import { parseApiError } from '../../utils/apiError';

const downloadTemplate = async () => {
  const response = await classApi.getMajorVerificationTemplate();
  const blob = new Blob([response.data || response], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = 'Major_Verification_Template.xlsx';
  anchor.click();
  URL.revokeObjectURL(url);
};

// ─── Status badge ──────────────────────────────────────────────────────────────
const StatusBadge = ({ status }) => {
  if (status === 'matched')
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-green-100 text-green-700 rounded-full"><CheckCircle2 className="w-3 h-3" />Matched</span>;
  if (status === 'mismatched')
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-red-100 text-red-600 rounded-full"><AlertTriangle className="w-3 h-3" />Sai</span>;
  if (status === 'missing')
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-amber-100 text-amber-700 rounded-full"><AlertTriangle className="w-3 h-3" />Missing</span>;
  return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-slate-100 text-slate-500 rounded-full"><HelpCircle className="w-3 h-3" />Not found</span>;
};

// ─── MajorSelect dropdown for manual correction ────────────────────────────────
function MajorSelect({ currentMajor, onSave }) {
  const [open, setOpen]     = useState(false);
  const [value, setValue]   = useState(currentMajor || '');

  return (
    <div className="relative inline-block text-left">
      <button
        onClick={() => setOpen(o => !o)}
        className="flex items-center gap-1 text-xs px-2.5 py-1.5 border border-primary rounded-lg text-primary hover:bg-primary-50 transition-all font-medium"
      >
        {value || 'Select major'} <ChevronDown className="w-3 h-3" />
      </button>
      {open && (
        <div className="absolute z-50 right-0 mt-1 w-52 bg-white rounded-xl border border-slate-200 shadow-lg max-h-60 overflow-y-auto">
          {PROGRAM_GROUPS.map(group => (
            <div key={group.code}>
              <p className="px-3 py-1.5 text-[10px] font-bold text-slate-400 uppercase tracking-wider bg-slate-50 sticky top-0">
                {group.code}
              </p>
              {group.majors.map(m => (
                <button
                  key={m.code}
                  className={`w-full text-left px-3 py-2 text-xs hover:bg-primary-50 hover:text-primary transition-colors ${value === m.code ? 'bg-primary-50 text-primary font-semibold' : 'text-slate-700'}`}
                  onClick={() => {
                    setValue(m.code);
                    setOpen(false);
                    onSave(m.code);
                  }}
                >
                  <span className="font-mono">{m.code}</span>
                  <span className="text-slate-400 ml-1">— {m.name}</span>
                </button>
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Tab labels ────────────────────────────────────────────────────────────────
const TABS = [
  { key: 'mismatched', label: 'Sai',         color: 'text-red-600',    bg: 'bg-red-50',    border: 'border-red-200' },
  { key: 'missing',    label: 'Missing',      color: 'text-amber-600',  bg: 'bg-amber-50',  border: 'border-amber-200' },
  { key: 'matched',    label: 'Matched',      color: 'text-green-600',  bg: 'bg-green-50',  border: 'border-green-200' },
  { key: 'notFound',   label: 'Not found',    color: 'text-slate-500',  bg: 'bg-slate-50',  border: 'border-slate-200' },
];

// ─── Main Component ────────────────────────────────────────────────────────────
export default function VerifyMajorModal({ classId, onClose }) {
  const inputRef           = useRef(null);
  const [file, setFile]    = useState(null);
  const [loading, setLoading] = useState(false);
  const [report, setReport]   = useState(null);
  const [activeTab, setActiveTab] = useState('mismatched');
  const [search, setSearch]       = useState('');
  const [savingId, setSavingId]   = useState(null); // studentId being saved

  // Local state for mismatched items so we can mark them corrected
  const [corrected, setCorrected] = useState({}); // { studentId: newMajor }

  const handleFileDrop = (e) => {
    e.preventDefault();
    const dropped = e.dataTransfer?.files[0] || e.target.files[0];
    if (!dropped) return;
    if (!/\.(xlsx|xls)$/i.test(dropped.name)) {
      toast.error('Only .xlsx or .xls files are supported.');
      return;
    }
    setFile(dropped);
    setReport(null);
    setCorrected({});
  };

  const handleVerify = async () => {
    if (!file) { toast.error('Please select a file.'); return; }
    setLoading(true);
    try {
      const fd = new FormData();
      fd.append('file', file);
      const res: any = await classApi.verifyMajors(classId, fd);
      const data = res?.data || res;
      setReport(data);
      // Default to first tab that has data
      const firstWithData = TABS.find(t => (data[t.key] || []).length > 0);
      setActiveTab(firstWithData?.key || 'matched');
      toast.success('Major verification completed.');
    } catch (e) {
      toast.error(parseApiError(e, 'Major verification failed.').message);
    } finally {
      setLoading(false);
    }
  };

  const handleCorrect = useCallback(async (studentId, newMajor) => {
    const reason = window.prompt('Enter the reason for this major correction:', 'Corrected after major verification review');
    if (!reason) return;
    setSavingId(studentId);
    try {
      await classApi.updateStudentMajor(classId, studentId, newMajor, reason);
      setCorrected(prev => ({ ...prev, [studentId]: newMajor }));
      toast.success(`Major updated to ${newMajor}.`);
    } catch (e) {
      toast.error(parseApiError(e, 'Failed to update the major.').message);
    } finally {
      setSavingId(null);
    }
  }, [classId]);

  // ── Render rows for active tab ──
  const activeRows = report ? (report[activeTab] || []) : [];
  const filteredRows = search
    ? activeRows.filter(r =>
        [r.fullName, r.rollNumber, r.email].some(v =>
          v?.toLowerCase().includes(search.toLowerCase())
        )
      )
    : activeRows;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />

      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-3xl max-h-[92vh] flex flex-col animate-scale-in">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <div>
            <h2 className="text-xl font-bold text-slate-900">Verify Majors</h2>
            <p className="text-sm text-slate-400 mt-0.5">Upload an Excel file to compare each student's enrollment major</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="overflow-y-auto flex-1 p-6 space-y-5">
          {/* Template + Upload */}
          <div className="flex items-center justify-between bg-indigo-50 rounded-xl p-3 border border-indigo-100">
            <div className="flex items-center gap-2">
              <FileSpreadsheet className="w-5 h-5 text-indigo-500" />
              <div>
                <p className="text-sm font-medium text-indigo-700">Download verification template</p>
                <p className="text-xs text-slate-400">Required columns: StudentCode and MajorCode</p>
              </div>
            </div>
            <button
              onClick={() => void downloadTemplate().catch(error => toast.error(parseApiError(error, 'Unable to download the template.').message))}
              className="text-xs px-3 py-1.5 bg-indigo-500 text-white rounded-lg hover:bg-indigo-600 transition-all font-medium"
            >
              Download
            </button>
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
                <p className="text-xs text-slate-400 mt-1">{(file.size / 1024).toFixed(1)} KB · Click to choose another file</p>
              </div>
            ) : (
              <div>
                <p className="font-medium text-slate-500">Drop an Excel file here</p>
                <p className="text-xs text-slate-400 mt-1">or click to select a file</p>
              </div>
            )}
          </div>

          {/* Results */}
          {report && (
            <div className="space-y-4">
              {/* Summary cards */}
              <div className="grid grid-cols-4 gap-3">
                {TABS.map(t => {
                  const count = (report[t.key] || []).length;
                  return (
                    <button
                      key={t.key}
                      onClick={() => { setActiveTab(t.key); setSearch(''); }}
                      className={`rounded-xl p-3 text-center border transition-all ${
                        activeTab === t.key
                          ? `${t.bg} ${t.border} ring-2 ring-offset-1 ${t.border.replace('border-', 'ring-')}`
                          : 'bg-slate-50 border-slate-100 hover:bg-slate-100'
                      }`}
                    >
                      <p className={`text-2xl font-bold ${activeTab === t.key ? t.color : 'text-slate-700'}`}>{count}</p>
                      <p className="text-xs text-slate-400 mt-0.5">{t.label}</p>
                    </button>
                  );
                })}
              </div>

              {/* Search within tab */}
              <div className="relative">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <input
                  type="text"
                  placeholder="Search by name, student code, or email..."
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                />
              </div>

              {/* Results table */}
              {filteredRows.length === 0 ? (
                <div className="text-center py-8 text-slate-400 text-sm">
                  {activeRows.length === 0 ? `No students in the "${TABS.find(t=>t.key===activeTab)?.label}" group` : 'No results found'}
                </div>
              ) : (
                <div className="overflow-x-auto rounded-xl border border-slate-100">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 border-b border-slate-100">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Student</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Student code</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Trong file</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Enrollment major</th>
                        <th className="px-4 py-3 text-center text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</th>
                        {activeTab === 'mismatched' && (
                          <th className="px-4 py-3 text-center text-xs font-semibold text-slate-500 uppercase tracking-wider">Correct</th>
                        )}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-50">
                      {filteredRows.map((row, i) => {
                        const isCorrected   = !!corrected[row.studentId];
                        const correctedMajor = corrected[row.studentId];
                        const isSaving      = savingId === row.studentId;
                        const dbMajorDisplay = isCorrected ? correctedMajor : row.majorInDB;

                        return (
                          <tr key={i} className={`hover:bg-slate-50 transition-colors ${isCorrected ? 'opacity-60' : ''}`}>
                            <td className="px-4 py-3">
                              <div>
                                <p className="font-medium text-slate-800 text-xs">{row.fullName}</p>
                                <p className="text-[11px] text-slate-400 truncate max-w-[160px]">{row.email}</p>
                              </div>
                            </td>
                            <td className="px-4 py-3 font-mono text-xs text-slate-500">{row.rollNumber}</td>
                            <td className="px-4 py-3">
                              {row.majorInFile ? (
                                <span className="font-mono text-xs font-semibold text-primary">
                                  {row.majorInFile}
                                  {getMajorName(row.majorInFile) && (
                                    <span className="font-normal text-slate-400 ml-1">— {getMajorName(row.majorInFile)}</span>
                                  )}
                                </span>
                              ) : <span className="text-xs text-slate-400">—</span>}
                            </td>
                            <td className="px-4 py-3">
                              {dbMajorDisplay ? (
                                <span className={`font-mono text-xs font-semibold ${isCorrected ? 'text-green-600' : activeTab === 'mismatched' ? 'text-red-500' : 'text-slate-700'}`}>
                                  {dbMajorDisplay}
                                  {getMajorName(dbMajorDisplay) && (
                                    <span className="font-normal text-slate-400 ml-1">— {getMajorName(dbMajorDisplay)}</span>
                                  )}
                                </span>
                              ) : <span className="text-xs text-amber-500 font-medium">Missing</span>}
                            </td>
                            <td className="px-4 py-3 text-center">
                              {isCorrected
                                ? <span className="flex items-center justify-center gap-1 px-2 py-0.5 text-xs font-semibold bg-green-100 text-green-700 rounded-full"><CheckCircle2 className="w-3 h-3" />Corrected</span>
                                : <StatusBadge status={activeTab} />
                              }
                            </td>
                            {activeTab === 'mismatched' && (
                              <td className="px-4 py-3 text-center">
                                {isCorrected ? (
                                  <span className="text-xs text-green-500">✓</span>
                                ) : isSaving ? (
                                  <Loader2 className="w-4 h-4 animate-spin text-primary mx-auto" />
                                ) : row.studentId ? (
                                  <MajorSelect
                                    currentMajor={row.majorInDB || ''}
                                    onSave={(m) => handleCorrect(row.studentId, m)}
                                  />
                                ) : (
                                  <span className="text-xs text-slate-400">—</span>
                                )}
                              </td>
                            )}
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex gap-3 p-6 pt-0 border-t border-slate-100">
          <button onClick={onClose} className="flex-1 px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-600 hover:bg-slate-50 transition-all">
            Close
          </button>
          {!report && (
            <button
              onClick={handleVerify}
              disabled={!file || loading}
              className="flex-1 px-4 py-2.5 bg-indigo-500 text-white rounded-xl text-sm font-medium hover:bg-indigo-600 disabled:opacity-50 transition-all flex items-center justify-center gap-2"
            >
              {loading ? <><Loader2 className="w-4 h-4 animate-spin" /> Verifying...</> : <>Verify majors</>}
            </button>
          )}
          {report && (
            <button
              onClick={() => { setReport(null); setFile(null); setCorrected({}); }}
              className="flex-1 px-4 py-2.5 bg-indigo-500 text-white rounded-xl text-sm font-medium hover:bg-indigo-600 transition-all"
            >
              Verify again
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
