import { useState, useRef, useCallback } from 'react';
import toast from 'react-hot-toast';
import {
  X, Upload, FileSpreadsheet, Loader2, CheckCircle2,
  AlertTriangle, HelpCircle, Search, ChevronDown
} from 'lucide-react';
import { classApi } from '../../api/classApi';
import { PROGRAM_GROUPS, getMajorName } from '../../constants/majors';
import { parseApiError } from '../../utils/apiError';
import ConfirmDialog from '../ui/ConfirmDialog';

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
const StatusBadge = ({ status, applied, profileWillChange }) => {
  if (applied && (status === 'matched' || status === 'mismatched'))
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-green-100 text-green-700 rounded-full"><CheckCircle2 className="w-3 h-3" />Verified</span>;
  if (status === 'matched' && profileWillChange)
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-amber-100 text-amber-700 rounded-full"><AlertTriangle className="w-3 h-3" />Profile will change</span>;
  if (status === 'matched')
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-green-100 text-green-700 rounded-full"><CheckCircle2 className="w-3 h-3" />Class matches</span>;
  if (status === 'mismatched')
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-red-100 text-red-600 rounded-full"><AlertTriangle className="w-3 h-3" />Will change</span>;
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
  { key: 'mismatched', label: 'Class will change', color: 'text-red-600', bg: 'bg-red-50', border: 'border-red-200' },
  { key: 'missing',    label: 'Missing',      color: 'text-amber-600',  bg: 'bg-amber-50',  border: 'border-amber-200' },
  { key: 'matched',    label: 'Class matches', color: 'text-green-600', bg: 'bg-green-50', border: 'border-green-200' },
  { key: 'notFound',   label: 'Not found',    color: 'text-slate-500',  bg: 'bg-slate-50',  border: 'border-slate-200' },
];

const majorDiffers = (before, after) =>
  String(before || '').trim().toUpperCase() !== String(after || '').trim().toUpperCase();

