import { useState, useRef, useCallback } from 'react';
import toast from 'react-hot-toast';
import {
  X, Upload, FileSpreadsheet, Loader2, CheckCircle2,
  AlertTriangle, HelpCircle, Search, ChevronDown, Lock
} from 'lucide-react';
import { classApi } from '../../api/classApi';
import { PROGRAM_GROUPS, getMajorName } from '../../constants/majors';
import { parseApiError } from '../../utils/apiError';
import ConfirmDialog from '../ui/ConfirmDialog';
import type { TeamMajorWarning } from '../../types/teamManagement';
import { matchesSearchQuery } from '../../utils/searchText';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';

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
  const { t: tr } = useTranslation('class');
  if (applied && (status === 'matched' || status === 'mismatched'))
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-green-100 text-green-700 rounded-full"><CheckCircle2 className="w-3 h-3" />{tr('studentMajorActionCard.verified')}</span>;
  if (status === 'matched' && profileWillChange)
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-amber-100 text-amber-700 rounded-full"><AlertTriangle className="w-3 h-3" />{tr('verifyMajorModal.profileWillChange')}</span>;
  if (status === 'matched')
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-green-100 text-green-700 rounded-full"><CheckCircle2 className="w-3 h-3" />{tr('verifyMajorModal.classMatches')}</span>;
  if (status === 'mismatched')
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-red-100 text-red-600 rounded-full"><AlertTriangle className="w-3 h-3" />{tr('verifyMajorModal.willChange')}</span>;
  if (status === 'missing')
    return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-amber-100 text-amber-700 rounded-full"><AlertTriangle className="w-3 h-3" />{tr('studentTable.missing')}</span>;
  return <span className="flex items-center gap-1 px-2 py-0.5 text-xs font-semibold bg-slate-100 text-slate-500 rounded-full"><HelpCircle className="w-3 h-3" />{tr('verifyMajorModal.notFound')}</span>;
};

