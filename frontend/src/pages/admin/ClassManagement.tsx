import { useState, useEffect, useContext, useMemo, useCallback, useRef } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Plus, RefreshCw, Search, Filter, GraduationCap,
  Users, BookOpen, ChevronRight, ChevronLeft, Upload, Eye, Calendar, LayoutGrid, ClipboardCheck, AlertCircle, RotateCcw,
  Archive, UserRoundCheck, X, CircleCheck, Download, Loader2,
  CalendarClock,
} from 'lucide-react';
import { AuthContext } from '../../context/AuthContext';
import { classApi } from '../../api/classApi';
import { subjectApi } from '../../api/subjectApi';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import BulkCreateModal from '../../components/class/BulkCreateModal';
import AssignLectureModal from '../../components/class/AssignLectureModal';
import ImportStudentsModal from '../../components/class/ImportStudentsModal';
import ClassDirectionOverview from '../../components/class/ClassDirectionOverview';
import LecturerCheckpointManagement from '../../components/class/LecturerCheckpointManagement';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import Modal from '../../components/ui/Modal';
import Button from '../../components/ui/Button';
import BulkAssignLecturerModal from '../../components/class/BulkAssignLecturerModal';
import { classFeatureFlags } from '../../config/classFeatureFlags';
import { parseApiError } from '../../utils/apiError';
import { toClassViewModel, unwrapApiData } from '../../utils/classMappers';
import { CLASS_LIST_PAGE_SIZE, getClassLifecyclePresentation } from '../../utils/classComponentPolicy';
import type { ClassListResponse, ClassStatus, ClassViewModel } from '../../types/classes';
import { canCreateClasses, canManageClass, hasClassRole } from '../../utils/classPermissions';
import { executeBulkClassAction, type BulkClassActionResult } from '../../utils/bulkClassActions';
import { useTranslation } from 'react-i18next';

const SEMESTERS = ['SP', 'SU', 'FA'];
const CURRENT_YEAR = new Date().getFullYear();
const YEARS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - 1 + i);

const semesterColor: Record<string, string> = { SP: 'bg-green-100 text-green-700', SU: 'bg-amber-100 text-amber-700', FA: 'bg-blue-100 text-blue-700' };
const statusColor: Record<ClassStatus, string> = {
  Draft: 'bg-amber-50 text-amber-700',
  Active: 'bg-green-50 text-green-700',
  Inactive: 'bg-slate-100 text-slate-600',
  Completed: 'bg-blue-50 text-blue-700',
  Archived: 'bg-red-50 text-red-600',
};
const groupClassesBySubject = (list: ClassViewModel[]) => list.reduce<Array<{ subjectCode: string; classes: ClassViewModel[] }>>((groups, cls) => {
  const subjectCode = cls.subjectCode || 'Unknown Subject';
  const group = groups.find(item => item.subjectCode === subjectCode);
  if (group) {
    group.classes.push(cls);
  } else {
    groups.push({ subjectCode, classes: [cls] });
  }
  return groups;
}, []);