// ─── Main Component ────────────────────────────────────────────────────────────
export default function VerifyMajorModal({ classId, onClose, onUpdated }) {
  const inputRef           = useRef(null);
  const [file, setFile]    = useState(null);
  const [loading, setLoading] = useState(false);
  const [applying, setApplying] = useState(false);
  const [report, setReport]   = useState(null);
  const [appliedSummary, setAppliedSummary] = useState<{ enrollments: number; profiles: number } | null>(null);
  const [showApplyConfirm, setShowApplyConfirm] = useState(false);
  const [activeTab, setActiveTab] = useState('mismatched');
  const [search, setSearch]       = useState('');
  const [savingId, setSavingId]   = useState(null); // studentId being saved

  const handleFileDrop = (e) => {
    e.preventDefault();
    if (applying) return;
    const dropped = e.dataTransfer?.files[0] || e.target.files[0];
    if (!dropped) return;
    if (!/\.(xlsx|xls)$/i.test(dropped.name)) {
      toast.error('Only .xlsx or .xls files are supported.');
      return;
    }
    setFile(dropped);
    setReport(null);
    setAppliedSummary(null);
    setShowApplyConfirm(false);
  };

  const handlePreview = async () => {
    if (!file) { toast.error('Please select a file.'); return; }
    setLoading(true);
    try {
      const fd = new FormData();
      fd.append('file', file);
      const res: any = await classApi.previewMajors(classId, fd);
      const data = res?.data || res;
      setReport(data);
      // Default to first tab that has data
      const firstWithData = TABS.find(t => (data[t.key] || []).length > 0);
      setActiveTab(firstWithData?.key || 'matched');
      setAppliedSummary(null);
      toast.success('Preview ready. No majors have been changed.');
    } catch (e) {
      toast.error(parseApiError(e, 'Unable to preview major changes.').message);
    } finally {
      setLoading(false);
    }
  };

  const handleApply = async () => {
    if (!showApplyConfirm || !file || !report || applying || savingId || appliedSummary) return;

    setApplying(true);
    try {
      const formData = new FormData();
      formData.append('file', file);
      const response = await classApi.synchronizeMajorsFromFile(classId, formData);
      const payload = (response as { data?: unknown }).data || response;
      const data = payload as {
        notFound?: unknown[];
        synchronizedEnrollmentCount?: number;
        synchronizedProfileCount?: number;
      };
      setAppliedSummary({
        enrollments: data.synchronizedEnrollmentCount || 0,
        profiles: data.synchronizedProfileCount || 0,
      });
      setShowApplyConfirm(false);
      toast.success(`Updated ${data.synchronizedEnrollmentCount || 0} class major(s) and ${data.synchronizedProfileCount || 0} profile major(s).`);
      onUpdated?.();
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to synchronize majors from the file.').message);
    } finally {
      setApplying(false);
    }
  };

  const handleCorrect = useCallback(async (studentId, newMajor) => {
    const reason = window.prompt('Enter the reason for this major correction:', 'Corrected after major verification review');
    if (!reason) return;
    setSavingId(studentId);
    try {
      await classApi.updateStudentMajor(classId, studentId, newMajor, reason);
      setReport(null);
      setAppliedSummary(null);
      toast.success(`Major updated to ${newMajor}. Preview again before applying the file.`);
      onUpdated?.();
    } catch (e) {
      toast.error(parseApiError(e, 'Failed to update the major.').message);
    } finally {
      setSavingId(null);
    }
  }, [classId, onUpdated]);

  // ── Render rows for active tab ──
  const activeRows = report ? (report[activeTab] || []) : [];
  const filteredRows = search
    ? activeRows.filter(r =>
        [r.fullName, r.rollNumber, r.email].some(v =>
          v?.toLowerCase().includes(search.toLowerCase())
        )
      )
    : activeRows;
  const previewRows = report ? [...(report.matched || []), ...(report.mismatched || [])] : [];
  const classChanges = previewRows.filter(row => majorDiffers(row.majorInDb, row.majorInFile)).length;
  const profileChanges = previewRows.filter(row => majorDiffers(row.majorInProfile, row.majorInFile)).length;

  return (
    <>
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />

      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-3xl max-h-[92vh] flex flex-col animate-scale-in">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <div>
            <h2 className="text-xl font-bold text-slate-900">Verify / Sync Majors</h2>
            <p className="text-sm text-slate-400 mt-0.5">Upload the official Excel file to compare majors, then apply them to the class and profiles.</p>
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
              <div className="rounded-xl border border-indigo-200 bg-indigo-50 p-3 text-sm text-indigo-900">
                {appliedSummary
                  ? `Verified and updated ${appliedSummary.enrollments} class major(s) and ${appliedSummary.profiles} profile major(s). The table below retains the before/after preview.`
                  : `Preview only — no data has changed. The official file would change ${classChanges} class major(s) and ${profileChanges} profile major(s). Review before verifying and updating. Manual correction saves immediately and requires a new preview.`}
                {' '}Other columns, including GroupName, are ignored.
              </div>
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
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Before · Class</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Before · Profile</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">After · Official file (code)</th>
                        <th className="px-4 py-3 text-center text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</th>
                        {activeTab === 'mismatched' && !appliedSummary && (
                          <th className="px-4 py-3 text-center text-xs font-semibold text-slate-500 uppercase tracking-wider">Correct now</th>
                        )}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-50">
                      {filteredRows.map((row, i) => {
                        const isSaving      = savingId === row.studentId;

                        return (
                          <tr key={i} className="hover:bg-slate-50 transition-colors">
                            <td className="px-4 py-3">
                              <div>
                                <p className="font-medium text-slate-800 text-xs">{row.fullName}</p>
                                <p className="text-[11px] text-slate-400 truncate max-w-[160px]">{row.email}</p>
                              </div>
                            </td>
                            <td className="px-4 py-3 font-mono text-xs text-slate-500">{row.rollNumber}</td>
                            <td className="px-4 py-3">
                              <span className="font-mono text-xs text-slate-700">{row.majorInDb || '—'}</span>
                            </td>
                            <td className="px-4 py-3">
                              <span className="font-mono text-xs text-slate-700">{row.majorInProfile || '—'}</span>
                            </td>
                            <td className="px-4 py-3">
                              {row.majorInFile ? (
                                <abbr
                                  title={getMajorName(row.majorInFile) ?? undefined}
                                  className={`font-mono text-xs font-semibold no-underline ${majorDiffers(row.majorInDb, row.majorInFile) || majorDiffers(row.majorInProfile, row.majorInFile) ? 'text-primary' : 'text-slate-700'}`}
                                >
                                  {row.majorInFile}
                                </abbr>
                              ) : <span className="text-xs text-amber-500 font-medium">Missing</span>}
                            </td>
                            <td className="px-4 py-3 text-center">
                              <StatusBadge status={activeTab} applied={Boolean(appliedSummary)} profileWillChange={majorDiffers(row.majorInProfile, row.majorInFile)} />
                            </td>
                            {activeTab === 'mismatched' && !appliedSummary && (
                              <td className="px-4 py-3 text-center">
                                {isSaving ? (
                                  <Loader2 className="w-4 h-4 animate-spin text-primary mx-auto" />
                                ) : row.studentId ? (
                                  <MajorSelect
                                    currentMajor={row.majorInDb || ''}
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
          <button onClick={onClose} disabled={applying} className="flex-1 px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-600 hover:bg-slate-50 transition-all disabled:opacity-50">
            Close
          </button>
          {!report && (
            <button
              onClick={handlePreview}
              disabled={!file || loading}
              className="flex-1 px-4 py-2.5 bg-indigo-500 text-white rounded-xl text-sm font-medium hover:bg-indigo-600 disabled:opacity-50 transition-all flex items-center justify-center gap-2"
            >
              {loading ? <><Loader2 className="w-4 h-4 animate-spin" /> Preparing preview...</> : <>Preview changes</>}
            </button>
          )}
          {report && (
            <>
              <button
                onClick={() => { setReport(null); setFile(null); setAppliedSummary(null); setShowApplyConfirm(false); }}
                disabled={applying}
                className="flex-1 px-4 py-2.5 border border-indigo-200 text-indigo-700 rounded-xl text-sm font-medium hover:bg-indigo-50 disabled:opacity-50"
              >
                Choose another file
              </button>
              <button
                onClick={() => setShowApplyConfirm(true)}
                disabled={applying || Boolean(savingId) || Boolean(appliedSummary) || !file || ((report.matched?.length || 0) + (report.mismatched?.length || 0) === 0)}
                className="flex-1 px-4 py-2.5 bg-indigo-500 text-white rounded-xl text-sm font-medium hover:bg-indigo-600 disabled:opacity-50"
              >
                {applying ? 'Verifying and updating...' : appliedSummary ? 'Verified' : 'Verify majors & update'}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
    <ConfirmDialog
      isOpen={showApplyConfirm}
      onClose={() => { if (!applying) setShowApplyConfirm(false); }}
      onConfirm={handleApply}
      isSubmitting={applying}
      title="Verify and update majors?"
      description={`Apply official majors from this file to ${previewRows.length} active class student(s) and their profiles?${report?.notFound?.length ? ` ${report.notFound.length} unmatched file/class row(s) will not receive a major.` : ''} Existing majors will be replaced.`}
      confirmText="Verify majors & update"
      cancelText="Cancel"
      confirmVariant="gradient"
    />
    </>
  );
}