// ─── MajorSelect dropdown for manual correction ────────────────────────────────
function MajorSelect({ currentMajor, onSave }) {
  const { t: tr } = useTranslation('class');
  const [open, setOpen]     = useState(false);
  const [value, setValue]   = useState(currentMajor || '');

  return (
    <div className="relative inline-block text-left">
      <button
        onClick={() => setOpen(o => !o)}
        className="flex items-center gap-1 text-xs px-2.5 py-1.5 border border-primary rounded-lg text-primary-700 hover:bg-primary-50 transition-all font-medium"
      >
        {value || tr('verifyMajorModal.selectMajor')} <ChevronDown className="w-3 h-3" />
      </button>
      {open && (
        <div className="absolute z-50 right-0 mt-1 w-52 bg-white rounded-xl border border-slate-200 shadow-lg max-h-60 overflow-y-auto">
          {PROGRAM_GROUPS.map(group => (
            <div key={group.code}>
              <p className="px-3 py-1.5 text-overline font-bold text-slate-400 uppercase tracking-wider bg-slate-50 sticky top-0">
                {group.code}
              </p>
              {group.majors.map(m => (
                <button
                  key={m.code}
                  className={`w-full text-left px-3 py-2 text-xs hover:bg-primary-50 hover:text-primary-700 transition-colors ${value === m.code ? 'bg-primary-50 text-primary-700 font-semibold' : 'text-slate-700'}`}
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
  { key: 'mismatched', get label() { return i18n.t('class:verifyMajorModal.classWillChange'); }, color: 'text-red-600', bg: 'bg-red-50', border: 'border-red-200' },
  { key: 'missing',    get label() { return i18n.t('class:studentTable.missing'); },      color: 'text-amber-600',  bg: 'bg-amber-50',  border: 'border-amber-200' },
  { key: 'matched',    get label() { return i18n.t('class:verifyMajorModal.classMatches'); }, color: 'text-green-600', bg: 'bg-green-50', border: 'border-green-200' },
  { key: 'notFound',   get label() { return i18n.t('class:verifyMajorModal.notFound'); },    color: 'text-slate-500',  bg: 'bg-slate-50',  border: 'border-slate-200' },
];

const majorDiffers = (before, after) =>
  String(before || '').trim().toUpperCase() !== String(after || '').trim().toUpperCase();

// ─── Main Component ────────────────────────────────────────────────────────────
export default function VerifyMajorModal({ classId, onClose, onUpdated }) {
  const { t: tr } = useTranslation('class');
  const inputRef           = useRef(null);
  const [file, setFile]    = useState(null);
  const [loading, setLoading] = useState(false);
  const [applying, setApplying] = useState(false);
  const [report, setReport]   = useState(null);
  const [appliedSummary, setAppliedSummary] = useState<{ enrollments: number; profiles: number; teamWarnings: TeamMajorWarning[]; autoLocked: boolean } | null>(null);
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
      toast.error(tr('importSemesterGroupsModal.onlyXlsxOrXlsFilesAre'));
      return;
    }
    setFile(dropped);
    setReport(null);
    setAppliedSummary(null);
    setShowApplyConfirm(false);
  };

  const handlePreview = async () => {
    if (!file) { toast.error(tr('verifyMajorModal.pleaseSelectAFile')); return; }
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
      toast.success(tr('verifyMajorModal.previewReadyNoMajorsHaveBeen'));
    } catch (e) {
      toast.error(parseApiError(e, tr('verifyMajorModal.unableToPreviewMajorChanges')).message);
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
        teamMajorWarnings?: TeamMajorWarning[];
        majorsAutoLocked?: boolean;
      };
      const teamWarnings = data.teamMajorWarnings || [];
      const autoLocked = Boolean(data.majorsAutoLocked);
      setAppliedSummary({
        enrollments: data.synchronizedEnrollmentCount || 0,
        profiles: data.synchronizedProfileCount || 0,
        teamWarnings,
        autoLocked,
      });
      setShowApplyConfirm(false);
      toast.success(tr('verifyMajorModal.updatedClassMajorSAndProfile', { v1: data.synchronizedEnrollmentCount || 0, v2: data.synchronizedProfileCount || 0 }));
      if (autoLocked) {
        toast.success(tr('verifyMajorModal.allStudentsAreVerifiedMajorUpdates'), { duration: 6000 });
      }
      if (teamWarnings.length > 0) {
        toast(tr('verifyMajorModal.teamSNoLongerMeetThe', { length: teamWarnings.length }), { icon: '⚠️' });
      }
      onUpdated?.();
    } catch (error) {
      toast.error(parseApiError(error, tr('verifyMajorModal.unableToSynchronizeMajorsFromThe')).message);
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
      toast.success(tr('verifyMajorModal.majorUpdatedToPreviewAgainBefore', { newMajor }));
      onUpdated?.();
    } catch (e) {
      toast.error(parseApiError(e, tr('verifyMajorModal.failedToUpdateTheMajor')).message);
    } finally {
      setSavingId(null);
    }
  }, [classId, onUpdated]);

  // ── Render rows for active tab ──
  const activeRows = report ? (report[activeTab] || []) : [];
  const filteredRows = search
    ? activeRows.filter(r => matchesSearchQuery(search, [r.fullName, r.rollNumber, r.email]))
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
            <h2 className="text-xl font-bold text-slate-900">{tr('verifyMajorModal.verifySyncMajors')}</h2>
            <p className="text-sm text-slate-400 mt-0.5">{tr('verifyMajorModal.uploadTheOfficialExcelFileTo')}</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="overflow-y-auto flex-1 p-6 space-y-5">
          {/* Template + Upload */}
          <div className="flex items-center justify-between bg-secondary-50 rounded-xl p-3 border border-secondary-100">
            <div className="flex items-center gap-2">
              <FileSpreadsheet className="w-5 h-5 text-secondary" />
              <div>
                <p className="text-sm font-medium text-secondary">{tr('verifyMajorModal.downloadVerificationTemplate')}</p>
                <p className="text-xs text-slate-400">{tr('verifyMajorModal.requiredColumnsStudentcodeAndMajorcode')}</p>
              </div>
            </div>
            <button
              onClick={() => void downloadTemplate().catch(error => toast.error(parseApiError(error, tr('verifyMajorModal.unableToDownloadTheTemplate')).message))}
              className="text-xs px-3 py-1.5 bg-secondary text-white rounded-lg hover:bg-secondary-700 transition-all font-medium"
            >
              {tr('common:actions.download')}
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
            <Upload className={`w-10 h-10 mx-auto mb-3 ${file ? 'text-primary-700' : 'text-slate-300'}`} />
            {file ? (
              <div>
                <p className="font-semibold text-slate-800">{file.name}</p>
                <p className="text-xs text-slate-400 mt-1">{tr('importSemesterGroupsModal.kbClickToChooseAnotherFile', { v1: (file.size / 1024).toFixed(1) })}</p>
              </div>
            ) : (
              <div>
                <p className="font-medium text-slate-500">{tr('verifyMajorModal.dropAnExcelFileHere')}</p>
                <p className="text-xs text-slate-400 mt-1">{tr('verifyMajorModal.orClickToSelectAFile')}</p>
              </div>
            )}
          </div>

          {/* Results */}
          {report && (
            <div className="space-y-4">
              <div className="rounded-xl border border-secondary-200 bg-secondary-50 p-3 text-sm text-secondary">
                {tr('verifyMajorModal.otherColumnsIncludingGroupnameAreIgnored', { v1: appliedSummary
                  ? `Verified and updated ${appliedSummary.enrollments} class major(s) and ${appliedSummary.profiles} profile major(s). The table below retains the before/after preview.`
                  : `Preview only — no data has changed. The official file would change ${classChanges} class major(s) and ${profileChanges} profile major(s). Review before verifying and updating. Manual correction saves immediately and requires a new preview.` })}
              </div>
              {appliedSummary?.autoLocked && (
                <div role="status" data-testid="verify-major-auto-lock" className="flex items-start gap-2 rounded-xl border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800">
                  <Lock className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                  <div>
                    <p className="font-semibold">{tr('verifyMajorModal.majorUpdatesLockedAutomatically')}</p>
                    <p className="mt-0.5 text-xs text-blue-700">
                      {tr('verifyMajorModal.everyStudentInThisClassIs')}
                    </p>
                  </div>
                </div>
              )}
              {appliedSummary && appliedSummary.teamWarnings.length > 0 && (
                <div role="status" data-testid="verify-team-major-warnings" className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                  <p className="flex items-center gap-2 font-semibold">
                    <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" aria-hidden="true" />
                    {appliedSummary.teamWarnings.length} {tr('verifyMajorModal.teamSDoNotMeetThe')}
                  </p>
                  <p className="mt-1 text-xs text-amber-700">{tr('verifyMajorModal.noTeamOrMemberDataWas')}</p>
                  <ul className="mt-2 space-y-1.5 text-xs">
                    {appliedSummary.teamWarnings.map(warning => (
                      <li key={warning.teamId}>
                        <span className="font-semibold">{warning.teamName}</span>
                        {' — '}{warning.majorComposition.message}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
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
                  placeholder={tr('verifyMajorModal.searchByNameStudentCodeOr')}
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                />
              </div>

              {/* Results table */}
              {filteredRows.length === 0 ? (
                <div className="text-center py-8 text-slate-400 text-sm">
                  {activeRows.length === 0 ? tr('verifyMajorModal.noStudentsInTheGroup', { label: TABS.find(t=>t.key===activeTab)?.label }) : tr('verifyMajorModal.noResultsFound')}
                </div>
              ) : (
                <div className="overflow-x-auto rounded-xl border border-slate-100">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 border-b border-slate-100">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-600 uppercase tracking-wider">{tr('importSemesterGroupsModal.student')}</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-600 uppercase tracking-wider">{tr('importStudentsModal.studentCode')}</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-600 uppercase tracking-wider">{tr('verifyMajorModal.beforeClass')}</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-600 uppercase tracking-wider">{tr('verifyMajorModal.beforeProfile')}</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold text-slate-600 uppercase tracking-wider">{tr('verifyMajorModal.afterOfficialFileCode')}</th>
                        <th className="px-4 py-3 text-center text-xs font-semibold text-slate-600 uppercase tracking-wider">{tr('importSemesterGroupsModal.status')}</th>
                        {activeTab === 'mismatched' && !appliedSummary && (
                          <th className="px-4 py-3 text-center text-xs font-semibold text-slate-600 uppercase tracking-wider">{tr('verifyMajorModal.correctNow')}</th>
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
                                <p className="text-caption text-slate-400 truncate max-w-[160px]">{row.email}</p>
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
                                  className={`font-mono text-xs font-semibold no-underline ${majorDiffers(row.majorInDb, row.majorInFile) || majorDiffers(row.majorInProfile, row.majorInFile) ? 'text-primary-700' : 'text-slate-700'}`}
                                >
                                  {row.majorInFile}
                                </abbr>
                              ) : <span className="text-xs text-amber-500 font-medium">{tr('studentTable.missing')}</span>}
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
            {tr('common:actions.close')}
          </button>
          {!report && (
            <button
              onClick={handlePreview}
              disabled={!file || loading}
              className="flex-1 px-4 py-2.5 bg-secondary text-white rounded-xl text-sm font-medium hover:bg-secondary-700 disabled:opacity-50 transition-all flex items-center justify-center gap-2"
            >
              {loading ? <><Loader2 className="w-4 h-4 animate-spin" /> {tr('verifyMajorModal.preparingPreview')}</> : <>{tr('verifyMajorModal.previewChanges')}</>}
            </button>
          )}
          {report && (
            <>
              <button
                onClick={() => { setReport(null); setFile(null); setAppliedSummary(null); setShowApplyConfirm(false); }}
                disabled={applying}
                className="flex-1 px-4 py-2.5 border border-secondary-200 text-secondary rounded-xl text-sm font-medium hover:bg-secondary-50 disabled:opacity-50"
              >
                {tr('importStudentsModal.chooseAnotherFile')}
              </button>
              <button
                onClick={() => setShowApplyConfirm(true)}
                disabled={applying || Boolean(savingId) || Boolean(appliedSummary) || !file || ((report.matched?.length || 0) + (report.mismatched?.length || 0) === 0)}
                className="flex-1 px-4 py-2.5 bg-secondary text-white rounded-xl text-sm font-medium hover:bg-secondary-700 disabled:opacity-50"
              >
                {applying ? tr('verifyMajorModal.verifyingAndUpdating') : appliedSummary ? tr('studentMajorActionCard.verified') : tr('verifyMajorModal.verifyMajorsUpdate')}
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
      title={tr('verifyMajorModal.verifyAndUpdateMajors')}
      description={tr('verifyMajorModal.applyOfficialMajorsFromThisFile', { length: previewRows.length, v2: report?.notFound?.length ? ` ${tr('verifyMajorModal.unmatchedRows', { count: report.notFound.length })}` : '' })}
      confirmText={tr('verifyMajorModal.verifyMajorsUpdate')}
      cancelText={tr('common:actions.cancel')}
      confirmVariant="primary"
    />
    </>
  );
}
