import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import {
  BookOpen, Building2, Calendar, CheckCircle2, ChevronLeft, ChevronRight, CircleAlert, Edit3, Factory, Filter, GraduationCap, Plus,
  LockKeyhole, RefreshCw, Search, ShieldAlert, Sparkles, UserCheck, Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { subjectApi } from '../../api/subjectApi';
import SemesterDateRangePicker from '../../components/admin/SemesterDateRangePicker';
import MentorAdministrationCard from '../../components/admin/MentorAdministrationCard';
import AddSemesterStaffModal from '../../components/admin/AddSemesterStaffModal';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import EmptyState from '../../components/ui/EmptyState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import Modal from '../../components/ui/Modal';
import { parseApiError } from '../../utils/apiError';
import {
  filterTeachingStaff,
  getStaffKind,
  paginateTeachingStaff,
  type StaffKind,
  type StaffRoleFilter,
  type StaffStatusFilter,
} from '../../utils/teachingStaffDirectory';
import StartupIndustryManagement from './StartupIndustryManagement';

const STAFF_KIND_STYLES: Record<StaffKind, {
  label: string;
  hint: string;
  Icon: typeof Users;
  badge: string;
  avatar: string;
  accent: string;
}> = {
  LECTURER: {
    label: 'Lecturer',
    hint: 'Teaches and manages classes',
    Icon: GraduationCap,
    badge: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-400/40 dark:bg-blue-500/15 dark:text-blue-300',
    avatar: 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-300',
    accent: 'border-l-blue-500',
  },
  ENTERPRISE_MENTOR: {
    label: 'Industry mentor',
    hint: 'Mentor from a company',
    Icon: Building2,
    badge: 'border-orange-200 bg-orange-50 text-orange-700 dark:border-orange-400/40 dark:bg-orange-500/15 dark:text-orange-300',
    avatar: 'bg-orange-100 text-orange-700 dark:bg-orange-500/20 dark:text-orange-300',
    accent: 'border-l-orange-500',
  },
  ACADEMIC_MENTOR: {
    label: 'Lecturer mentor',
    hint: 'Mentor from the faculty',
    Icon: UserCheck,
    badge: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-400/40 dark:bg-emerald-500/15 dark:text-emerald-300',
    avatar: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300',
    accent: 'border-l-emerald-500',
  },
  MENTOR: {
    label: 'Mentor',
    hint: 'Mentor',
    Icon: Users,
    badge: 'border-slate-200 bg-slate-50 text-slate-700 dark:border-white/20 dark:bg-white/10 dark:text-slate-200',
    avatar: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-slate-200',
    accent: 'border-l-slate-400',
  },
};
import type {
  SemesterCode,
  SemesterCompletionPreview,
  SemesterDto,
  SubjectDto,
  SubjectStatus,
  TeachingStaffDto,
  TeachingStaffSummary,
} from '../../types/subjects';

const emptySummary: TeachingStaffSummary = { lecturers: 0, mentors: 0, assigned: 0, unassigned: 0, classes: 0 };
const currentYear = new Date().getFullYear();
const staffPageSizes = [10, 20, 50] as const;

function responseData(response: any) {
  return response?.data ?? response ?? {};
}

function initials(name: string) {
  return name.split(' ').filter(Boolean).slice(0, 2).map((part) => part[0]).join('').toUpperCase() || '?';
}

function semesterLabel({ semester, year }: SemesterDto) {
  return `${semester} ${year}`;
}

function formatSemesterDate(value: string | null) {
  if (!value) return 'Not configured';
  return new Intl.DateTimeFormat('en-GB').format(new Date(`${value}T00:00:00`));
}

function semesterTiming(semester: SemesterDto): 'Not configured' | 'Upcoming' | 'In progress' | 'Ended' {
  if (!semester.startDate || !semester.endDate) return 'Not configured';
  const today = new Date().toISOString().slice(0, 10);
  if (today < semester.startDate) return 'Upcoming';
  if (today > semester.endDate) return 'Ended';
  return 'In progress';
}

function semesterDateRangeError(
  semester: SemesterCode,
  year: number,
  startDate: string,
  endDate: string,
  blockedRanges: Array<{ label: string; startDate: string; endDate: string }>,
): string | undefined {
  if (startDate && !startDate.startsWith(`${year}-`)) {
    return `Start date must belong to ${year}.`;
  }
  if (!startDate || !endDate) return undefined;
  if (endDate <= startDate) return 'End date must be after the start date.';

  const validEndInSelectedYear = endDate.startsWith(`${year}-`);
  const validFallEnd = semester === 'FA' && endDate.startsWith(`${year + 1}-01-`);
  if (!validEndInSelectedYear && !validFallEnd) {
    return semester === 'FA'
      ? `End date must be in ${year}, or January ${year + 1} for Fall.`
      : `End date must belong to ${year}.`;
  }

  const overlappingRange = blockedRanges.find(range => range.startDate <= endDate && range.endDate >= startDate);
  if (overlappingRange) {
    return `This date range overlaps with ${overlappingRange.label}\n(${formatSemesterDate(overlappingRange.startDate)} – ${formatSemesterDate(overlappingRange.endDate)}).`;
  }
  return undefined;
}

const SubjectManagement = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [activeTab, setActiveTab] = useState<'subjects' | 'staff' | 'industries'>(
    searchParams.get('tab') === 'industries' ? 'industries' : 'subjects',
  );
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [subjectsLoading, setSubjectsLoading] = useState(true);
  const [subjectsRefreshing, setSubjectsRefreshing] = useState(false);
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | SubjectStatus>('ALL');
  const [currentSemester, setCurrentSemester] = useState<SemesterDto | null>(null);
  const [semesters, setSemesters] = useState<SemesterDto[]>([]);
  const [semesterScheduleYear, setSemesterScheduleYear] = useState(currentYear);
  const [selectedSemester, setSelectedSemester] = useState<SemesterCode>('SP');
  const [selectedYear, setSelectedYear] = useState(currentYear);
  const [semesterContextReady, setSemesterContextReady] = useState(false);
  const [availableYears, setAvailableYears] = useState<number[]>([currentYear]);
  const [canPlanNextYear, setCanPlanNextYear] = useState(false);
  const [savingSemester, setSavingSemester] = useState(false);
  const [semesterScheduleOpen, setSemesterScheduleOpen] = useState(false);
  const [editingSemesterSchedule, setEditingSemesterSchedule] = useState<SemesterDto | null>(null);
  const [semesterScheduleServerError, setSemesterScheduleServerError] = useState<{ code: string; message: string } | null>(null);
  const [semesterScheduleForm, setSemesterScheduleForm] = useState({
    semester: 'SP' as SemesterCode,
    year: currentYear,
    startDate: '',
    endDate: '',
    reason: '',
  });
  const [staff, setStaff] = useState<TeachingStaffDto[]>([]);
  const [staffSummary, setStaffSummary] = useState<TeachingStaffSummary>(emptySummary);
  const [staffLoading, setStaffLoading] = useState(false);
  const [staffSearch, setStaffSearch] = useState('');
  const [staffRole, setStaffRole] = useState<StaffRoleFilter>('ALL');
  const [staffStatus, setStaffStatus] = useState<StaffStatusFilter>('ALL');
  const [staffPage, setStaffPage] = useState(1);
  const [staffPageSize, setStaffPageSize] = useState<(typeof staffPageSizes)[number]>(10);
  const [staffModalOpen, setStaffModalOpen] = useState(false);
  const [editingStaff, setEditingStaff] = useState<TeachingStaffDto | null>(null);
  const [addStaffRole, setAddStaffRole] = useState<TeachingStaffDto['role'] | null>(null);
  const [staffEntryStatus, setStaffEntryStatus] = useState<'Active' | 'Inactive'>('Active');
  const [staffSaving, setStaffSaving] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingSubject, setEditingSubject] = useState<SubjectDto | null>(null);
  const [form, setForm] = useState({ subjectCode: '', subjectName: '', status: 'active' as SubjectStatus });
  const [savingSubject, setSavingSubject] = useState(false);
  const [disableTarget, setDisableTarget] = useState<SubjectDto | null>(null);
  const [disabling, setDisabling] = useState(false);
  const [semesterLifecycleTarget, setSemesterLifecycleTarget] = useState<{
    semester: SemesterDto;
    action: 'complete' | 'reopen';
    preview?: SemesterCompletionPreview;
  } | null>(null);
  const [semesterLifecycleReason, setSemesterLifecycleReason] = useState('');
  const [semesterLifecycleBusy, setSemesterLifecycleBusy] = useState(false);
  const [semesterTransitionTarget, setSemesterTransitionTarget] = useState<{
    current: SemesterDto;
    target: SemesterDto;
  } | null>(null);
  const [semesterTransitionReason, setSemesterTransitionReason] = useState('');
  const staffRequestId = useRef(0);
  const subjectsRequestId = useRef(0);
  const hasLoadedSubjects = useRef(false);

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(search), 500);
    return () => window.clearTimeout(timeout);
  }, [search]);

  useEffect(() => {
    setStaffPage(1);
  }, [staffSearch, staffRole, staffStatus, staffPageSize, selectedSemester, selectedYear]);

  // Only the first load swaps the table for a skeleton; later searches keep the current rows
  // visible (dimmed) and ignore responses that a newer request has already superseded.
  const loadSubjects = async () => {
    const requestId = ++subjectsRequestId.current;
    if (hasLoadedSubjects.current) setSubjectsRefreshing(true);
    else setSubjectsLoading(true);
    try {
      const params: { search?: string; status?: SubjectStatus } = {};
      if (debouncedSearch.trim()) params.search = debouncedSearch.trim();
      if (statusFilter !== 'ALL') params.status = statusFilter;
      const payload = responseData(await subjectApi.getAll(params));
      if (requestId !== subjectsRequestId.current) return;
      setSubjects(payload.subjects ?? []);
      hasLoadedSubjects.current = true;
    } catch (error) {
      if (requestId !== subjectsRequestId.current) return;
      toast.error(parseApiError(error, 'Failed to load subjects').message);
    } finally {
      if (requestId === subjectsRequestId.current) {
        setSubjectsLoading(false);
        setSubjectsRefreshing(false);
      }
    }
  };

  const loadSemester = async (): Promise<SemesterDto | null> => {
    try {
      const [currentResponse, listResponse] = await Promise.all([
        subjectApi.getCurrentSemester(),
        subjectApi.getSemesters(),
      ]);
      const payload = responseData(currentResponse);
      const listPayload = responseData(listResponse);
      const semester = payload.currentSemester as SemesterDto | undefined;
      const loadedSemesters = (listPayload.semesters ?? []) as SemesterDto[];
      const years = Array.isArray(payload.availableYears) && payload.availableYears.length
        ? payload.availableYears.map(Number)
        : [currentYear];
      if (semester) {
        setCurrentSemester(semester);
        setSemesterScheduleYear(Number(semester.year));
        setSelectedSemester(semester.semester);
        setSelectedYear(Number(semester.year));
      }
      else {
        setCurrentSemester(null);
        setSemesterScheduleYear(current => loadedSemesters.some(item => Number(item.year) === current)
          ? current
          : Number(loadedSemesters[0]?.year ?? currentYear));
      }
      setSemesters(loadedSemesters);
      setAvailableYears(years);
      setCanPlanNextYear(Boolean(payload.isDecember || payload.canPlanNextYear));
      setSemesterContextReady(true);
      return semester ?? null;
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to load the active semester').message);
      return null;
    }
  };

  const loadStaff = async (
    semester: SemesterCode = selectedSemester,
    year: number = selectedYear,
  ) => {
    const requestId = ++staffRequestId.current;
    setStaffLoading(true);
    try {
      const payload = responseData(await subjectApi.getTeachingStaff({ semester, year }));
      if (requestId !== staffRequestId.current) return;
      setStaff(payload.staff ?? payload.teachingStaff ?? []);
      setStaffSummary({ ...emptySummary, ...(payload.summary ?? {}) });
    } catch (error) {
      if (requestId !== staffRequestId.current) return;
      toast.error(parseApiError(error, 'Failed to load teaching staff').message);
      setStaff([]);
      setStaffSummary(emptySummary);
    } finally {
      if (requestId === staffRequestId.current) setStaffLoading(false);
    }
  };

  useEffect(() => { void loadSemester(); }, []);
  useEffect(() => { void loadSubjects(); }, [debouncedSearch, statusFilter]);
  useEffect(() => {
    if (activeTab === 'staff' && semesterContextReady) void loadStaff();
  }, [activeTab, selectedSemester, selectedYear, semesterContextReady]);

  const refresh = async () => {
    const activeSemester = await loadSemester();
    if (activeTab === 'subjects') {
      await loadSubjects();
      return;
    }
    if (activeSemester) {
      await loadStaff(activeSemester.semester, Number(activeSemester.year));
      return;
    }
    await loadStaff();
  };

  const openStaffTab = () => {
    if (currentSemester) {
      setSelectedSemester(currentSemester.semester);
      setSelectedYear(Number(currentSemester.year));
    }
    setActiveTab('staff');
  };

  const openAddStaff = (role: TeachingStaffDto['role']) => {
    const targetSemester = semesters.find(item =>
      item.semester === selectedSemester && item.year === selectedYear);
    if (!targetSemester) {
      toast.error(`Plan ${selectedSemester} ${selectedYear} before adding teaching staff.`);
      return;
    }
    if (targetSemester.status === 'Completed' || targetSemester.status === 'Archived') {
      toast.error('Teaching staff cannot be changed for a completed or archived semester.');
      return;
    }

    setAddStaffRole(role);
  };

  const openEditStaff = (member: TeachingStaffDto) => {
    if (member.isIncomplete) return;
    setEditingStaff(member);
    setStaffEntryStatus(member.status === 'Inactive' ? 'Inactive' : 'Active');
    setStaffModalOpen(true);
  };

  const saveTeachingStaff = async () => {
    if (!editingStaff) return;
    setStaffSaving(true);
    try {
      await subjectApi.updateTeachingStaff(editingStaff._id, {
        status: staffEntryStatus,
        rowVersion: editingStaff.rowVersion,
      });
      toast.success(`${editingStaff.name} updated for ${selectedSemester} ${selectedYear}.`);
      setStaffModalOpen(false);
      await loadStaff();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to update semester teaching staff').message);
    } finally {
      setStaffSaving(false);
    }
  };

  const openAdd = () => {
    setEditingSubject(null);
    setForm({ subjectCode: '', subjectName: '', status: 'active' });
    setModalOpen(true);
  };

  const openEdit = (subject: SubjectDto) => {
    setEditingSubject(subject);
    setForm({ subjectCode: subject.subjectCode, subjectName: subject.subjectName, status: subject.status });
    setModalOpen(true);
  };

  const saveSubject = async () => {
    if (!form.subjectCode.trim() || !form.subjectName.trim()) {
      toast.error('Subject Code and Subject Name are required');
      return;
    }
    setSavingSubject(true);
    try {
      if (editingSubject) {
        await subjectApi.update(editingSubject._id, { subjectName: form.subjectName.trim(), status: form.status });
        toast.success('Subject updated successfully');
      } else {
        await subjectApi.create({ ...form, subjectCode: form.subjectCode.trim(), subjectName: form.subjectName.trim() });
        toast.success('Subject created successfully');
      }
      setModalOpen(false);
      await loadSubjects();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to save subject').message);
    } finally {
      setSavingSubject(false);
    }
  };

  const disableSubject = async () => {
    if (!disableTarget) return;
    setDisabling(true);
    try {
      await subjectApi.delete(disableTarget._id);
      toast.success(`Subject ${disableTarget.subjectCode} disabled successfully`);
      setDisableTarget(null);
      await loadSubjects();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to disable subject').message);
    } finally {
      setDisabling(false);
    }
  };

  const openPlanSemester = () => {
    const initialYear = semesterScheduleYear >= currentYear && semesterScheduleYear <= currentYear + 2
      ? semesterScheduleYear
      : currentYear;
    setEditingSemesterSchedule(null);
    setSemesterScheduleServerError(null);
    setSemesterScheduleForm({ semester: 'SP', year: initialYear, startDate: '', endDate: '', reason: '' });
    setSemesterScheduleOpen(true);
  };

  const openEditSemesterDates = (semester: SemesterDto) => {
    setEditingSemesterSchedule(semester);
    setSemesterScheduleServerError(null);
    setSemesterScheduleForm({
      semester: semester.semester,
      year: semester.year,
      startDate: semester.startDate || '',
      endDate: semester.endDate || '',
      reason: '',
    });
    setSemesterScheduleOpen(true);
  };

  const saveSemesterSchedule = async () => {
    if (!editingSemesterSchedule && (semesterScheduleForm.year < currentYear || semesterScheduleForm.year > currentYear + 2)) {
      toast.error(`Semester year must be between ${currentYear} and ${currentYear + 2}.`);
      return;
    }
    if (!semesterScheduleForm.startDate || !semesterScheduleForm.endDate) {
      toast.error('Start date and end date are required.');
      return;
    }
    if (semesterScheduleForm.endDate <= semesterScheduleForm.startDate) {
      toast.error('End date must be after start date.');
      return;
    }
    const startYear = new Date(`${semesterScheduleForm.startDate}T00:00:00`).getFullYear();
    const endDate = new Date(`${semesterScheduleForm.endDate}T00:00:00`);
    const endYear = endDate.getFullYear();
    const endMonth = endDate.getMonth() + 1; // JS month is 0-indexed

    if (startYear !== semesterScheduleForm.year) {
      toast.error('Start date must belong to the selected semester year.');
      return;
    }

    if (semesterScheduleForm.semester === 'FA') {
      // Fall may spill into January of the following year (e.g. FA2026: Sep 2026 - Jan 2027)
      const validEndInSameYear = endYear === semesterScheduleForm.year;
      const validEndInJanuaryNextYear = endYear === semesterScheduleForm.year + 1 && endMonth === 1;
      if (!validEndInSameYear && !validEndInJanuaryNextYear) {
        toast.error('For Fall, end date must belong to the semester year or fall within January of the following year.');
        return;
      }
    } else if (endYear !== semesterScheduleForm.year) {
      toast.error('Both dates must belong to the selected semester year.');
      return;
    }
    if (editingSemesterSchedule && semesterScheduleForm.reason.trim().length < 3) {
      toast.error('Provide a reason of at least 3 characters when changing semester dates.');
      return;
    }

    setSavingSemester(true);
    try {
      if (editingSemesterSchedule) {
        await subjectApi.updateSemesterDates(editingSemesterSchedule.id, {
          startDate: semesterScheduleForm.startDate,
          endDate: semesterScheduleForm.endDate,
          rowVersion: editingSemesterSchedule.rowVersion,
          reason: semesterScheduleForm.reason.trim(),
        });
        toast.success(`${semesterLabel(editingSemesterSchedule)} dates updated successfully`);
      } else {
        await subjectApi.planSemester({
          semester: semesterScheduleForm.semester,
          year: semesterScheduleForm.year,
          startDate: semesterScheduleForm.startDate,
          endDate: semesterScheduleForm.endDate,
        });
        toast.success(`${semesterScheduleForm.semester} ${semesterScheduleForm.year} planned successfully`);
      }
      setSemesterScheduleOpen(false);
      await loadSemester();
    } catch (error) {
      const parsedError = parseApiError(error, 'Failed to save semester schedule');
      setSemesterScheduleServerError({ code: parsedError.code, message: parsedError.message });
      if (!['SEMESTER_ALREADY_PLANNED', 'SEMESTER_DATE_OVERLAP', 'SEMESTER_DATE_INVALID'].includes(parsedError.code)) {
        toast.error(parsedError.message);
      }
    } finally {
      setSavingSemester(false);
    }
  };

  const activateSemester = async (semester: SemesterDto) => {
    setSavingSemester(true);
    try {
      const payload = responseData(await subjectApi.updateCurrentSemester(semester.semester, semester.year));
      const nextSemester = payload.currentSemester;
      setCurrentSemester(nextSemester);
      setSelectedSemester(nextSemester.semester);
      setSelectedYear(Number(nextSemester.year));
      toast.success(`Active semester set to ${semesterLabel(nextSemester)}`);
      await loadSemester();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to activate semester').message);
    } finally {
      setSavingSemester(false);
    }
  };

  const openCompleteSemester = async (semester: SemesterDto) => {
    setSemesterLifecycleBusy(true);
    try {
      const preview = responseData(await subjectApi.getSemesterCompletionPreview(semester.id));
      setSemesterLifecycleTarget({ semester, action: 'complete', preview });
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to preview semester completion').message);
    } finally {
      setSemesterLifecycleBusy(false);
    }
  };

  const confirmSemesterTransition = async () => {
    if (!semesterTransitionTarget) return;
    setSavingSemester(true);
    try {
      const { current, target } = semesterTransitionTarget;
      await subjectApi.transitionSemester({
        currentSemesterId: current.id,
        currentRowVersion: current.rowVersion,
        targetSemesterId: target.id,
        targetRowVersion: target.rowVersion,
        reason: semesterTransitionReason.trim(),
      });
      toast.success(`${semesterLabel(target)} activated; ${semesterLabel(current)} moved to Closing`);
      setSemesterTransitionTarget(null);
      setSemesterTransitionReason('');
      await loadSemester();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to transition semesters').message);
    } finally {
      setSavingSemester(false);
    }
  };

  const confirmSemesterLifecycle = async () => {
    if (!semesterLifecycleTarget) return;
    setSemesterLifecycleBusy(true);
    try {
      const { semester, action, preview } = semesterLifecycleTarget;
      const payload = {
        rowVersion: preview?.rowVersion ?? semester.rowVersion,
        reason: semesterLifecycleReason.trim(),
      };
      if (action === 'complete') {
        await subjectApi.completeSemester(semester.id, payload);
        toast.success(`${semesterLabel(semester)} completed successfully`);
      } else {
        await subjectApi.reopenSemester(semester.id, payload);
        toast.success(`${semesterLabel(semester)} reopened successfully`);
      }
      setSemesterLifecycleTarget(null);
      setSemesterLifecycleReason('');
      await loadSemester();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to change semester lifecycle').message);
    } finally {
      setSemesterLifecycleBusy(false);
    }
  };

  const visibleStaff = useMemo(() => {
    return filterTeachingStaff(staff, { search: staffSearch, role: staffRole, status: staffStatus });
  }, [staff, staffRole, staffSearch, staffStatus]);

  const staffPagination = paginateTeachingStaff(visibleStaff, staffPage, staffPageSize);
  const staffTotalPages = staffPagination.totalPages;
  const currentStaffPage = staffPagination.page;
  const paginatedStaff = staffPagination.items;
  const staffRangeStart = staffPagination.rangeStart;
  const staffRangeEnd = staffPagination.rangeEnd;
  const hasStaffFilters = Boolean(staffSearch.trim()) || staffRole !== 'ALL' || staffStatus !== 'ALL';

  const existingStaffUserIds = useMemo(() => new Set(
    staff
      .filter(member => member.role === addStaffRole && member.userId)
      .map(member => member.userId as string)), [staff, addStaffRole]);

  const semesterScheduleYears = useMemo(() => Array.from(new Set(
    semesters.map(item => Number(item.year)),
  )).sort((left, right) => left - right), [semesters]);
  const semesterScheduleYearIndex = semesterScheduleYears.indexOf(semesterScheduleYear);
  const previousSemesterScheduleYear = semesterScheduleYearIndex > 0
    ? semesterScheduleYears[semesterScheduleYearIndex - 1]
    : null;
  const nextSemesterScheduleYear = semesterScheduleYearIndex >= 0
    && semesterScheduleYearIndex < semesterScheduleYears.length - 1
    ? semesterScheduleYears[semesterScheduleYearIndex + 1]
    : null;
  const plannedSemestersForScheduleYear = useMemo(() => semesters.filter(item =>
    item.status === 'Planned' && Number(item.year) === semesterScheduleYear), [semesters, semesterScheduleYear]);
  const closingSemesters = useMemo(() => semesters.filter(item => item.status === 'Closing'), [semesters]);
  const semesterScheduleBlockedRanges = useMemo(() => semesters
    .filter(item => item.id !== editingSemesterSchedule?.id
      && Boolean(item.startDate)
      && Boolean(item.endDate))
    .map(item => ({ label: semesterLabel(item), startDate: item.startDate!, endDate: item.endDate! })), [editingSemesterSchedule?.id, semesters]);
  const existingSemesterSchedule = !editingSemesterSchedule
    ? semesters.find(item => item.semester === semesterScheduleForm.semester
      && Number(item.year) === semesterScheduleForm.year)
    : undefined;
  const semesterScheduleDuplicateError = existingSemesterSchedule
    ? `${semesterLabel(existingSemesterSchedule)} has already been planned. You can edit its dates instead.`
    : semesterScheduleServerError?.code === 'SEMESTER_ALREADY_PLANNED'
      ? semesterScheduleServerError.message
      : undefined;
  const semesterScheduleFormError = useMemo(() => semesterDateRangeError(
    semesterScheduleForm.semester,
    semesterScheduleForm.year,
    semesterScheduleForm.startDate,
    semesterScheduleForm.endDate,
    semesterScheduleBlockedRanges,
  ), [semesterScheduleBlockedRanges, semesterScheduleForm]);
  const semesterScheduleDateError = semesterScheduleFormError
    ?? (semesterScheduleServerError?.code === 'SEMESTER_DATE_OVERLAP'
      || semesterScheduleServerError?.code === 'SEMESTER_DATE_INVALID'
      ? semesterScheduleServerError.message
      : undefined);
  const semesterScheduleGeneralError = semesterScheduleServerError
    && !['SEMESTER_ALREADY_PLANNED', 'SEMESTER_DATE_OVERLAP', 'SEMESTER_DATE_INVALID'].includes(semesterScheduleServerError.code)
    ? semesterScheduleServerError.message
    : undefined;
  const semesterScheduleYearAllowed = Boolean(editingSemesterSchedule)
    || (semesterScheduleForm.year >= currentYear && semesterScheduleForm.year <= currentYear + 2);
  const canSaveSemesterSchedule = ['SP', 'SU', 'FA'].includes(semesterScheduleForm.semester)
    && Number.isInteger(semesterScheduleForm.year)
    && semesterScheduleYearAllowed
    && Boolean(semesterScheduleForm.startDate)
    && Boolean(semesterScheduleForm.endDate)
    && !semesterScheduleDuplicateError
    && !semesterScheduleDateError
    && !semesterScheduleGeneralError
    && (!editingSemesterSchedule || semesterScheduleForm.reason.trim().length >= 3);
  const semesterPlanYears = editingSemesterSchedule
    ? [Number(editingSemesterSchedule.year)]
    : [currentYear, currentYear + 1, currentYear + 2];

  const staffStats = [
    { label: 'Lecturers', value: staffSummary.lecturers, icon: GraduationCap, style: 'bg-primary text-white ring-primary-200 dark:ring-primary/50' },
    { label: 'Mentors', value: staffSummary.mentors, icon: Users, style: 'bg-secondary text-white ring-black/10 dark:ring-blue-300/40' },
    { label: 'Needs info', value: staff.filter(member => member.isIncomplete).length, icon: CircleAlert, style: 'bg-warning text-white ring-black/10 dark:ring-amber-300/40' },
    { label: 'Assigned', value: staffSummary.assigned, icon: CheckCircle2, style: 'bg-success text-white ring-black/10 dark:ring-green-300/40' },
    { label: 'Unassigned', value: staffSummary.unassigned, icon: ShieldAlert, style: 'bg-slate-600 text-white ring-black/10 dark:bg-slate-500 dark:ring-slate-300/40' },
    { label: 'Classes', value: staffSummary.classes, icon: BookOpen, style: 'bg-cyan text-white ring-black/10 dark:ring-cyan-300/40' },
  ];
  const selectedSemesterRecord = semesters.find(item => item.semester === selectedSemester && Number(item.year) === selectedYear);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 sm:text-3xl">
            {activeTab === 'subjects' ? 'Subject & Semester' : activeTab === 'industries' ? 'Startup Industries' : 'Lecturers & Mentors'}
          </h1>
          <p className="mt-1 text-slate-500">
            {activeTab === 'subjects'
              ? 'Manage academic subjects and the active semester.'
              : activeTab === 'industries'
                ? 'Manage the standardized startup industries used across E-HUB projects.'
                : 'Review teaching assignments for each semester.'}
          </p>
        </div>
        {activeTab !== 'industries' && (
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" icon={RefreshCw} onClick={() => void refresh()}>Refresh</Button>
            {activeTab === 'subjects' && <Button icon={Plus} onClick={openAdd}>Add Subject</Button>}
          </div>
        )}
      </div>

      <div className="inline-flex w-full gap-1 overflow-x-auto rounded-xl bg-slate-100 p-1 sm:w-auto">
        <button type="button" onClick={() => setActiveTab('subjects')} className={`inline-flex shrink-0 items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition ${activeTab === 'subjects' ? 'bg-white text-primary shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}>
          <BookOpen className="h-4 w-4" /> Subject & Semester
        </button>
        <button type="button" onClick={openStaffTab} className={`inline-flex shrink-0 items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition ${activeTab === 'staff' ? 'bg-white text-primary shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}>
          <Users className="h-4 w-4" /> Lecturers & Mentors by Semester
        </button>
        <button type="button" onClick={() => setActiveTab('industries')} className={`inline-flex shrink-0 items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition ${activeTab === 'industries' ? 'bg-white text-primary shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}>
          <Factory className="h-4 w-4" /> Startup Industries
        </button>
      </div>

      {activeTab === 'industries' ? (
        <StartupIndustryManagement embedded />
      ) : activeTab === 'subjects' ? (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-4">
          <div className="space-y-4 lg:col-span-3">
            <div className="flex flex-col gap-3 sm:flex-row">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search subjects by code or name..." className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-10 pr-4 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
              </div>
              <div className="relative sm:w-44">
                <Filter className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as typeof statusFilter)} className="w-full appearance-none rounded-xl border border-slate-200 bg-white py-2 pl-10 pr-4 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20">
                  <option value="ALL">All Status</option><option value="active">Active</option><option value="disabled">Disabled</option>
                </select>
              </div>
            </div>
            <div className="overflow-hidden rounded-2xl border border-slate-200/70 bg-white shadow-sm">
              {subjectsLoading ? <LoadingSkeleton variant="table" lines={6} className="p-4" /> : subjects.length === 0 ? <EmptyState icon={BookOpen} title="No subjects found" description="Try adjusting your search or add a new subject." action={{ label: 'Add Subject', onClick: openAdd }} /> : (
                <div aria-busy={subjectsRefreshing || undefined} className={`overflow-x-auto transition-opacity duration-150 ${subjectsRefreshing ? 'opacity-60' : 'opacity-100'}`}><table className="w-full min-w-[620px]"><thead><tr className="border-b border-slate-100 bg-slate-50"><th className="px-6 py-3 text-left text-xs font-semibold uppercase text-slate-400">Subject Code</th><th className="px-6 py-3 text-left text-xs font-semibold uppercase text-slate-400">Subject Name</th><th className="px-6 py-3 text-left text-xs font-semibold uppercase text-slate-400">Status</th><th className="px-6 py-3 text-right text-xs font-semibold uppercase text-slate-400">Actions</th></tr></thead>
                  <tbody>{subjects.map((subject) => <tr key={subject._id} className="group border-b border-slate-50 last:border-0 hover:bg-slate-50/80"><td className="px-6 py-3.5"><div className="flex items-center gap-3"><span className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary-50 font-mono text-xs font-bold text-primary">{subject.subjectCode.slice(0, 3)}</span><Link to={`/admin/subjects/${subject.subjectCode}`} className="font-mono font-semibold text-slate-900 hover:text-primary hover:underline">{subject.subjectCode}</Link></div></td><td className="px-6 py-3.5 text-sm font-medium text-slate-700">{subject.subjectName}</td><td className="px-6 py-3.5"><Badge variant={subject.status === 'active' ? 'Active' : 'Overdue'}>{subject.status === 'active' ? 'Active' : 'Disabled'}</Badge></td><td className="px-6 py-3.5"><div className="flex justify-end gap-1 opacity-100 transition-opacity sm:opacity-0 sm:group-hover:opacity-100"><button type="button" title="Edit Subject" onClick={() => openEdit(subject)} className="rounded-lg p-2 text-slate-400 hover:bg-primary-50 hover:text-primary"><Edit3 className="h-4 w-4" /></button>{subject.status !== 'disabled' && <button type="button" title="Disable Subject" onClick={() => setDisableTarget(subject)} className="rounded-lg p-2 text-slate-400 hover:bg-danger-50 hover:text-danger"><LockKeyhole className="h-4 w-4" /></button>}</div></td></tr>)}</tbody></table></div>
              )}</div>
          </div>
          <aside className="rounded-2xl border border-slate-200/70 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between gap-2 border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2"><Calendar className="h-5 w-5 text-primary" /><h2 className="font-bold text-slate-800">Semester Schedule</h2></div>
              <button type="button" onClick={openPlanSemester} className="rounded-lg p-1.5 text-primary hover:bg-primary-50" title="Plan a semester"><Plus className="h-4 w-4" /></button>
            </div>
            <div className={`mt-4 rounded-xl border p-3 ${currentSemester && semesterTiming(currentSemester) === 'Ended' ? 'border-red-200 bg-red-50' : 'border-slate-100 bg-slate-50'}`}>
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="text-xs font-medium text-slate-400">Active semester</p>
                  <p className="mt-1 text-lg font-bold text-slate-900">{currentSemester ? semesterLabel(currentSemester) : 'None'}</p>
                </div>
                {currentSemester && <button type="button" onClick={() => openEditSemesterDates(currentSemester)} className="rounded-lg p-1.5 text-slate-400 hover:bg-white hover:text-primary" title="Edit semester dates"><Edit3 className="h-4 w-4" /></button>}
              </div>
              {currentSemester && (
                <>
                  <p className="mt-1 text-xs text-slate-500">{formatSemesterDate(currentSemester.startDate)} – {formatSemesterDate(currentSemester.endDate)}</p>
                  <p className={`mt-1 text-xs font-semibold ${semesterTiming(currentSemester) === 'Ended' ? 'text-red-700' : 'text-green-700'}`}>{semesterTiming(currentSemester)}</p>
                </>
              )}
            </div>
            {currentSemester && semesterTiming(currentSemester) === 'Ended' && (
              <p className="mt-2 rounded-lg bg-red-50 px-2.5 py-2 text-xs leading-5 text-red-700">This Active semester has passed its configured end date. Complete it or correct its dates before creating classes.</p>
            )}
            {currentSemester && (
              <Button variant="outline" className="mt-3 w-full" onClick={() => void openCompleteSemester(currentSemester)} isLoading={semesterLifecycleBusy}>
                Complete Active Semester
              </Button>
            )}

            <div className="mt-4 border-t border-slate-100 pt-3">
              <div className="flex items-center justify-between gap-2">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Planned semesters</p>
                <button type="button" onClick={openPlanSemester} className="text-xs font-semibold text-primary">Add</button>
              </div>
              <div className="mt-2 flex items-center justify-between rounded-lg border border-slate-100 bg-slate-50 px-2 py-1.5">
                <button
                  type="button"
                  disabled={previousSemesterScheduleYear === null}
                  onClick={() => previousSemesterScheduleYear !== null && setSemesterScheduleYear(previousSemesterScheduleYear)}
                  className="rounded-md p-1 text-slate-500 hover:bg-white hover:text-primary disabled:cursor-not-allowed disabled:opacity-30"
                  aria-label={previousSemesterScheduleYear === null ? 'No earlier semester year' : `View semester schedule for ${previousSemesterScheduleYear}`}
                  title={previousSemesterScheduleYear === null ? 'No earlier semester year' : `View ${previousSemesterScheduleYear}`}
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
                <span className="text-sm font-bold text-slate-700">{semesterScheduleYear}</span>
                <button
                  type="button"
                  disabled={nextSemesterScheduleYear === null}
                  onClick={() => nextSemesterScheduleYear !== null && setSemesterScheduleYear(nextSemesterScheduleYear)}
                  className="rounded-md p-1 text-slate-500 hover:bg-white hover:text-primary disabled:cursor-not-allowed disabled:opacity-30"
                  aria-label={nextSemesterScheduleYear === null ? 'No later semester year' : `View semester schedule for ${nextSemesterScheduleYear}`}
                  title={nextSemesterScheduleYear === null ? 'No later semester year' : `View ${nextSemesterScheduleYear}`}
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
              </div>
              <div className="mt-2 h-72 overflow-y-auto pr-1 [scrollbar-gutter:stable]">
                <div key={semesterScheduleYear} className="space-y-2 motion-safe:animate-fade-in">
                  {plannedSemestersForScheduleYear.length === 0 ? (
                    <p className="flex h-72 items-center justify-center rounded-lg bg-slate-50 px-2.5 text-center text-xs text-slate-400">No planned semester in {semesterScheduleYear}</p>
                  ) : plannedSemestersForScheduleYear.map(item => {
                    const timing = semesterTiming(item);
                    const canActivate = !currentSemester && timing === 'In progress';
                    const canTransition = Boolean(currentSemester)
                      && semesterTiming(currentSemester!) === 'Ended'
                      && timing === 'In progress';
                    return (
                      <div key={item.id} className="rounded-lg border border-slate-100 bg-slate-50 px-2.5 py-2 text-xs">
                        <div className="flex items-start justify-between gap-2">
                          <div className="min-w-0">
                            <p className="font-semibold text-slate-800">{semesterLabel(item)}</p>
                            <p className="mt-0.5 text-[11px] text-slate-500">{formatSemesterDate(item.startDate)} – {formatSemesterDate(item.endDate)}</p>
                            <p className={`mt-0.5 text-[11px] font-semibold ${timing === 'Ended' ? 'text-red-600' : timing === 'In progress' ? 'text-green-700' : 'text-blue-600'}`}>{timing}</p>
                          </div>
                          <button type="button" onClick={() => openEditSemesterDates(item)} className="rounded p-1 text-slate-400 hover:bg-white hover:text-primary" title="Edit dates"><Edit3 className="h-3.5 w-3.5" /></button>
                        </div>
                        <button
                          type="button"
                          disabled={(!canActivate && !canTransition) || savingSemester}
                          onClick={() => {
                            if (canTransition && currentSemester) {
                              setSemesterTransitionTarget({ current: currentSemester, target: item });
                              setSemesterTransitionReason('');
                              return;
                            }
                            void activateSemester(item);
                          }}
                          className="mt-2 w-full rounded-lg border border-primary-200 bg-white px-2 py-1.5 font-semibold text-primary disabled:cursor-not-allowed disabled:border-slate-200 disabled:text-slate-400"
                          title={timing !== 'In progress'
                            ? 'Activation is available only between the configured dates'
                            : currentSemester && semesterTiming(currentSemester) !== 'Ended'
                              ? 'The active semester must reach its configured end date before transition'
                              : canTransition
                                ? `Activate ${semesterLabel(item)} and move ${semesterLabel(currentSemester!)} to Closing`
                                : 'Activate semester'}
                        >
                          {canTransition ? 'Transition & activate' : 'Activate'}
                        </button>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>
            {closingSemesters.length > 0 && (
              <div className="mt-4 border-t border-slate-100 pt-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-amber-600">Closing semesters</p>
                <div className="mt-2 space-y-2">
                  {closingSemesters.map(item => (
                    <div key={item.id} className="rounded-lg border border-amber-200 bg-amber-50 px-2.5 py-2 text-xs">
                      <div className="flex items-center justify-between gap-2">
                        <div>
                          <p className="font-semibold text-amber-900">{semesterLabel(item)}</p>
                          <p className="mt-0.5 text-[11px] text-amber-700">Resolve remaining classes, then complete this semester.</p>
                        </div>
                        <button
                          type="button"
                          onClick={() => void openCompleteSemester(item)}
                          disabled={semesterLifecycleBusy}
                          className="shrink-0 rounded-md border border-amber-300 bg-white px-2 py-1 font-semibold text-amber-800 disabled:opacity-50"
                        >
                          Review blockers
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
            {semesters.some(item => item.status === 'Completed') && (
              <div className="mt-4 border-t border-slate-100 pt-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Completed history</p>
                <div className="mt-2 space-y-2">
                  {semesters.filter(item => item.status === 'Completed').slice(0, 4).map(item => (
                    <div key={item.id} className="flex items-center justify-between gap-2 rounded-lg bg-blue-50 px-2.5 py-2 text-xs">
                      <span className="min-w-0 font-semibold text-blue-800">
                        <span className="block">{semesterLabel(item)}</span>
                        {item.completionReason && <span className="block truncate font-normal text-blue-600" title={item.completionReason}>{item.completionReason}</span>}
                      </span>
                      <button
                        type="button"
                        disabled={Boolean(currentSemester)}
                        onClick={() => setSemesterLifecycleTarget({ semester: item, action: 'reopen' })}
                        className="font-semibold text-primary disabled:cursor-not-allowed disabled:opacity-40"
                        title={currentSemester ? 'Complete the active semester before reopening another one' : 'Reopen semester'}
                      >
                        Reopen
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}
            {canPlanNextYear && <p className="mt-3 rounded-xl border border-success-light bg-success-50 p-3 text-xs font-medium text-success-dark">Planning is available for the next academic year.</p>}
            <p className="mt-3 flex gap-2 rounded-xl border border-warning-light bg-warning-50 p-3 text-xs leading-5 text-warning-dark"><Sparkles className="mt-0.5 h-4 w-4 shrink-0" />Class creation uses the valid Active semester and the nearest upcoming Planned semester.</p>
          </aside>
        </div>
      ) : (
        <section className="space-y-5">
          <div className="flex flex-col gap-3 rounded-2xl border border-slate-200/70 bg-white p-4 shadow-sm md:flex-row md:items-end md:justify-between"><div className="flex flex-col gap-3 sm:flex-row"><label className="text-xs font-semibold uppercase text-slate-400">Semester<select value={selectedSemester} onChange={(event) => setSelectedSemester(event.target.value as SemesterCode)} className="mt-1.5 block rounded-xl border border-slate-200 px-3 py-2 text-sm font-normal text-slate-700 outline-none"><option value="SP">SP (Spring)</option><option value="SU">SU (Summer)</option><option value="FA">FA (Fall)</option></select></label><label className="text-xs font-semibold uppercase text-slate-400">Year<select value={selectedYear} onChange={(event) => setSelectedYear(Number(event.target.value))} className="mt-1.5 block rounded-xl border border-slate-200 px-3 py-2 text-sm font-normal text-slate-700 outline-none">{availableYears.map((year) => <option key={year} value={year}>{year}</option>)}</select></label></div><Button variant="outline" icon={Users} onClick={() => navigate('/admin/classes')}>Manage Assignments</Button></div>
          <MentorAdministrationCard
            semesterId={selectedSemesterRecord?.id}
            semesterLabel={`${selectedSemester} ${selectedYear}`}
            onImportCommitted={() => {
              setStaffPage(1);
              return loadStaff(selectedSemester, selectedYear);
            }}
          />
          <section className="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm dark:border-white/15 dark:bg-[#111827] dark:shadow-[0_10px_30px_rgba(0,0,0,0.18)]">
            <header className="flex items-center gap-3 border-b border-slate-200/80 bg-slate-50/80 px-4 py-3 dark:border-white/10 dark:bg-white/[0.045]">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-secondary text-white shadow-sm ring-1 ring-black/10 dark:ring-blue-300/40"><Users className="h-4.5 w-4.5" /></span>
              <div><h2 className="text-sm font-bold text-slate-900">Staff overview</h2><p className="text-xs text-slate-500">Availability and assignment status for {selectedSemester} {selectedYear}</p></div>
            </header>
            <div className="grid grid-cols-2 gap-2 p-3 sm:grid-cols-3 xl:grid-cols-6">
              {staffStats.map(({ label, value, icon: Icon, style }) => (
                <div key={label} className="flex items-center gap-3 rounded-xl border border-slate-200/80 bg-slate-50/70 px-3 py-2.5 dark:border-white/10 dark:bg-white/[0.035]">
                  <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg shadow-sm ring-1 ring-inset ${style}`}><Icon className="h-4 w-4" /></span>
                  <div className="min-w-0"><p className="text-lg font-bold leading-5 text-slate-900">{value}</p><p className="truncate text-xs text-slate-500">{label}</p></div>
                </div>
              ))}
            </div>
          </section>

          <section className="rounded-2xl border border-slate-200/80 bg-white p-3 shadow-sm dark:border-white/15 dark:bg-[#111827] dark:shadow-[0_10px_30px_rgba(0,0,0,0.18)]">
            <header className="mb-3 flex items-center gap-3 border-b border-slate-100 pb-3 dark:border-white/10">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary text-white shadow-sm ring-1 ring-primary-200 dark:ring-primary/50"><Filter className="h-4 w-4" /></span>
              <div><h2 className="text-sm font-bold text-slate-900">Search and filters</h2><p className="text-xs text-slate-500">Find staff by identity, role, profile status or assignment.</p></div>
            </header>
            <div className="flex flex-col gap-2 xl:flex-row xl:items-center">
              <div className="relative min-w-0 flex-1">
                <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500 dark:text-slate-300" />
                <input value={staffSearch} onChange={(event) => setStaffSearch(event.target.value)} placeholder="Search name, email, class, subject or missing field..." className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-10 pr-4 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 dark:border-white/15 dark:bg-[#0f172a]" />
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <select aria-label="Filter teaching staff by role" value={staffRole} onChange={(event) => setStaffRole(event.target.value as typeof staffRole)} className="min-w-[135px] rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-primary">
                  <option value="ALL">All roles</option><option value="LECTURER">Lecturers</option><option value="MENTOR">All mentors</option><option value="ENTERPRISE_MENTOR">Industry mentors</option><option value="ACADEMIC_MENTOR">Lecturer mentors</option>
                </select>
                <select aria-label="Filter teaching staff by status" value={staffStatus} onChange={(event) => setStaffStatus(event.target.value as StaffStatusFilter)} className="min-w-[165px] rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-primary">
                  <option value="ALL">All statuses</option><option value="ACTIVE">Active</option><option value="INACTIVE">Inactive</option><option value="NEEDS_INFORMATION">Needs information</option>
                </select>
                <select aria-label="Rows per page" value={staffPageSize} onChange={(event) => setStaffPageSize(Number(event.target.value) as (typeof staffPageSizes)[number])} className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-primary">
                  {staffPageSizes.map(size => <option key={size} value={size}>{size} / page</option>)}
                </select>
                {hasStaffFilters && <button type="button" onClick={() => { setStaffSearch(''); setStaffRole('ALL'); setStaffStatus('ALL'); }} className="px-2 py-2 text-sm font-semibold text-primary hover:text-primary-dark">Reset</button>}
              </div>
            </div>
          </section>

          <section className="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm dark:border-white/15 dark:bg-[#111827] dark:shadow-[0_10px_30px_rgba(0,0,0,0.18)]">
            <header className="flex flex-col gap-3 border-b border-slate-200/80 bg-slate-50/80 px-4 py-3 dark:border-white/10 dark:bg-white/[0.045] sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-3">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-secondary text-white shadow-sm ring-1 ring-black/10 dark:ring-blue-300/40"><Users className="h-4.5 w-4.5" /></span>
                <div>
                  <div className="flex items-center gap-2"><h2 className="font-bold text-slate-900">Teaching staff directory</h2><span className="rounded-full border border-slate-200 bg-white px-2 py-0.5 text-xs font-semibold text-slate-700 shadow-sm dark:border-white/15 dark:bg-[#0f172a]">{visibleStaff.length}</span></div>
                  <p className="mt-0.5 text-xs text-slate-500">Lecturers and mentors for {selectedSemester} {selectedYear}</p>
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="outline" icon={Plus} onClick={() => void openAddStaff('LECTURER')}>Add lecturer</Button>
                <Button size="sm" variant="outline" icon={Plus} onClick={() => void openAddStaff('MENTOR')}>Add mentor</Button>
              </div>
            </header>

            <div className="flex flex-wrap items-center gap-2 border-b border-slate-100 px-4 py-2 dark:border-white/10" aria-label="Staff type legend">
              {(['LECTURER', 'ENTERPRISE_MENTOR', 'ACADEMIC_MENTOR'] as const).map((kind) => {
                const style = STAFF_KIND_STYLES[kind];
                const LegendIcon = style.Icon;
                const count = visibleStaff.filter(member => getStaffKind(member) === kind).length;
                return (
                  <span key={kind} className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-semibold ${style.badge}`}>
                    <LegendIcon className="h-3 w-3" aria-hidden="true" />{style.label}<span className="opacity-70">· {count}</span>
                  </span>
                );
              })}
            </div>

            {staffLoading ? (
              <LoadingSkeleton variant="table" lines={6} className="p-4" />
            ) : paginatedStaff.length === 0 ? (
              <div className="px-5 py-10 text-center">
                <p className="text-sm font-semibold text-slate-700">No teaching staff found</p>
                <p className="mt-1 text-sm text-slate-500">{hasStaffFilters ? 'Try another search or reset the current filters.' : `Add lecturers or mentors to ${selectedSemester} ${selectedYear}.`}</p>
                {hasStaffFilters && <button type="button" onClick={() => { setStaffSearch(''); setStaffRole('ALL'); setStaffStatus('ALL'); }} className="mt-3 text-sm font-semibold text-primary hover:underline">Clear filters</button>}
              </div>
            ) : (
              <div className="divide-y divide-slate-100">
                {paginatedStaff.map((member) => {
                  const kindStyle = STAFF_KIND_STYLES[getStaffKind(member)];
                  const KindIcon = kindStyle.Icon;
                  return (
                  <article key={member._id} className={`grid gap-3 border-l-4 px-4 py-3 transition-colors hover:bg-slate-50/60 dark:hover:bg-white/[0.035] lg:grid-cols-[minmax(230px,1.25fr)_minmax(190px,auto)_minmax(230px,1fr)_36px] lg:items-center ${kindStyle.accent}`}>
                    <div className="flex min-w-0 items-center gap-3">
                      {member.avatar ? <img src={member.avatar} alt="" className="h-9 w-9 rounded-full object-cover" /> : <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold ${kindStyle.avatar}`}>{initials(member.name)}</span>}
                      <div className="min-w-0">
                        <p className="truncate text-sm font-semibold text-slate-900">{member.name}</p>
                        <p className={`truncate text-xs ${member.isIncomplete ? 'font-medium text-amber-600' : 'text-slate-500'}`}>{member.email || 'Email not provided'}</p>
                        {member.isIncomplete && member.missingFields.length > 0 && <p title={member.missingFields.join(', ')} className="mt-0.5 truncate text-[11px] text-slate-400">Missing: {member.missingFields.join(', ')}</p>}
                        {!member.isIncomplete && member.userStatus !== 'Active' && <p className="mt-0.5 text-[11px] font-medium text-red-600">User account: {member.userStatus}</p>}
                      </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span title={kindStyle.hint} className={`inline-flex items-center gap-1 rounded-full border px-2 py-1 text-[11px] font-semibold ${kindStyle.badge}`}><KindIcon className="h-3 w-3" aria-hidden="true" />{kindStyle.label}</span>
                      {member.isIncomplete ? <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-1 text-[11px] font-semibold text-amber-700">Needs information</span> : <Badge variant={member.status === 'Active' ? 'Active' : 'Inactive'}>{member.status}</Badge>}
                    </div>
                    <div className="flex min-w-0 flex-wrap items-center gap-1.5">
                      {member.isIncomplete ? <span className="text-xs font-medium text-amber-700">Complete profile before assignment</span> : member.assignments.length ? <>{member.assignments.slice(0, 2).map(assignment => <span key={assignment._id} className="rounded-full border border-primary-100 bg-primary-50 px-2 py-1 text-[11px] font-semibold text-primary">{assignment.classCode} · {assignment.subjectCode}</span>)}{member.assignments.length > 2 && <span className="text-xs font-semibold text-slate-500">+{member.assignments.length - 2} more</span>}</> : <span className="text-xs text-slate-500">Not assigned · {member.classCount} classes</span>}
                    </div>
                    <div className="flex justify-end">
                      {!member.isIncomplete && <button type="button" onClick={() => openEditStaff(member)} className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-primary-50 hover:text-primary" aria-label={`Edit ${member.name} semester status`} title="Edit semester status"><Edit3 className="h-4 w-4" /></button>}
                    </div>
                  </article>
                  );
                })}
              </div>
            )}

            {!staffLoading && visibleStaff.length > 0 && (
              <footer className="flex flex-col gap-2 border-t border-slate-100 bg-slate-50/60 px-4 py-3 text-xs text-slate-500 sm:flex-row sm:items-center sm:justify-between">
                <span>Showing <strong className="text-slate-700">{staffRangeStart}–{staffRangeEnd}</strong> of <strong className="text-slate-700">{visibleStaff.length}</strong></span>
                <div className="flex items-center gap-2">
                  <button type="button" disabled={currentStaffPage <= 1} onClick={() => setStaffPage(page => Math.max(1, page - 1))} className="inline-flex h-8 items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 font-semibold text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"><ChevronLeft className="h-3.5 w-3.5" /> Previous</button>
                  <span className="min-w-[76px] text-center font-medium text-slate-600">Page {currentStaffPage} / {staffTotalPages}</span>
                  <button type="button" disabled={currentStaffPage >= staffTotalPages} onClick={() => setStaffPage(page => Math.min(staffTotalPages, page + 1))} className="inline-flex h-8 items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 font-semibold text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40">Next <ChevronRight className="h-3.5 w-3.5" /></button>
                </div>
              </footer>
            )}
          </section>
        </section>
      )}

      <Modal
        isOpen={staffModalOpen}
        onClose={() => setStaffModalOpen(false)}
        title={editingStaff ? `Edit ${editingStaff.name}` : 'Edit semester status'}
        submitText={staffSaving ? 'Saving...' : 'Update Status'}
        isSubmitting={staffSaving}
        onSubmit={saveTeachingStaff}
      >
        {editingStaff && (
          <div className="space-y-4">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
              <p className="font-semibold text-slate-900">{editingStaff.name}</p>
              <p className="text-sm text-slate-500">{editingStaff.email}</p>
              <p className="mt-1 text-xs font-semibold text-primary">
                {editingStaff.role === 'LECTURER' ? 'Lecturer' : 'Mentor'} · {selectedSemester} {selectedYear}
              </p>
            </div>
            <label className="block text-sm font-medium text-slate-700">
              Semester status
              <select
                value={staffEntryStatus}
                onChange={(event) => setStaffEntryStatus(event.target.value as 'Active' | 'Inactive')}
                className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary"
              >
                <option value="Active">Active — available for new assignments</option>
                <option value="Inactive">Inactive — hidden from assignment lists</option>
              </select>
            </label>
            {editingStaff.classCount > 0 && staffEntryStatus === 'Inactive' && (
              <p className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs leading-5 text-amber-700">
                This member still has {editingStaff.classCount} class assignment(s). Reassign or end them before deactivation.
              </p>
            )}
            <p className="rounded-xl bg-slate-50 px-3 py-2 text-xs leading-5 text-slate-600">
              Only active staff in this semester list are available for new class or team assignments.
            </p>
          </div>
        )}
      </Modal>

      {addStaffRole && (
        <AddSemesterStaffModal
          isOpen
          role={addStaffRole}
          semester={selectedSemester}
          year={selectedYear}
          existingUserIds={existingStaffUserIds}
          onClose={() => setAddStaffRole(null)}
          onChanged={() => loadStaff()}
        />
      )}

      <Modal isOpen={modalOpen} onClose={() => setModalOpen(false)} title={editingSubject ? 'Edit Subject' : 'Add Subject'} submitText={savingSubject ? 'Saving...' : 'Save Subject'} isSubmitting={savingSubject} onSubmit={saveSubject}>
        <div className="space-y-4"><label className="block text-sm font-medium text-slate-700">Subject Code *<input disabled={Boolean(editingSubject)} value={form.subjectCode} onChange={(event) => setForm({ ...form, subjectCode: event.target.value })} placeholder="e.g. EXE301" className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 font-mono text-sm outline-none focus:border-primary disabled:bg-slate-50" /></label><label className="block text-sm font-medium text-slate-700">Subject Name *<input value={form.subjectName} onChange={(event) => setForm({ ...form, subjectName: event.target.value })} placeholder="e.g. Experiential Entrepreneurship 3" className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-primary" /></label><label className="block text-sm font-medium text-slate-700">Status<select value={form.status} onChange={(event) => setForm({ ...form, status: event.target.value as SubjectStatus })} className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary"><option value="active">Active</option><option value="disabled">Disabled</option></select></label></div>
      </Modal>
      <Modal
        isOpen={semesterScheduleOpen}
        onClose={() => setSemesterScheduleOpen(false)}
        title={editingSemesterSchedule ? `Edit ${semesterLabel(editingSemesterSchedule)} Dates` : 'Plan Semester'}
        submitText={savingSemester ? 'Saving...' : editingSemesterSchedule ? 'Update Dates' : 'Plan Semester'}
        isSubmitting={savingSemester}
        submitDisabled={!canSaveSemesterSchedule}
        onSubmit={saveSemesterSchedule}
        size="lg"
      >
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <label className="block text-sm font-medium text-slate-700">Semester *
              <select disabled={Boolean(editingSemesterSchedule)} value={semesterScheduleForm.semester} onChange={(event) => {
                const semester = event.target.value as SemesterCode;
                setSemesterScheduleServerError(null);
                setSemesterScheduleForm(current => {
                  if (semester !== 'FA' && current.startDate === `${current.year}-12-31`) {
                    return { ...current, semester, startDate: '', endDate: '' };
                  }
                  const endDate = semester !== 'FA' && current.endDate && !current.endDate.startsWith(`${current.year}-`)
                    ? ''
                    : current.endDate;
                  return { ...current, semester, endDate };
                });
              }} className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary disabled:bg-slate-50">
                <option value="SP">SP (Spring)</option><option value="SU">SU (Summer)</option><option value="FA">FA (Fall)</option>
              </select>
            </label>
            <label className="block text-sm font-medium text-slate-700">Year *
              <select disabled={Boolean(editingSemesterSchedule)} value={semesterScheduleForm.year} onChange={(event) => {
                setSemesterScheduleServerError(null);
                setSemesterScheduleForm(current => ({ ...current, year: Number(event.target.value), startDate: '', endDate: '' }));
              }} className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary disabled:bg-slate-50">
                {semesterPlanYears.map(year => <option key={year} value={year}>{year}</option>)}
              </select>
            </label>
          </div>
          {semesterScheduleDuplicateError && (
            <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-medium text-amber-700" aria-live="polite">
              {semesterScheduleDuplicateError}
            </p>
          )}
          <SemesterDateRangePicker
            semester={semesterScheduleForm.semester}
            year={semesterScheduleForm.year}
            startDate={semesterScheduleForm.startDate}
            endDate={semesterScheduleForm.endDate}
            error={semesterScheduleDateError}
            onChange={(startDate, endDate) => {
              setSemesterScheduleServerError(null);
              setSemesterScheduleForm(current => ({ ...current, startDate, endDate }));
            }}
          />
          {editingSemesterSchedule && (
            <label className="block text-sm font-medium text-slate-700">Reason for change *
              <textarea rows={3} maxLength={500} value={semesterScheduleForm.reason} onChange={(event) => {
                setSemesterScheduleServerError(null);
                setSemesterScheduleForm(current => ({ ...current, reason: event.target.value }));
              }} placeholder="Explain why the semester dates are being corrected" className="mt-1.5 w-full resize-none rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-primary" />
              {semesterScheduleForm.reason.length > 0 && semesterScheduleForm.reason.trim().length < 3 && (
                <span className="mt-1 block text-xs font-medium text-danger">Reason must contain at least 3 characters.</span>
              )}
            </label>
          )}
          {semesterScheduleGeneralError && (
            <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs font-medium text-red-700" aria-live="polite">
              {semesterScheduleGeneralError}
            </p>
          )}
          <p className="text-center text-xs leading-5 text-slate-500">
            Planning saves the schedule only; it does not activate the semester.
            {semesterScheduleForm.semester === 'FA' && ' Fall may end in January of the following year.'}
          </p>
        </div>
      </Modal>
      <ConfirmDialog isOpen={Boolean(disableTarget)} onClose={() => setDisableTarget(null)} onConfirm={disableSubject} title="Disable Subject" description="Existing class data will be kept, but this subject can no longer be used when creating new classes." confirmText="Disable Subject" isSubmitting={disabling} />
      <ConfirmDialog
        isOpen={Boolean(semesterLifecycleTarget)}
        onClose={() => { setSemesterLifecycleTarget(null); setSemesterLifecycleReason(''); }}
        onConfirm={confirmSemesterLifecycle}
        title={semesterLifecycleTarget?.action === 'reopen' ? 'Reopen this semester?' : 'Complete this semester?'}
        description={semesterLifecycleTarget?.action === 'reopen'
          ? 'The semester will become Active again. Completed classes remain read-only until an administrator reopens each class explicitly.'
          : semesterLifecycleTarget?.preview?.blockers?.length
            ? `Completion is blocked: ${semesterLifecycleTarget.preview.blockers.join(' ')}`
            : 'The semester will become Completed and no longer accept operational classes. This action is available only after every class is completed or archived.'}
        confirmText={semesterLifecycleTarget?.action === 'reopen' ? 'Reopen semester' : 'Complete semester'}
        confirmVariant="primary"
        isSubmitting={semesterLifecycleBusy}
        reason={semesterLifecycleReason}
        onReasonChange={setSemesterLifecycleReason}
        reasonRequired
        confirmDisabled={semesterLifecycleTarget?.action === 'complete' && (semesterLifecycleTarget.preview?.blockers?.length ?? 0) > 0}
        details={semesterLifecycleTarget?.action === 'complete' && (semesterLifecycleTarget.preview?.blockingClasses?.length ?? 0) > 0 ? (
          <div className="w-full space-y-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-left">
            <p className="text-xs font-semibold uppercase tracking-wide text-amber-800">Classes requiring action</p>
            {semesterLifecycleTarget?.preview?.blockingClasses.map(item => (
              <Link
                key={item.classId}
                to={`/classes/${item.slug || item.classId}`}
                className="flex items-center justify-between gap-3 rounded-lg border border-amber-100 bg-white px-3 py-2 text-sm transition hover:border-primary-200 hover:text-primary"
              >
                <span className="min-w-0">
                  <span className="block truncate font-semibold">{item.classCode}</span>
                  <span className="block text-xs text-slate-500">{item.status} · {item.activeEnrollmentCount} active enrollment(s)</span>
                </span>
                <span className="shrink-0 text-xs font-semibold">Open class →</span>
              </Link>
            ))}
            <Link
              to={`/admin/classes?semester=${semesterLifecycleTarget.semester.semester}&year=${semesterLifecycleTarget.semester.year}`}
              className="block text-center text-xs font-semibold text-primary hover:underline"
            >
              View all classes in {semesterLabel(semesterLifecycleTarget.semester)}
            </Link>
          </div>
        ) : undefined}
      />
      <ConfirmDialog
        isOpen={Boolean(semesterTransitionTarget)}
        onClose={() => {
          if (!savingSemester) {
            setSemesterTransitionTarget(null);
            setSemesterTransitionReason('');
          }
        }}
        onConfirm={confirmSemesterTransition}
        title={semesterTransitionTarget ? `Start ${semesterLabel(semesterTransitionTarget.target)}?` : 'Transition semesters?'}
        description={semesterTransitionTarget
          ? `${semesterLabel(semesterTransitionTarget.target)} will become Active. ${semesterLabel(semesterTransitionTarget.current)} will move to Closing so its remaining classes can be resolved without blocking the new semester.`
          : ''}
        confirmText="Transition semesters"
        confirmVariant="primary"
        isSubmitting={savingSemester}
        reason={semesterTransitionReason}
        onReasonChange={setSemesterTransitionReason}
        reasonLabel="Reason for transition"
        reasonRequired
      />
    </div>
  );
};

export default SubjectManagement;