export default function ClassManagement() {
  const { t } = useTranslation('admin');
  const { user } = useContext(AuthContext);
  const navigate  = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const initialSearchParams = useRef(new URLSearchParams(searchParams));
  const isAdmin = hasClassRole(user, 'ADMIN');
  const isLecturer = !isAdmin && hasClassRole(user, 'LECTURER');
  const canCreate = canCreateClasses(user);
  const canManageClassRecord = (classItem: ClassViewModel) => canManageClass(user, classItem);

  const [classes, setClasses] = useState<ClassViewModel[]>([]);
  const [loading,    setLoading]    = useState(true);
  const [error, setError] = useState('');
  const [subjects, setSubjects] = useState<string[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

  // Filters
  const [search, setSearch] = useState(searchParams.get('search') || '');
  const appliedSearch = searchParams.get('search') || '';
  const [previousSearch, setPreviousSearch] = useState(appliedSearch);
  const setAppliedSearch = (value: string) => {
    const next = new URLSearchParams(searchParams);
    if (value) next.set('search', value);
    else next.delete('search');
    next.delete('page');
    if (next.get('tab') === 'checkpoint') next.delete('classId');
    setSearchParams(next, { replace: true });
  };
  const [filterSem, setFilterSem] = useState(searchParams.get('semester') || '');
  const [filterYear, setFilterYear] = useState(searchParams.get('year') || '');
  const [filterSubj, setFilterSubj] = useState(searchParams.get('subject') || '');
  const [filterStatus, setFilterStatus] = useState<ClassStatus | ''>((searchParams.get('status') as ClassStatus | null) || '');
  const [filterAssignment, setFilterAssignment] = useState<'Assigned' | 'Unassigned' | ''>((searchParams.get('assignment') as 'Assigned' | 'Unassigned' | null) || '');
  const [sort, setSort] = useState(searchParams.get('sort') || 'code');
  const [page, setPage] = useState(Math.max(1, Number(searchParams.get('page')) || 1));
  // The shared navbar can submit a search while this route stays mounted.
  // Synchronize before rendering so the URL writer cannot restore stale filters.
  if (previousSearch !== appliedSearch) {
    setPreviousSearch(appliedSearch);
    setSearch(appliedSearch);
    setPage(1);
  }
  const requestedTab = searchParams.get('tab');
  const viewMode = requestedTab === 'overview' ? 'overview' : requestedTab === 'checkpoint' ? 'checkpoint' : 'classes';
  const overviewClassId = searchParams.get('classId') || '';
  const overviewTeamId = searchParams.get('teamId') || '';
  const checkpointNumber = Math.max(0, Number(searchParams.get('checkpointNumber')) || 0);
  const deepLinkSemester = viewMode === 'overview' && overviewClassId
    ? searchParams.get('semester') || ''
    : '';
  const deepLinkYear = viewMode === 'overview' && overviewClassId
    ? searchParams.get('year') || ''
    : '';
  const effectiveFilterSem = deepLinkSemester || filterSem;
  const effectiveFilterYear = deepLinkYear || filterYear;

  // Modals
  const [showBulk,   setShowBulk]   = useState(false);
  const [importTarget, setImportTarget] = useState<string | null>(null);
  const [restoreTarget, setRestoreTarget] = useState<ClassViewModel | null>(null);
  const [assignTarget, setAssignTarget] = useState<ClassViewModel | null>(null);
  const [restoreReason, setRestoreReason] = useState('');
  const [restoring, setRestoring] = useState(false);
  const [selectedClassIds, setSelectedClassIds] = useState<Set<string>>(() => new Set());
  const [bulkAssignOpen, setBulkAssignOpen] = useState(false);
  const [bulkArchiveOpen, setBulkArchiveOpen] = useState(false);
  const [bulkCompleteOpen, setBulkCompleteOpen] = useState(false);
  const [bulkRestoreOpen, setBulkRestoreOpen] = useState(false);
  const [bulkReason, setBulkReason] = useState('');
  const [bulkExporting, setBulkExporting] = useState(false);
  const [bulkBusy, setBulkBusy] = useState(false);
  const [bulkResult, setBulkResult] = useState<{ title: string; result: BulkClassActionResult } | null>(null);

  const fetchAll = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const params = { page, pageSize: CLASS_LIST_PAGE_SIZE, sort } as {
        page: number;
        pageSize: number;
        sort: string;
        semesterCode?: string;
        year?: number;
        subjectCode?: string;
        status?: ClassStatus;
        assignmentStatus?: 'Assigned' | 'Unassigned';
        search?: string;
      };
      if (effectiveFilterSem && effectiveFilterYear) params.semesterCode = `${effectiveFilterSem}${effectiveFilterYear}`;
      if (effectiveFilterYear) params.year = Number(effectiveFilterYear);
      if (filterSubj) params.subjectCode = filterSubj;
      if (filterStatus) params.status = filterStatus;
      if (isAdmin && filterAssignment) params.assignmentStatus = filterAssignment;
      if (appliedSearch) params.search = appliedSearch.trim();

      const clsRes = await classApi.getAll(params);

      const classList = unwrapApiData<ClassListResponse>(clsRes);
      setClasses((classList.items || []).map(toClassViewModel));
      setTotalCount(classList.totalCount || 0);
      setTotalPages(Math.max(1, classList.totalPages || 1));
    } catch (requestError) {
      const parsed = parseApiError(requestError, t('classManagement.failedToLoadClasses'));
      setError(parsed.message);
      toast.error(parsed.message);
    } finally {
      setLoading(false);
    }
  }, [appliedSearch, effectiveFilterSem, effectiveFilterYear, filterAssignment, filterStatus, filterSubj, isAdmin, page, sort]);

  useEffect(() => {
    const loadInitialConfig = async () => {
      try {
        const [subjRes, semRes] = await Promise.all([
          subjectApi.getActive(),
          subjectApi.getCurrentSemester(),
        ]);
        const list = subjRes.data?.subjects || subjRes.subjects || [];
        setSubjects(list.map((s: { subjectCode: string }) => s.subjectCode));

        const activeSem = semRes.data?.currentSemester || semRes.currentSemester;
        if (activeSem && !initialSearchParams.current.has('semester') && !initialSearchParams.current.has('year')) {
          setFilterSem(activeSem.semester);
          setFilterYear(String(activeSem.year));
        }
      } catch (err) {
        console.error('Failed to load initial config', err);
      }
    };
    loadInitialConfig();
  }, []);

  useEffect(() => { void fetchAll(); }, [fetchAll]);

  useEffect(() => {
    if (!overviewClassId) return;
    // Notification deep-links are an external navigation source, including when this route is already mounted.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (deepLinkSemester && deepLinkSemester !== filterSem) setFilterSem(deepLinkSemester);
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (deepLinkYear && deepLinkYear !== filterYear) setFilterYear(deepLinkYear);
  }, [deepLinkSemester, deepLinkYear, filterSem, filterYear, overviewClassId]);

  useEffect(() => {
    const next = new URLSearchParams();
    if (appliedSearch) next.set('search', appliedSearch);
    if (effectiveFilterSem) next.set('semester', effectiveFilterSem);
    if (effectiveFilterYear) next.set('year', effectiveFilterYear);
    if (filterSubj) next.set('subject', filterSubj);
    if (filterStatus) next.set('status', filterStatus);
    if (isAdmin && filterAssignment) next.set('assignment', filterAssignment);
    if (sort !== 'code') next.set('sort', sort);
    if (page > 1) next.set('page', String(page));
    if (viewMode === 'overview' || viewMode === 'checkpoint') {
      next.set('tab', viewMode);
      if (overviewClassId) next.set('classId', overviewClassId);
      if (viewMode === 'overview' && overviewTeamId) next.set('teamId', overviewTeamId);
      if (viewMode === 'checkpoint' && checkpointNumber > 0) next.set('checkpointNumber', String(checkpointNumber));
    }
    setSearchParams(next, { replace: true });
  }, [appliedSearch, checkpointNumber, effectiveFilterSem, effectiveFilterYear, filterAssignment, filterStatus, filterSubj, isAdmin, overviewClassId, overviewTeamId, page, setSearchParams, sort, viewMode]);

  useEffect(() => {
    setSelectedClassIds(new Set());
  }, [appliedSearch, filterAssignment, filterSem, filterStatus, filterSubj, filterYear, page, sort]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setAppliedSearch(search.trim());
  };

  const handleBulkCreated = (options: { keepOpen?: boolean; suppressToast?: boolean } = {}) => {
    if (!options.keepOpen) setShowBulk(false);
    void fetchAll();
    if (!options.suppressToast) toast.success(t('classManagement.classesCreatedSuccessfully'));
  };

  const handleImported = () => {
    void fetchAll();
  };

  const handleRestore = async () => {
    if (!restoreTarget) return;
    setRestoring(true);
    try {
      await classApi.restore(restoreTarget._id, {
        rowVersion: restoreTarget.rowVersion,
        reason: restoreReason.trim(),
      });
      toast.success(t('classManagement.restoredSuccessfully', { classCode: restoreTarget.classCode }));
      setRestoreTarget(null);
      setRestoreReason('');
      await fetchAll();
    } catch (requestError) {
      toast.error(parseApiError(requestError, t('classManagement.failedToRestoreClass')).message);
    } finally {
      setRestoring(false);
    }
  };

  // The API sorts the full filtered result before pagination. Preserve that order
  // instead of re-sorting each page independently in the browser.
  const displayedClasses = classes;
  const subjectGroups = useMemo(() => groupClassesBySubject(classes), [classes]);
  const showSubjectGroups = !filterSubj;
  const selectableClasses = displayedClasses.filter((item) => canManageClassRecord(item));
  const selectedClasses = useMemo(
    () => displayedClasses.filter((item) => selectedClassIds.has(item._id)),
    [displayedClasses, selectedClassIds],
  );
  const assignableSelectedClasses = useMemo(
    () => selectedClasses.filter((item) => item.status !== 'Completed' && item.status !== 'Archived'),
    [selectedClasses],
  );
  const archivableSelectedClasses = useMemo(
    () => selectedClasses.filter((item) => item.status !== 'Archived'),
    [selectedClasses],
  );
  const completableSelectedClasses = useMemo(
    () => selectedClasses.filter((item) => item.status === 'Active'),
    [selectedClasses],
  );
  const restorableSelectedClasses = useMemo(
    () => selectedClasses.filter((item) => item.status === 'Archived'),
    [selectedClasses],
  );
  const allSelectableSelected = selectableClasses.length > 0 && selectableClasses.every((item) => selectedClassIds.has(item._id));

  const toggleClassSelection = (classId: string) => {
    setSelectedClassIds((current) => {
      const next = new Set(current);
      if (next.has(classId)) next.delete(classId);
      else next.add(classId);
      return next;
    });
  };

  const toggleSelectPage = () => {
    setSelectedClassIds(allSelectableSelected ? new Set() : new Set(selectableClasses.map((item) => item._id)));
  };

  const finishBulkAction = async (title: string, result: BulkClassActionResult) => {
    setBulkResult({ title, result });
    setSelectedClassIds(new Set());
    if (result.failed.length === 0) toast.success(t('classManagement.classesUpdatedSuccessfully', { length: result.succeeded.length }));
    else if (result.succeeded.length > 0) toast.success(t('classManagement.succeededFailed', { length: result.succeeded.length, length2: result.failed.length }));
    else toast.error(t('classManagement.allSelectedClassesFailed', { length: result.failed.length }));
    await fetchAll();
  };

  const handleBulkExport = async () => {
    if (!isAdmin || selectedClasses.length === 0) return;
    if (!SEMESTERS.includes(effectiveFilterSem) || !effectiveFilterYear) {
      toast.error(t('classManagement.selectASemesterAndYearBefore'));
      return;
    }

    setBulkExporting(true);
    try {
      const response = await classApi.exportAdminClassData({
        semester: effectiveFilterSem as 'SP' | 'SU' | 'FA',
        year: Number(effectiveFilterYear),
        classIds: selectedClasses.map(item => item._id),
      });
      const blob = new Blob([response.data || response], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${effectiveFilterSem}${effectiveFilterYear}_class_data.xlsx`;
      link.click();
      window.URL.revokeObjectURL(url);
      toast.success(t('classManagement.exportedDataForClassEs', { length: selectedClasses.length }));
    } catch (error) {
      toast.error(parseApiError(error, t('classManagement.failedToExportClassData')).message);
    } finally {
      setBulkExporting(false);
    }
  };

  const handleBulkAssign = async (lecturerId: string) => {
    if (!isAdmin || assignableSelectedClasses.length === 0 || !lecturerId) return;
    setBulkBusy(true);
    try {
      const result = await executeBulkClassAction(
        assignableSelectedClasses,
        (item) => classApi.updateTeachingAssignment(item._id, {
          primaryLecturerId: lecturerId,
          rowVersion: item.rowVersion,
        }),
        'Failed to assign lecturer.',
      );
      setBulkAssignOpen(false);
      await finishBulkAction('Bulk Lecturer Assignment', result);
    } finally {
      setBulkBusy(false);
    }
  };

  const handleBulkArchive = async () => {
    const reason = bulkReason.trim();
    if (archivableSelectedClasses.length === 0 || reason.length < 3) return;
    setBulkBusy(true);
    try {
      const result = await executeBulkClassAction(
        archivableSelectedClasses,
        (item) => classApi.archive(item._id, { rowVersion: item.rowVersion, reason }),
        'Failed to archive class.',
      );
      setBulkArchiveOpen(false);
      setBulkReason('');
      await finishBulkAction('Bulk Class Archive', result);
    } finally {
      setBulkBusy(false);
    }
  };

  const handleBulkComplete = async () => {
    const reason = bulkReason.trim();
    if (completableSelectedClasses.length === 0 || reason.length < 3) return;
    setBulkBusy(true);
    try {
      const result = await executeBulkClassAction(
        completableSelectedClasses,
        (item) => classApi.complete(item._id, { rowVersion: item.rowVersion, reason }),
        'Failed to complete class.',
      );
      setBulkCompleteOpen(false);
      setBulkReason('');
      await finishBulkAction('Bulk Class Completion', result);
    } finally {
      setBulkBusy(false);
    }
  };

  const handleBulkRestore = async () => {
    const reason = bulkReason.trim();
    if (restorableSelectedClasses.length === 0 || reason.length < 3) return;
    setBulkBusy(true);
    try {
      const result = await executeBulkClassAction(
        restorableSelectedClasses,
        (item) => classApi.restore(item._id, { rowVersion: item.rowVersion, reason }),
        'Failed to restore class.',
      );
      setBulkRestoreOpen(false);
      setBulkReason('');
      await finishBulkAction('Bulk Class Restore', result);
    } finally {
      setBulkBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* ── Header ── */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-4">
        <div>
          <h1 className="text-heading sm:text-display text-secondary tracking-tight">{isLecturer ? t('classManagement.myClasses') : t('classManagement.classManagement')}</h1>
          <p className="text-slate-500 mt-1">{t('classManagement.classesFound', { count: totalCount })}</p>
        </div>
        <div className="flex gap-2 flex-wrap">
          <button
            onClick={() => void fetchAll()}
            className="flex items-center gap-2 px-3 py-2 text-sm border border-slate-200 rounded-xl text-slate-600 hover:bg-slate-50 transition-all"
          >
            <RefreshCw className="w-4 h-4" /> {t('common:actions.refresh')}
          </button>
          {canCreate && (
            <button
              id="btn-bulk-create"
              onClick={() => setShowBulk(true)}
              className="flex items-center gap-2 px-4 py-2 text-sm bg-primary text-white rounded-xl hover:bg-primary-700 transition-all shadow-sm hover:shadow-md"
            >
              <Plus className="w-4 h-4" /> {t('classManagement.createClasses')}
            </button>
          )}
        </div>
      </div>

      {isLecturer && (
        <div className="inline-flex rounded-lg border border-slate-200 bg-slate-50 p-1">
          <button
            type="button"
            onClick={() => {
              const next = new URLSearchParams(searchParams);
              next.delete('tab');
              next.delete('classId');
              next.delete('teamId');
              setSearchParams(next);
            }}
            className={`inline-flex items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition ${viewMode === 'classes' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}
          >
            <LayoutGrid className="h-4 w-4" /> {t('classManagement.classes')}
          </button>
          {classFeatureFlags.projectDirection && <button
            type="button"
            onClick={() => {
              const next = new URLSearchParams(searchParams);
              next.set('tab', 'overview');
              next.delete('classId');
              next.delete('teamId');
              setSearchParams(next);
            }}
            className={`inline-flex items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition ${viewMode === 'overview' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}
          >
            <ClipboardCheck className="h-4 w-4" /> {t('classManagement.overview')}
          </button>}
          <button
            type="button"
            onClick={() => {
              const next = new URLSearchParams(searchParams);
              next.set('tab', 'checkpoint');
              next.delete('teamId');
              setSearchParams(next);
            }}
            className={`inline-flex items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition ${viewMode === 'checkpoint' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}
          >
            <CalendarClock className="h-4 w-4" /> {t('classManagement.checkpoint')}
          </button>
        </div>
      )}

      {/* ── Filters ── */}
      <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-4">
        <form onSubmit={handleSearch} className="flex flex-wrap gap-3 items-center">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              type="text"
              placeholder={t('classManagement.searchByClassCode')}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
          <select
            value={filterSubj}
            onChange={(e) => {
              setFilterSubj(e.target.value);
              setPage(1);
              const next = new URLSearchParams(searchParams);
              next.delete('classId');
              setSearchParams(next, { replace: true });
            }}
            className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
          >
            <option value="">{t('classManagement.allSubjects')}</option>
            {subjects.map(s => <option key={s}>{s}</option>)}
          </select>
          <select
            value={filterSem}
            onChange={(e) => {
              setFilterSem(e.target.value);
              setPage(1);
              const next = new URLSearchParams(searchParams);
              next.delete('classId');
              next.delete('teamId');
              setSearchParams(next, { replace: true });
            }}
            className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
          >
            <option value="">{t('classManagement.allSemesters')}</option>
            {SEMESTERS.map(s => <option key={s}>{s}</option>)}
          </select>
          <select
            value={filterYear}
            onChange={(e) => {
              setFilterYear(e.target.value);
              setPage(1);
              const next = new URLSearchParams(searchParams);
              next.delete('classId');
              next.delete('teamId');
              setSearchParams(next, { replace: true });
            }}
            className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
          >
            <option value="">{t('classManagement.allYears')}</option>
            {YEARS.map(y => <option key={y}>{y}</option>)}
          </select>
          <select
            value={filterStatus}
            onChange={(e) => {
              setFilterStatus(e.target.value as ClassStatus | '');
              setPage(1);
              const next = new URLSearchParams(searchParams);
              next.delete('classId');
              setSearchParams(next, { replace: true });
            }}
            className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
          >
            <option value="">{t('classManagement.activeDraft')}</option>
            <option value="Draft">{t('classManagement.draft')}</option>
            <option value="Active">{t('classManagement.active')}</option>
            <option value="Inactive">{t('classManagement.inactive')}</option>
            <option value="Completed">{t('classManagement.completed')}</option>
            {(isAdmin || isLecturer) && <option value="Archived">{t('classManagement.archived')}</option>}
          </select>
          {isAdmin && (
            <select
              value={filterAssignment}
              onChange={(event) => { setFilterAssignment(event.target.value as 'Assigned' | 'Unassigned' | ''); setPage(1); }}
              className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
              aria-label={t('classManagement.filterByLecturerAssignment')}
            >
              <option value="">{t('classManagement.all')}</option>
              <option value="Assigned">{t('classManagement.assigned')}</option>
              <option value="Unassigned">{t('classManagement.unassigned')}</option>
            </select>
          )}
          <select
            value={sort}
            onChange={(e) => { setSort(e.target.value); setPage(1); }}
            className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
            aria-label={t('classManagement.sortClasses')}
          >
            <option value="code">{t('classManagement.codeAZ')}</option>
            <option value="-code">{t('classManagement.codeZA')}</option>
            <option value="classIndex">{t('classManagement.classIndex')}</option>
            <option value="-classIndex">{t('classManagement.classIndex2')}</option>
            <option value="-createdAt">{t('classManagement.newest')}</option>
            <option value="createdAt">{t('classManagement.oldest')}</option>
          </select>
          <button type="submit" className="flex items-center gap-2 px-4 py-2 bg-secondary text-white rounded-xl text-sm hover:bg-secondary-700 transition-all">
            <Filter className="w-4 h-4" /> {t('classManagement.filter')}
          </button>
          <button type="button" onClick={() => {
            setSearch(''); setFilterSem(''); setFilterYear(''); setFilterSubj(''); setFilterStatus(''); setFilterAssignment(''); setSort('code'); setPage(1);
            const next = new URLSearchParams();
            if (viewMode !== 'classes') next.set('tab', viewMode);
            setSearchParams(next, { replace: true });
          }} className="text-sm text-slate-400 hover:text-slate-600 px-2">
            {t('common:actions.reset')}
          </button>
        </form>
      </div>

      {/* ── Class Grid ── */}
      {loading ? (
        <LoadingSkeleton />
      ) : error ? (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center">
          <AlertCircle className="mx-auto h-7 w-7 text-red-500" />
          <p className="mt-2 text-sm font-semibold text-red-800">{t('classManagement.unableToLoadClasses')}</p>
          <p className="mt-1 text-sm text-red-600">{error}</p>
          <button type="button" onClick={() => void fetchAll()} className="mt-3 rounded-lg border border-red-300 bg-white px-3 py-1.5 text-xs font-semibold text-red-700 hover:bg-red-100">{t('classManagement.retry')}</button>
        </div>
      ) : isLecturer && classFeatureFlags.projectDirection && viewMode === 'overview' ? (
        <ClassDirectionOverview
          semester={effectiveFilterSem}
          year={effectiveFilterYear}
          initialClassId={overviewClassId}
          focusTeamId={overviewTeamId}
          onSelectedClassChange={(classId) => {
            const next = new URLSearchParams(searchParams);
            if (classId) next.set('classId', classId);
            else next.delete('classId');
            next.delete('teamId');
            setSearchParams(next, { replace: true });
          }}
        />
      ) : isLecturer && viewMode === 'checkpoint' ? (
        <LecturerCheckpointManagement
          semester={effectiveFilterSem}
          year={effectiveFilterYear}
          subjectCode={filterSubj}
          classStatus={filterStatus}
          search={appliedSearch}
          initialClassId={overviewClassId}
          initialCheckpointNumber={checkpointNumber || undefined}
          onSelectionChange={(classId, selectedCheckpointNumber) => {
            const next = new URLSearchParams(searchParams);
            if (classId) next.set('classId', classId);
            else next.delete('classId');
            if (selectedCheckpointNumber) next.set('checkpointNumber', String(selectedCheckpointNumber));
            else next.delete('checkpointNumber');
            next.delete('teamId');
            setSearchParams(next, { replace: true });
          }}
        />
      ) : classes.length === 0 ? (
        <EmptyState
          icon={GraduationCap}
          title={t('classManagement.noClassesFound')}
          description={isAdmin ? t('classManagement.useCreateClassesToGenerateClasses') : t('classManagement.noClassesAssignedToYouYet')}
          action={canCreate ? { label: t('classManagement.createClasses'), onClick: () => setShowBulk(true) } : undefined}
        />
      ) : (
        <>
        <div className="sticky top-3 z-20 flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white/95 p-3 shadow-sm backdrop-blur sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <label className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-700">
              <input
                type="checkbox"
                checked={allSelectableSelected}
                onChange={toggleSelectPage}
                disabled={selectableClasses.length === 0}
                className="h-4 w-4 rounded border-slate-300 text-primary-700 focus:ring-primary"
              />
              {t('classManagement.selectAll')}
            </label>
            <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-500">
              {t('classManagement.selected', { length: selectedClasses.length })}
            </span>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {isAdmin && (
              <button
                type="button"
                disabled={selectedClasses.length === 0 || bulkBusy || bulkExporting}
                onClick={handleBulkExport}
                className="inline-flex items-center gap-1.5 rounded-lg border border-secondary-200 bg-secondary-50 px-3 py-2 text-xs font-semibold text-secondary transition hover:bg-secondary-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500/25 disabled:cursor-not-allowed disabled:opacity-40"
              >
                {bulkExporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
                {t('classManagement.exportClassData')}{selectedClasses.length})
              </button>
            )}
            {isAdmin && (
              <button
                type="button"
                disabled={assignableSelectedClasses.length === 0 || bulkBusy}
                onClick={() => setBulkAssignOpen(true)}
                className="inline-flex items-center gap-1.5 rounded-lg border border-primary-200 px-3 py-2 text-xs font-semibold text-primary-700 transition hover:bg-primary-50 disabled:cursor-not-allowed disabled:opacity-40"
                title={selectedClasses.length > assignableSelectedClasses.length ? t('classManagement.completedClassesAreExcludedFromLecturer') : undefined}
              >
                <UserRoundCheck className="h-4 w-4" /> {t('classManagement.assignLecturer')}{assignableSelectedClasses.length})
              </button>
            )}
            <button
              type="button"
              disabled={completableSelectedClasses.length === 0 || bulkBusy}
              onClick={() => setBulkCompleteOpen(true)}
              className="inline-flex items-center gap-1.5 rounded-lg border border-green-200 px-3 py-2 text-xs font-semibold text-green-700 transition hover:bg-green-50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <CircleCheck className="h-4 w-4" /> {t('classManagement.complete')}{completableSelectedClasses.length})
            </button>
            <button
              type="button"
              disabled={archivableSelectedClasses.length === 0 || bulkBusy}
              onClick={() => setBulkArchiveOpen(true)}
              className="inline-flex items-center gap-1.5 rounded-lg border border-red-200 px-3 py-2 text-xs font-semibold text-red-600 transition hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <Archive className="h-4 w-4" /> {t('classManagement.archive')}{archivableSelectedClasses.length})
            </button>
            <button
              type="button"
              disabled={restorableSelectedClasses.length === 0 || bulkBusy}
              onClick={() => setBulkRestoreOpen(true)}
              className="inline-flex items-center gap-1.5 rounded-lg border border-blue-200 px-3 py-2 text-xs font-semibold text-blue-700 transition hover:bg-blue-50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <RotateCcw className="h-4 w-4" /> {t('classManagement.restore')}{restorableSelectedClasses.length})
            </button>
            {selectedClasses.length > 0 && (
              <button type="button" onClick={() => setSelectedClassIds(new Set())} className="inline-flex items-center gap-1 rounded-lg px-2.5 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100">
                <X className="h-4 w-4" /> {t('common:actions.clear')}
              </button>
            )}
          </div>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          <AnimatePresence>
            {displayedClasses.map((cls, i) => (
              <div key={cls._id} className="contents">
                {showSubjectGroups && (i === 0 || displayedClasses[i - 1]?.subjectCode !== cls.subjectCode) && (
                  <div className="col-span-full flex items-center gap-3 pt-2 first:pt-0">
                    <h2 className="text-lg font-bold text-slate-900">{cls.subjectCode || t('classManagement.unknownSubject')}</h2>
                    <span className="h-px flex-1 bg-slate-200" />
                    <span className="text-xs font-semibold text-slate-500 bg-slate-100 px-2 py-1 rounded-full">
                      {t('classManagement.classCount', { count: subjectGroups.find(group => group.subjectCode === (cls.subjectCode || 'Unknown Subject'))?.classes.length || 0 })}
                    </span>
                  </div>
                )}
              <motion.div
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: i * 0.04 }}
                className={`bg-white rounded-2xl border shadow-sm hover:shadow-elevated hover:-translate-y-1 transition-all group cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 ${selectedClassIds.has(cls._id) ? 'border-primary ring-2 ring-primary/15' : 'border-slate-200/60'}`}
                onClick={() => navigate(`/classes/${cls.slug || cls._id || cls.id}`)}
                role="link"
                tabIndex={0}
                aria-label={t('classManagement.openClass', { classCode: cls.classCode })}
                onKeyDown={(event) => {
                  // Only react when the card itself has focus, so Enter on the checkbox or buttons inside keeps its own meaning.
                  if (event.target === event.currentTarget && (event.key === 'Enter' || event.key === ' ')) {
                    event.preventDefault();
                    navigate(`/classes/${cls.slug || cls._id || cls.id}`);
                  }
                }}
              >
                <div className="p-5">
                  {/* Top row */}
                  <div className="flex items-start justify-between mb-3">
                    <div>
                      <div className="flex items-center gap-2 mb-1">
                        <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${semesterColor[cls.semester] || 'bg-slate-100 text-slate-600'}`}>
                          {cls.semester} {cls.year}
                        </span>
                        <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-primary-50 text-primary-700">
                          {cls.subjectCode}
                        </span>
                        <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${statusColor[cls.status] || 'bg-slate-100 text-slate-600'}`}>{cls.status}</span>
                      </div>
                      <h3 className="text-xl font-bold text-slate-900">{cls.classCode}</h3>
                    </div>
                    <div className="flex items-center gap-2">
                      {canManageClassRecord(cls) && (
                        <label className="flex h-8 w-8 cursor-pointer items-center justify-center rounded-lg border border-slate-200 bg-white hover:border-primary hover:bg-primary-50" onClick={(event) => event.stopPropagation()}>
                          <input
                            type="checkbox"
                            checked={selectedClassIds.has(cls._id)}
                            onChange={() => toggleClassSelection(cls._id)}
                            aria-label={t('classManagement.select', { classCode: cls.classCode })}
                            className="h-4 w-4 rounded border-slate-300 text-primary-700 focus:ring-primary"
                          />
                        </label>
                      )}
                      <ChevronRight className="w-5 h-5 text-slate-300 group-hover:text-primary transition-colors mt-1" />
                    </div>
                  </div>

                  {/* Lecturer */}
                   {cls.lectureId ? (
                    <div className="flex items-center gap-2 mb-2 p-2 bg-slate-50 rounded-xl">
                      <div className="w-7 h-7 rounded-lg bg-primary-100 flex items-center justify-center shrink-0">
                        <GraduationCap className="w-4 h-4 text-primary" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-xs text-slate-400">{t('classManagement.lecturer')}</p>
                        <p className="text-sm font-medium text-slate-800 truncate">{cls.lectureId?.name || t('classManagement.unknown')}</p>
                      </div>
                      {isAdmin && cls.status !== 'Completed' && cls.status !== 'Archived' && (
                        <button type="button" onClick={(event) => { event.stopPropagation(); setAssignTarget(cls); }} className="rounded-lg px-2 py-1 text-caption font-semibold text-primary-700 hover:bg-primary-50">{t('classManagement.change')}</button>
                      )}
                    </div>
                  ) : (
                    <div className="mb-2 flex items-center justify-between gap-2 rounded-xl border border-amber-100 bg-amber-50 p-2">
                      <p className="text-xs font-medium text-amber-700">{t('classManagement.unassignedDraft')}</p>
                      {isAdmin && cls.status === 'Draft' && (
                        <button type="button" onClick={(event) => { event.stopPropagation(); setAssignTarget(cls); }} className="rounded-lg bg-white px-2 py-1 text-caption font-semibold text-amber-700 shadow-xs hover:bg-amber-100">{t('classManagement.assignLecturer2')}</button>
                      )}
                    </div>
                  )}

                  {/* Mentors */}
                  {cls.mentors && cls.mentors.length > 0 ? (
                    <div className="flex items-center gap-2 mb-2 p-2 bg-slate-50 rounded-xl">
                      <div className="w-7 h-7 rounded-lg bg-amber-100 flex items-center justify-center shrink-0">
                        <Users className="w-4 h-4 text-amber-500" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-xs text-slate-400">{t('classManagement.mentors', { length: cls.mentors.length })}</p>
                        <p className="text-sm font-medium text-slate-800 truncate">
                          {cls.mentors.map(m => m.fullName || 'Unknown').join(', ')}
                        </p>
                      </div>
                    </div>
                  ) : (
                    <div className="mb-2 p-2 bg-slate-50/50 rounded-xl border border-dashed border-slate-200">
                      <p className="text-xs text-slate-400">{t('classManagement.noMentorsAssigned')}</p>
                    </div>
                  )}

                  {/* Schedule */}
                  {cls.schedules.length > 0 ? (
                    <div className="flex items-center gap-2 mb-4 p-2 bg-slate-50 rounded-xl">
                      <div className="w-7 h-7 rounded-lg bg-secondary-100 flex items-center justify-center shrink-0">
                        <Calendar className="w-4 h-4 text-secondary" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-xs text-slate-400">{t('classManagement.weeklySessions', { count: cls.schedules.length })}</p>
                        <p className="text-sm font-medium text-slate-800 truncate">
                          {t('classManagement.scheduleLine', { day: t(`classManagement.weekdays.${cls.schedules[0].dayOfWeek}`), slot: cls.schedules[0].slotNumber, room: cls.schedules[0].room || cls.room || t('classManagement.tbd') })}
                          {cls.schedules.length > 1 ? ` · ${t('classManagement.moreSessions', { count: cls.schedules.length - 1 })}` : ''}
                        </p>
                      </div>
                    </div>
                  ) : (
                    <div className="mb-4 p-2 bg-slate-50/50 rounded-xl border border-dashed border-slate-200">
                      <p className="text-xs text-slate-400">{t('classManagement.scheduleTbd')}</p>
                    </div>
                  )}

                  {/* Stats */}
                  <div className="grid grid-cols-2 gap-2">
                    <div className="flex items-center gap-2 bg-slate-50 rounded-xl p-2.5">
                      <Users className="w-4 h-4 text-secondary" />
                      <div>
                        <p className="text-xs text-slate-400">{t('classManagement.students')}</p>
                        <p className="text-base font-bold text-slate-900">{cls.studentCount ?? 0}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2 bg-slate-50 rounded-xl p-2.5">
                      <BookOpen className="w-4 h-4 text-fpt-green" />
                      <div>
                        <p className="text-xs text-slate-400">{t('classManagement.teams')}</p>
                        <p className="text-base font-bold text-slate-900">{cls.teamCount ?? 0}</p>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Footer actions */}
                <div className="border-t border-slate-100 px-5 py-3 flex gap-2">
                  <button
                    onClick={(e) => { e.stopPropagation(); navigate(`/classes/${cls.slug || cls._id || cls.id}`); }}
                    className="flex-1 flex items-center justify-center gap-1.5 text-xs text-secondary hover:text-secondary-dark font-medium transition-colors"
                  >
                    <Eye className="w-3.5 h-3.5" /> {t('classManagement.viewDetail')}
                  </button>
                  {canManageClassRecord(cls) && getClassLifecyclePresentation(cls.status).action === 'restore' ? (
                    <button
                      onClick={(event) => { event.stopPropagation(); setRestoreTarget(cls); }}
                      className="flex-1 flex items-center justify-center gap-1.5 text-xs text-green-700 hover:text-green-800 font-medium transition-colors border-l border-slate-100"
                    >
                      <RotateCcw className="w-3.5 h-3.5" /> {t('classManagement.restore2')}
                    </button>
                  ) : cls.status !== 'Completed' && canManageClassRecord(cls) && (isAdmin || classFeatureFlags.lecturerStudentImport) && (
                    <button
                      onClick={(e) => { e.stopPropagation(); setImportTarget(cls._id); }}
                      className="flex-1 flex items-center justify-center gap-1.5 text-xs text-primary-700 hover:text-primary-dark font-medium transition-colors border-l border-slate-100"
                    >
                      <Upload className="w-3.5 h-3.5" /> {t('classManagement.importStudents')}
                    </button>
                  )}
                </div>
              </motion.div>
              </div>
            ))}
          </AnimatePresence>
        </div>
        </>
      )}

      {!loading && !error && viewMode === 'classes' && totalPages > 1 && (
        <div className="flex flex-col items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3 sm:flex-row">
          <p className="text-sm text-slate-500">
            {t('classManagement.page')} <span className="font-semibold text-slate-900">{page}</span> {t('classManagement.of')} <span className="font-semibold text-slate-900">{totalPages}</span> · {totalCount} {t('classManagement.classes2')}
          </p>
          <div className="flex gap-2">
            <button type="button" disabled={page <= 1} onClick={() => setPage(current => Math.max(1, current - 1))} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 px-3 py-1.5 text-sm font-medium text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40">
              <ChevronLeft className="h-4 w-4" /> {t('common:actions.previous')}
            </button>
            <button type="button" disabled={page >= totalPages} onClick={() => setPage(current => Math.min(totalPages, current + 1))} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 px-3 py-1.5 text-sm font-medium text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40">
              {t('common:actions.next')} <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}

      {/* ── Modals ── */}
      {canCreate && showBulk && (
        <BulkCreateModal
          onClose={() => setShowBulk(false)}
          onCreated={handleBulkCreated}
        />
      )}

      {isAdmin && assignTarget && (
        <AssignLectureModal
          classId={assignTarget._id}
          semester={assignTarget.semester as 'SP' | 'SU' | 'FA'}
          year={assignTarget.year}
          currentLecture={assignTarget.lectureId}
          rowVersion={assignTarget.rowVersion}
          allowUnassign={assignTarget.status === 'Draft'}
          onClose={() => setAssignTarget(null)}
          onAssigned={async () => {
            setAssignTarget(null);
            await fetchAll();
          }}
        />
      )}
      {isAdmin && (
        <BulkAssignLecturerModal
          isOpen={bulkAssignOpen}
          classes={assignableSelectedClasses}
          isSubmitting={bulkBusy}
          onClose={() => { if (!bulkBusy) setBulkAssignOpen(false); }}
          onAssign={handleBulkAssign}
        />
      )}
      {(isAdmin || (isLecturer && classFeatureFlags.lecturerStudentImport)) && importTarget && (
        <ImportStudentsModal
          classId={importTarget}
          onClose={() => setImportTarget(null)}
          onImported={handleImported}
        />
      )}
      <ConfirmDialog
        isOpen={!!restoreTarget}
        onClose={() => { setRestoreTarget(null); setRestoreReason(''); }}
        onConfirm={handleRestore}
        isSubmitting={restoring}
        title={t('classManagement.restoreThisClass')}
        description={restoreTarget ? t('classManagement.willBeRevalidatedForLecturerSchedule', { classCode: restoreTarget.classCode }) : ''}
        confirmText={t('classManagement.restoreClass')}
        confirmVariant="primary"
        reason={restoreReason}
        onReasonChange={setRestoreReason}
        reasonRequired
      />
      <ConfirmDialog
        isOpen={bulkArchiveOpen}
        onClose={() => { if (!bulkBusy) { setBulkArchiveOpen(false); setBulkReason(''); } }}
        onConfirm={handleBulkArchive}
        isSubmitting={bulkBusy}
        title={t('classManagement.archiveSelectedClasses', { length: archivableSelectedClasses.length })}
        description={t('classManagement.selectedClassesWillBecomeReadOnly')}
        confirmText={t('classManagement.archiveClasses', { length: archivableSelectedClasses.length })}
        confirmVariant="danger"
        reason={bulkReason}
        onReasonChange={setBulkReason}
        reasonRequired
      />
      <ConfirmDialog
        isOpen={bulkCompleteOpen}
        onClose={() => { if (!bulkBusy) { setBulkCompleteOpen(false); setBulkReason(''); } }}
        onConfirm={handleBulkComplete}
        isSubmitting={bulkBusy}
        title={t('classManagement.completeActiveClasses', { length: completableSelectedClasses.length })}
        description={t('classManagement.activeEnrollmentsBecomeCompletedClassChats')}
        confirmText={t('classManagement.completeClasses', { length: completableSelectedClasses.length })}
        confirmVariant="primary"
        reason={bulkReason}
        onReasonChange={setBulkReason}
        reasonRequired
      />
      <ConfirmDialog
        isOpen={bulkRestoreOpen}
        onClose={() => { if (!bulkBusy) { setBulkRestoreOpen(false); setBulkReason(''); } }}
        onConfirm={handleBulkRestore}
        isSubmitting={bulkBusy}
        title={t('classManagement.restoreArchivedClasses', { length: restorableSelectedClasses.length })}
        description={t('classManagement.eachClassIsRevalidatedIndependentlyFor')}
        confirmText={t('classManagement.restoreClasses', { length: restorableSelectedClasses.length })}
        confirmVariant="primary"
        reason={bulkReason}
        onReasonChange={setBulkReason}
        reasonRequired
      />
      <Modal isOpen={Boolean(bulkResult)} onClose={() => setBulkResult(null)} title={bulkResult?.title || t('classManagement.bulkActionResult')} size="lg">
        {bulkResult && (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div className="rounded-xl bg-green-50 p-4 text-center"><p className="text-2xl font-bold text-green-700">{bulkResult.result.succeeded.length}</p><p className="text-xs font-semibold text-green-600">{t('classManagement.succeeded')}</p></div>
              <div className="rounded-xl bg-red-50 p-4 text-center"><p className="text-2xl font-bold text-red-700">{bulkResult.result.failed.length}</p><p className="text-xs font-semibold text-red-600">{t('classManagement.failed')}</p></div>
            </div>
            {bulkResult.result.succeeded.length > 0 && (
              <div><p className="mb-2 text-xs font-bold uppercase tracking-wide text-slate-400">{t('classManagement.updatedClasses')}</p><div className="flex max-h-24 flex-wrap gap-1.5 overflow-y-auto">{bulkResult.result.succeeded.map((code) => <span key={code} className="rounded-md bg-green-50 px-2 py-1 font-mono text-xs font-semibold text-green-700">{code}</span>)}</div></div>
            )}
            {bulkResult.result.failed.length > 0 && (
              <div><p className="mb-2 text-xs font-bold uppercase tracking-wide text-slate-400">{t('classManagement.classesRequiringAttention')}</p><div className="max-h-56 space-y-2 overflow-y-auto">{bulkResult.result.failed.map((failure) => <div key={failure.classId} className="rounded-xl border border-red-100 bg-red-50 p-3"><div className="flex items-center justify-between gap-2"><span className="font-mono text-sm font-bold text-red-800">{failure.classCode}</span>{failure.code && <span className="rounded bg-white px-1.5 py-0.5 text-overline font-semibold text-red-600">{failure.code}</span>}</div><p className="mt-1 text-xs leading-5 text-red-700">{failure.message}</p></div>)}</div></div>
            )}
            <div className="flex justify-end border-t border-slate-100 pt-4"><Button onClick={() => setBulkResult(null)}>{t('common:actions.close')}</Button></div>
          </div>
        )}
      </Modal>
    </div>
  );
}
