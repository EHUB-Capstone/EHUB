// @ts-nocheck
import { useState, useEffect } from 'react';
import { X, Check, AlertTriangle, Eye, Loader, CheckCircle2, Search, ChevronLeft, ChevronRight } from 'lucide-react';
import toast from 'react-hot-toast';
import { workshopApi } from '../../api/workshopApi';
import { useAuth } from '../../hooks/useAuth';
import { useTranslation } from 'react-i18next';

export default function WorkshopAttendanceManager({ isOpen, onClose, workshop }) {
  const { t } = useTranslation('workspace');
  const { user } = useAuth();
  const [classes, setClasses] = useState([]);
  const [selectedClassId, setSelectedClassId] = useState('');
  const [students, setStudents] = useState([]);
  const [loadingClasses, setLoadingClasses] = useState(false);
  const [loadingStudents, setLoadingStudents] = useState(false);
  const [verifyingStudentId, setVerifyingStudentId] = useState(null);
  const [verifyingAll, setVerifyingAll] = useState(false);

  // Preview State for evidence image
  const [previewImage, setPreviewImage] = useState(null);

  // Rejection State
  const [rejectModalOpen, setRejectModalOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [studentToReject, setStudentToReject] = useState(null);

  // Class list search, filter, and pagination
  const [classSearch, setClassSearch] = useState('');
  const [classModeFilter, setClassModeFilter] = useState('ALL');
  const [classPage, setClassPage] = useState(1);
  const [classesPerPage] = useState(5);

  // Student list search, filter, and pagination
  const [studentSearch, setStudentSearch] = useState('');
  const [studentStatusFilter, setStudentStatusFilter] = useState('ALL');
  const [studentPage, setStudentPage] = useState(1);
  const [studentsPerPage] = useState(10);

  useEffect(() => {
    if (isOpen) {
      setSelectedClassId('');
      setStudents([]);
      setPreviewImage(null);

      // Reset Class States
      setClassSearch('');
      setClassModeFilter('ALL');
      setClassPage(1);

      // Reset Student States
      setStudentSearch('');
      setStudentStatusFilter('ALL');
      setStudentPage(1);

      fetchClasses();
    }
  }, [isOpen, workshop]);

  useEffect(() => {
    if (selectedClassId) {
      setStudentSearch('');
      setStudentStatusFilter('ALL');
      setStudentPage(1);
      fetchAttendance();
    } else {
      setStudents([]);
    }
  }, [selectedClassId]);

  const fetchClasses = async () => {
    setLoadingClasses(true);
    try {
      if (!workshop) return;

      // Extract and format online and offline classes from populated workshop fields
      const onlineList = workshop.onlineClassIds?.map(c => ({
        _id: c._id || c,
        classCode: c.classCode || 'Unknown',
        subjectCode: c.subjectCode || '',
        mode: 'ONLINE'
      })) || [];

      const offlineList = workshop.offlineClassIds?.map(c => ({
        _id: c._id || c,
        classCode: c.classCode || 'Unknown',
        subjectCode: c.subjectCode || '',
        mode: 'OFFLINE'
      })) || [];

      const list = [...onlineList, ...offlineList];
      setClasses(list);
    } catch (err) {
      console.error(err);
      toast.error(t('workshopAttendanceManager.couldNotLoadClassList'));
    } finally {
      setLoadingClasses(false);
    }
  };

  const fetchAttendance = async () => {
    if (!selectedClassId || !workshop?._id) return;
    setLoadingStudents(true);
    try {
      const res = await workshopApi.getAttendance(workshop._id, selectedClassId);
      const list = res.data || res || [];
      setStudents(list);
    } catch (err) {
      console.error(err);
      toast.error(t('workshopAttendanceManager.couldNotLoadAttendanceList'));
    } finally {
      setLoadingStudents(false);
    }
  };

  const handleVerify = async (studentId, status, reason = '') => {
    if (status === 'REJECTED' && !reason) {
      setStudentToReject(studentId);
      setRejectReason('');
      setRejectModalOpen(true);
      return;
    }

    setVerifyingStudentId(studentId);
    try {
      await workshopApi.verifyAttendance(workshop._id, studentId, status, reason);
      toast.success(status === 'VERIFIED' ? t('workshopAttendanceManager.evidenceApproved') : t('workshopAttendanceManager.evidenceRejected'));

      // Update local state realtime
      setStudents(prev => prev.map(s => {
        if (s.studentId === studentId) {
          return { ...s, status, rejectReason: reason };
        }
        return s;
      }));

      if (rejectModalOpen) {
        setRejectModalOpen(false);
        setStudentToReject(null);
      }
    } catch (err) {
      console.error(err);
      toast.error(t('workshopAttendanceManager.couldNotUpdateAttendanceStatus'));
    } finally {
      setVerifyingStudentId(null);
    }
  };

  const handleVerifyAll = async () => {
    const checkedInCount = students.filter(s => s.status === 'CHECKED_IN').length;
    if (checkedInCount === 0) return;

    if (!window.confirm(t('workshopAttendanceManager.areYouSureYouWantTo', { checkedInCount }))) return;

    setVerifyingAll(true);
    try {
      const res = await workshopApi.verifyAllAttendance(workshop._id, selectedClassId);
      toast.success(res.data?.message || res.message || t('workshopAttendanceManager.allAttendanceApprovedSuccessfully'));

      // Update local state realtime
      setStudents(prev => prev.map(s => {
        if (s.status === 'CHECKED_IN') {
          return { ...s, status: 'VERIFIED', rejectReason: '' };
        }
        return s;
      }));
    } catch (err) {
      console.error(err);
      toast.error(err.response?.data?.message || t('workshopAttendanceManager.anErrorOccurredWhileApprovingAll'));
    } finally {
      setVerifyingAll(false);
    }
  };

  // Filter classes
  const filteredClasses = classes.filter(c => {
    const cleanCode = c.classCode.toLowerCase();
    const cleanSubject = (c.subjectCode || '').toLowerCase();
    const query = classSearch.toLowerCase();

    const matchesSearch = cleanCode.includes(query) || cleanSubject.includes(query);
    const matchesMode = classModeFilter === 'ALL' || c.mode === classModeFilter;

    return matchesSearch && matchesMode;
  });

  // Paginate classes
  const totalClassPages = Math.ceil(filteredClasses.length / classesPerPage) || 1;
  const indexOfLastClass = classPage * classesPerPage;
  const indexOfFirstClass = indexOfLastClass - classesPerPage;
  const currentClasses = filteredClasses.slice(indexOfFirstClass, indexOfLastClass);

  // Extract unique majors dynamically from students
  const uniqueMajors = Array.from(
    new Set(
      students
        .map(s => s.major || s.programGroup || '')
        .filter(Boolean)
    )
  );

  // Filter students
  const filteredStudents = students.filter(s => {
    const cleanName = (s.studentName || '').toLowerCase();
    const cleanEmail = (s.email || '').toLowerCase();
    const cleanRoll = (s.rollNumber || '').toLowerCase();
    const query = studentSearch.toLowerCase();

    const matchesSearch = cleanName.includes(query) || cleanEmail.includes(query) || cleanRoll.includes(query);
    const matchesStatus = studentStatusFilter === 'ALL' || s.status === studentStatusFilter;

    return matchesSearch && matchesStatus;
  });

  // Paginate students
  const totalStudentPages = Math.ceil(filteredStudents.length / studentsPerPage) || 1;
  const indexOfLastStudent = studentPage * studentsPerPage;
  const indexOfFirstStudent = indexOfLastStudent - studentsPerPage;
  const currentStudents = filteredStudents.slice(indexOfFirstStudent, indexOfLastStudent);

  if (!isOpen) return null;

  const stats = {
    total: students.length,
    checkedIn: students.filter(s => s.status === 'CHECKED_IN').length,
    verified: students.filter(s => s.status === 'VERIFIED').length,
    rejected: students.filter(s => s.status === 'REJECTED').length,
    notParticipated: students.filter(s => s.status === 'NOT_PARTICIPATED').length,
  };

  const getStatusBadge = (status) => {
    switch (status) {
      case 'NOT_PARTICIPATED':
        return <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-slate-100 text-slate-500">{t('workshopAttendanceManager.notParticipated')}</span>;
      case 'CHECKED_IN':
        return <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-50 text-amber-600 border border-amber-200">{t('workshopAttendanceManager.checkedIn')}</span>;
      case 'VERIFIED':
        return <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-600 border border-emerald-200">{t('workshopAttendanceManager.verified')}</span>;
      case 'REJECTED':
        return <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-rose-50 text-rose-600 border border-rose-200">{t('workshopAttendanceManager.rejected')}</span>;
      default:
        return null;
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Overlay */}
      <div
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Main Container */}
      <div className="relative bg-white rounded-3xl shadow-2xl w-full max-w-5xl mx-4 overflow-hidden border border-slate-100 transform transition-all h-[85vh] flex flex-col animate-in fade-in zoom-in-95 duration-200">
        {/* Header */}
        <div className="flex items-center justify-between px-8 py-6 border-b border-slate-100 bg-slate-50/50 flex-shrink-0">
          <div>
            <h2 className="text-2xl font-bold text-slate-800 flex items-center gap-2">
              <CheckCircle2 className="w-6 h-6 text-primary" />
              {t('workshopAttendanceManager.attendanceManagement')}
            </h2>
            <p className="text-sm text-slate-500 mt-1 max-w-2xl truncate">
              {workshop?.title}
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-2 rounded-xl hover:bg-slate-100 text-slate-400 hover:text-slate-600 transition-colors"
          >
            <X size={24} />
          </button>
        </div>

        {/* Toolbar */}
        {selectedClassId && (
          <div className="px-8 py-4 border-b border-slate-100 bg-white flex flex-col sm:flex-row gap-4 justify-between items-start sm:items-center flex-shrink-0 animate-in fade-in slide-in-from-top-1 duration-200">
            <div className="flex items-center gap-4">
              <button
                type="button"
                onClick={() => setSelectedClassId('')}
                className="flex items-center gap-2 px-3 py-1.5 bg-slate-100 hover:bg-slate-200 active:bg-slate-300 text-slate-700 text-xs font-bold rounded-xl transition-all shadow-xs"
              >
                {t('workshopAttendanceManager.backToClassList')}
              </button>
              <div className="h-6 w-px bg-slate-200" />
              <div className="flex items-center gap-2">
                <span className="text-sm font-semibold text-slate-500">{t('workshopAttendanceManager.selectedClass')}</span>
                <span className="text-sm font-extrabold text-slate-800">
                  {classes.find(c => c._id === selectedClassId)?.classCode} ({classes.find(c => c._id === selectedClassId)?.mode === 'ONLINE' ? t('workshopAttendanceManager.online') : t('workshopAttendanceManager.offline')})
                </span>
              </div>
            </div>

            {students.length > 0 && (
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={handleVerifyAll}
                  disabled={stats.checkedIn === 0 || verifyingAll}
                  className="flex items-center gap-2 px-4 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-emerald-600/50 disabled:cursor-not-allowed text-white text-sm font-bold rounded-xl transition-all shadow-sm active:scale-[0.98]"
                >
                  {verifyingAll ? <Loader className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}
                  {t('workshopAttendanceManager.approveAll')}{stats.checkedIn})
                </button>
              </div>
            )}
          </div>
        )}

        {/* Stats Row */}
        {selectedClassId && students.length > 0 && (
          <div className="px-8 py-3 bg-slate-50/50 border-b border-slate-100 flex flex-wrap gap-4 shrink-0 animate-in fade-in duration-200">
            <div className="text-xs font-semibold text-slate-500 bg-white border border-slate-200 px-3 py-1.5 rounded-lg shadow-sm">
              {t('workshopAttendanceManager.total')} <span className="text-slate-800">{stats.total}</span>
            </div>
            <div className="text-xs font-semibold text-emerald-600 bg-emerald-50 border border-emerald-100 px-3 py-1.5 rounded-lg shadow-sm">
              {t('workshopAttendanceManager.verified2')} <span>{stats.verified}</span>
            </div>
            <div className="text-xs font-semibold text-amber-600 bg-amber-50 border border-amber-100 px-3 py-1.5 rounded-lg shadow-sm">
              {t('workshopAttendanceManager.checkedInPending')} <span>{stats.checkedIn}</span>
            </div>
            <div className="text-xs font-semibold text-rose-600 bg-rose-50 border border-rose-100 px-3 py-1.5 rounded-lg shadow-sm">
              {t('workshopAttendanceManager.rejected2')} <span>{stats.rejected}</span>
            </div>
            <div className="text-xs font-semibold text-slate-400 bg-slate-100 border border-slate-200 px-3 py-1.5 rounded-lg shadow-sm">
              {t('workshopAttendanceManager.notParticipated2')} <span>{stats.notParticipated}</span>
            </div>
          </div>
        )}

        {/* Table/List Area */}
        <div className="flex-1 overflow-y-auto p-8 bg-slate-50/30">
          {loadingStudents || loadingClasses ? (
            <div className="h-full flex items-center justify-center flex-col gap-3">
              <Loader className="w-10 h-10 animate-spin text-primary" />
              <p className="text-sm text-slate-500 font-medium">{t('workshopAttendanceManager.loading')}</p>
            </div>
          ) : !selectedClassId ? (
            <div className="h-full flex flex-col animate-in fade-in duration-300">
              {/* Search & Filter Bar for Classes */}
              <div className="mb-6 flex flex-col md:flex-row gap-4 justify-between items-stretch md:items-center">
                <div className="flex-1 flex gap-3 max-w-md">
                  <div className="relative flex-1">
                    <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 w-4 h-4" />
                    <input
                      type="text"
                      placeholder={t('workshopAttendanceManager.searchByClassCodeSubject')}
                      value={classSearch}
                      onChange={e => {
                        setClassSearch(e.target.value);
                        setClassPage(1);
                      }}
                      className="w-full pl-10 pr-4 py-2 text-sm bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary focus:bg-white transition-all font-semibold text-slate-700"
                    />
                  </div>
                  <select
                    value={classModeFilter}
                    onChange={e => {
                      setClassModeFilter(e.target.value);
                      setClassPage(1);
                    }}
                    className="bg-slate-50 border border-slate-200 rounded-xl px-4 py-2 text-sm font-bold text-slate-700 focus:outline-none cursor-pointer focus:bg-white transition-all"
                  >
                    <option value="ALL">{t('workshopAttendanceManager.allModes')}</option>
                    <option value="ONLINE">{t('workshopAttendanceManager.online2')}</option>
                    <option value="OFFLINE">{t('workshopAttendanceManager.offline2')}</option>
                  </select>
                </div>
                <span className="text-xs font-bold text-slate-400 bg-slate-200/60 px-3 py-1.5 rounded-full shrink-0 self-start md:self-auto">
                  {t('workshopAttendanceManager.classes', { length: filteredClasses.length, length2: classes.length })}
                </span>
              </div>

              {filteredClasses.length === 0 ? (
                <div className="flex-1 flex items-center justify-center text-slate-400 flex-col gap-3 text-center">
                  <AlertTriangle className="w-12 h-12 text-slate-300 animate-bounce" />
                  <h3 className="font-semibold text-slate-700 text-sm">{t('workshopAttendanceManager.noClassesFound')}</h3>
                  <p className="text-xs text-slate-400 mt-1 max-w-xs">{t('workshopAttendanceManager.noClassesMatchYourSearchOr')}</p>
                </div>
              ) : (
                <div className="flex-1 flex flex-col justify-between">
                  <div className="bg-white border border-slate-200/60 rounded-2xl shadow-xs overflow-hidden">
                    <table className="w-full text-left border-collapse">
                      <thead>
                        <tr className="bg-slate-50 border-b border-slate-200">
                          <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider">{t('workshopAttendanceManager.mode')}</th>
                          <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider">{t('workshopAttendanceManager.classCode')}</th>
                          <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider">{t('projectWorkspaceProfile.subject')}</th>
                          <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider text-right">{t('workshopAttendanceManager.action')}</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {currentClasses.map(c => (
                          <tr key={c._id} className="hover:bg-slate-50/40 transition-colors">
                            <td className="px-6 py-4 align-middle">
                              <span className={`inline-flex items-center px-2.5 py-1 rounded-lg text-overline font-bold tracking-wide uppercase ${c.mode === 'ONLINE'
                                ? 'bg-secondary-50 text-secondary border border-secondary-100'
                                : 'bg-amber-50 text-amber-600 border border-amber-100'
                                }`}>
                                {c.mode === 'ONLINE' ? t('workshopAttendanceManager.online2') : t('workshopAttendanceManager.offline2')}
                              </span>
                            </td>
                            <td className="px-6 py-4 align-middle font-bold text-slate-800 text-sm">
                              {c.classCode}
                            </td>
                            <td className="px-6 py-4 align-middle text-sm font-semibold text-slate-500">
                              {c.subjectCode || '—'}
                            </td>
                            <td className="px-6 py-4 align-middle text-right">
                              <button
                                type="button"
                                onClick={() => setSelectedClassId(c._id)}
                                className="inline-flex items-center gap-1.5 px-4 py-2 bg-primary/10 hover:bg-primary text-primary-700 hover:text-white text-xs font-bold rounded-xl transition-all shadow-xs active:scale-95"
                              >
                                <span>{t('workshopAttendanceManager.viewAttendance')}</span>
                                <span className="font-bold">→</span>
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>

                  {/* Pagination for Classes */}
                  {totalClassPages > 1 && (
                    <div className="flex items-center justify-between mt-6 bg-white border border-slate-200/60 px-6 py-3.5 rounded-2xl shadow-xs">
                      <span className="text-xs text-slate-500 font-semibold">
                        {t('workshopAttendanceManager.page')} <span className="text-slate-800 font-bold">{classPage}</span> / <span className="text-slate-800 font-bold">{totalClassPages}</span> {t('workshopAttendanceManager.showing')} {indexOfFirstClass + 1} - {Math.min(indexOfLastClass, filteredClasses.length)} {t('workshopAttendanceManager.of')} {filteredClasses.length})
                      </span>
                      <div className="flex gap-2">
                        <button
                          type="button"
                          disabled={classPage === 1}
                          onClick={() => setClassPage(p => Math.max(p - 1, 1))}
                          className="p-1.5 border border-slate-200 rounded-xl hover:bg-slate-50 text-slate-500 disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                        >
                          <ChevronLeft size={16} />
                        </button>
                        <button
                          type="button"
                          disabled={classPage === totalClassPages}
                          onClick={() => setClassPage(p => Math.min(p + 1, totalClassPages))}
                          className="p-1.5 border border-slate-200 rounded-xl hover:bg-slate-50 text-slate-500 disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                        >
                          <ChevronRight size={16} />
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          ) : filteredStudents.length === 0 ? (
            <div className="h-full flex flex-col animate-in fade-in duration-300">
              {/* Search & Filter Bar for Students */}
              <div className="mb-6 flex flex-col md:flex-row gap-4 justify-between items-stretch md:items-center">
                <div className="flex-1 flex gap-3 max-w-md">
                  <div className="relative flex-1">
                    <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 w-4 h-4" />
                    <input
                      type="text"
                      placeholder={t('workshopAttendanceManager.searchByNameStudentIdEmail')}
                      value={studentSearch}
                      onChange={e => {
                        setStudentSearch(e.target.value);
                        setStudentPage(1);
                      }}
                      className="w-full pl-10 pr-4 py-2 text-sm bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary focus:bg-white transition-all font-semibold text-slate-700"
                    />
                  </div>
                  <select
                    value={studentStatusFilter}
                    onChange={e => {
                      setStudentStatusFilter(e.target.value);
                      setStudentPage(1);
                    }}
                    className="bg-slate-50 border border-slate-200 rounded-xl px-4 py-2 text-sm font-bold text-slate-700 focus:outline-none cursor-pointer focus:bg-white transition-all min-w-[160px]"
                  >
                    <option value="ALL">{t('workshopAttendanceManager.allStatuses')}</option>
                    <option value="CHECKED_IN">{t('workshopAttendanceManager.checkedIn2')}</option>
                    <option value="VERIFIED">{t('workshopAttendanceManager.verified3')}</option>
                    <option value="REJECTED">{t('workshopAttendanceManager.rejected3')}</option>
                    <option value="NOT_PARTICIPATED">{t('workshopAttendanceManager.notParticipated3')}</option>
                  </select>
                </div>
                <span className="text-xs font-bold text-slate-400 bg-slate-200/60 px-3 py-1.5 rounded-full shrink-0 self-start md:self-auto">
                  {t('workshopAttendanceManager.students', { length: filteredStudents.length, length2: students.length })}
                </span>
              </div>
              <div className="flex-1 flex items-center justify-center text-slate-400 flex-col gap-3 text-center">
                <AlertTriangle className="w-12 h-12 text-slate-300" />
                <h3 className="font-semibold text-slate-700 text-sm">{t('workshopAttendanceManager.noStudentsFound')}</h3>
                <p className="text-xs text-slate-400 mt-1 max-w-xs">{t('workshopAttendanceManager.noStudentsMatchYourSearchOr')}</p>
              </div>
            </div>
          ) : (
            <div className="h-full flex flex-col justify-between animate-in fade-in duration-300">
              <div>
                {/* Search & Filter Bar for Students */}
                <div className="mb-6 flex flex-col md:flex-row gap-4 justify-between items-stretch md:items-center">
                  <div className="flex-1 flex gap-3 max-w-md">
                    <div className="relative flex-1">
                      <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 w-4 h-4" />
                      <input
                        type="text"
                        placeholder={t('workshopAttendanceManager.searchByNameStudentIdEmail')}
                        value={studentSearch}
                        onChange={e => {
                          setStudentSearch(e.target.value);
                          setStudentPage(1);
                        }}
                        className="w-full pl-10 pr-4 py-2 text-sm bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary focus:bg-white transition-all font-semibold text-slate-700"
                      />
                    </div>
                    <select
                      value={studentStatusFilter}
                      onChange={e => {
                        setStudentStatusFilter(e.target.value);
                        setStudentPage(1);
                      }}
                      className="bg-slate-50 border border-slate-200 rounded-xl px-4 py-2 text-sm font-bold text-slate-700 focus:outline-none cursor-pointer focus:bg-white transition-all min-w-[160px]"
                    >
                      <option value="ALL">{t('workshopAttendanceManager.allStatuses')}</option>
                      <option value="CHECKED_IN">{t('workshopAttendanceManager.checkedIn2')}</option>
                      <option value="VERIFIED">{t('workshopAttendanceManager.verified3')}</option>
                      <option value="REJECTED">{t('workshopAttendanceManager.rejected3')}</option>
                      <option value="NOT_PARTICIPATED">{t('workshopAttendanceManager.notParticipated3')}</option>
                    </select>
                  </div>
                  <span className="text-xs font-bold text-slate-400 bg-slate-200/60 px-3 py-1.5 rounded-full shrink-0 self-start md:self-auto">
                    {t('workshopAttendanceManager.students', { length: filteredStudents.length, length2: students.length })}
                  </span>
                </div>

                <div className="bg-white border border-slate-200/60 rounded-2xl shadow-sm overflow-hidden">
                  <table className="w-full text-left border-collapse">
                    <thead>
                      <tr className="bg-slate-50 border-b border-slate-200">
                        <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider">{t('rubricForm.student')}</th>
                        <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider">{t('workshopAttendanceManager.studentId')}</th>
                        <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider">{t('workshopAttendanceManager.major')}</th>
                        <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider">{t('workshopAttendanceManager.checkInTime')}</th>
                        <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider text-center">{t('workshopAttendanceManager.evidence')}</th>
                        <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider">{t('taskTableView.status')}</th>
                        <th className="px-6 py-4 text-xs font-bold text-slate-600 uppercase tracking-wider text-right">{t('workshopAttendanceManager.action')}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {currentStudents.map(s => {
                        const checkInDate = s.checkInTime ? new Date(s.checkInTime).toLocaleString('en-US', {
                          year: 'numeric',
                          month: 'short',
                          day: 'numeric',
                          hour: '2-digit',
                          minute: '2-digit'
                        }) : '—';

                        return (
                          <tr key={s.studentId} className="hover:bg-slate-50/40 transition-colors">
                            <td className="px-6 py-4 align-middle">
                              <div className="font-bold text-slate-700 text-sm">{s.studentName}</div>
                              <div className="text-xs text-slate-400 mt-0.5">{s.email}</div>
                              <div className="mt-1">
                                <span className={`text-overline font-semibold px-2 py-0.5 rounded-full ${s.mode === 'ONLINE' ? 'bg-secondary-50 text-secondary border border-secondary-100' : 'bg-amber-50 text-amber-600 border border-amber-100'}`}>
                                  {t('workshopAttendanceManager.mode', { mode: s.mode ? t(`workshopAttendanceManager.modes.${s.mode}`, { defaultValue: s.mode }) : t('workshopAttendanceManager.notAvailable') })}
                                </span>
                              </div>
                            </td>
                            <td className="px-6 py-4 align-middle text-sm font-semibold text-slate-600">
                              {s.rollNumber || '—'}
                            </td>
                            <td className="px-6 py-4 align-middle text-xs font-bold text-slate-500">
                              <span className="bg-slate-100 border border-slate-200 text-slate-600 px-2 py-1 rounded-lg">
                                {s.major || s.programGroup || t('mentoringPanel.nA')}
                              </span>
                            </td>
                            <td className="px-6 py-4 align-middle text-xs font-medium text-slate-500">
                              {checkInDate}
                            </td>
                            <td className="px-6 py-4 align-middle text-center">
                              {s.evidenceUrl ? (
                                <button
                                  type="button"
                                  onClick={() => setPreviewImage(s.evidenceUrl)}
                                  className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-slate-100 hover:bg-slate-200 active:bg-slate-300 text-slate-600 text-xs font-bold rounded-xl transition-all shadow-xs"
                                >
                                  <Eye size={14} />
                                  <span>{t('common:actions.view')}</span>
                                </button>
                              ) : (
                                <span className="text-xs text-slate-300 italic">{t('workshopAttendanceManager.none')}</span>
                              )}
                            </td>
                            <td className="px-6 py-4 align-middle">
                              {getStatusBadge(s.status)}
                              {s.status === 'REJECTED' && s.rejectReason && (
                                <div className="text-overline text-rose-500 mt-1 max-w-[150px] truncate" title={s.rejectReason}>
                                  {t('workshopAttendanceManager.reason', { rejectReason: s.rejectReason })}
                                </div>
                              )}
                            </td>
                            <td className="px-6 py-4 align-middle text-right">
                              {s.status === 'CHECKED_IN' ? (
                                <div className="flex gap-2 justify-end">
                                  <button
                                    type="button"
                                    disabled={verifyingStudentId !== null}
                                    onClick={() => handleVerify(s.studentId, 'VERIFIED')}
                                    className="p-1.5 bg-emerald-50 hover:bg-emerald-100 active:bg-emerald-200 text-emerald-600 hover:text-emerald-700 rounded-xl transition-all disabled:opacity-50"
                                    title={t('workshopAttendanceManager.approve')}
                                  >
                                    <Check size={16} />
                                  </button>
                                  <button
                                    type="button"
                                    disabled={verifyingStudentId !== null}
                                    onClick={() => handleVerify(s.studentId, 'REJECTED')}
                                    className="p-1.5 bg-rose-50 hover:bg-rose-100 active:bg-rose-200 text-rose-600 hover:text-rose-700 rounded-xl transition-all disabled:opacity-50"
                                    title={t('workshopAttendanceManager.reject')}
                                  >
                                    <X size={16} />
                                  </button>
                                </div>
                              ) : s.status === 'VERIFIED' || s.status === 'REJECTED' ? (
                                <button
                                  type="button"
                                  disabled={verifyingStudentId !== null}
                                  onClick={() => handleVerify(s.studentId, s.status === 'VERIFIED' ? 'REJECTED' : 'VERIFIED')}
                                  className="text-xs font-semibold text-slate-400 hover:text-slate-600 underline transition-all disabled:opacity-50"
                                >
                                  {s.status === 'VERIFIED' ? t('workshopAttendanceManager.changeToRejected') : t('workshopAttendanceManager.changeToApproved')}
                                </button>
                              ) : (
                                <span className="text-xs text-slate-300 font-semibold">—</span>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Pagination for Students */}
              {totalStudentPages > 1 && (
                <div className="flex items-center justify-between mt-6 bg-white border border-slate-200/60 px-6 py-3.5 rounded-2xl shadow-xs">
                  <span className="text-xs text-slate-500 font-semibold">
                    {t('workshopAttendanceManager.page')} <span className="text-slate-800 font-bold">{studentPage}</span> / <span className="text-slate-800 font-bold">{totalStudentPages}</span> {t('workshopAttendanceManager.showing')} {indexOfFirstStudent + 1} - {Math.min(indexOfLastStudent, filteredStudents.length)} {t('workshopAttendanceManager.of')} {filteredStudents.length})
                  </span>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      disabled={studentPage === 1}
                      onClick={() => setStudentPage(p => Math.max(p - 1, 1))}
                      className="p-1.5 border border-slate-200 rounded-xl hover:bg-slate-50 text-slate-500 disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                    >
                      <ChevronLeft size={16} />
                    </button>
                    <button
                      type="button"
                      disabled={studentPage === totalStudentPages}
                      onClick={() => setStudentPage(p => Math.min(p + 1, totalStudentPages))}
                      className="p-1.5 border border-slate-200 rounded-xl hover:bg-slate-50 text-slate-500 disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                    >
                      <ChevronRight size={16} />
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Image Preview Overlay Modal */}
      {previewImage && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/80 backdrop-blur-md">
          <div className="relative max-w-4xl max-h-[90vh] bg-white rounded-3xl overflow-hidden shadow-2xl flex flex-col border border-slate-100 animate-in fade-in zoom-in-95 duration-200">
            <button
              onClick={() => setPreviewImage(null)}
              className="absolute top-4 right-4 p-2 bg-black/70 hover:bg-black/90 text-white rounded-xl transition-all shadow-md z-10"
            >
              <X size={20} />
            </button>
            <div className="p-8 overflow-auto flex items-center justify-center min-h-[300px]">
              <img
                src={previewImage}
                alt={t('workshopAttendanceManager.evidenceLargePreview')}
                className="max-w-full max-h-[70vh] object-contain rounded-2xl"
              />
            </div>
            <div className="bg-slate-50 px-8 py-4 border-t border-slate-100 flex justify-end">
              <button
                onClick={() => setPreviewImage(null)}
                className="px-5 py-2.5 bg-slate-200 hover:bg-slate-300 text-slate-700 text-sm font-semibold rounded-xl transition-all"
              >
                {t('common:actions.close')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Reject Reason Modal */}
      {rejectModalOpen && (
        <div className="fixed inset-0 z-[110] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="bg-white rounded-2xl w-full max-w-md border border-slate-200/60 shadow-2xl p-6 animate-in fade-in zoom-in-95 duration-200">
            <h3 className="text-lg font-bold text-slate-800 mb-2">{t('workshopAttendanceManager.rejectionReason')}</h3>
            <p className="text-sm text-slate-500 mb-4">
              {t('workshopAttendanceManager.pleaseProvideAReasonForRejecting')}
            </p>
            <textarea
              className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-3 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all mb-4"
              rows={3}
              placeholder={t('workshopAttendanceManager.eGImageIsBlurryIncorrect')}
              value={rejectReason}
              onChange={e => setRejectReason(e.target.value)}
            />
            <div className="flex justify-end gap-3">
              <button
                type="button"
                onClick={() => {
                  setRejectModalOpen(false);
                  setStudentToReject(null);
                }}
                className="px-4 py-2 border border-slate-200 rounded-xl text-sm font-semibold text-slate-600 hover:bg-slate-100 transition-colors"
              >
                {t('common:actions.cancel')}
              </button>
              <button
                type="button"
                disabled={!rejectReason.trim()}
                onClick={() => handleVerify(studentToReject, 'REJECTED', rejectReason)}
                className="bg-rose-500 text-white font-semibold px-4 py-2 rounded-xl text-sm hover:bg-rose-600 active:scale-[0.98] transition-all disabled:opacity-50"
              >
                {t('workshopAttendanceManager.confirmRejection')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
