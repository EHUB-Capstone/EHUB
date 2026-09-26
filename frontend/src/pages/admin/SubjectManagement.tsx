import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  BookOpen, Calendar, CheckCircle2, ChevronLeft, ChevronRight, Edit3, Filter, GraduationCap, Plus,
  LockKeyhole, RefreshCw, Search, ShieldAlert, Sparkles, Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { subjectApi } from '../../api/subjectApi';
import SemesterDateRangePicker from '../../components/admin/SemesterDateRangePicker';
import MentorAdministrationCard from '../../components/admin/MentorAdministrationCard';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import EmptyState from '../../components/ui/EmptyState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import Modal from '../../components/ui/Modal';
import { parseApiError } from '../../utils/apiError';
import type {
  SemesterCode,
  SemesterCompletionPreview,
  SemesterDto,
  SubjectDto,
  SubjectStatus,
  TeachingStaffDto,
  TeachingStaffCandidateDto,
  TeachingStaffSummary,
} from '../../types/subjects';

const emptySummary: TeachingStaffSummary = { lecturers: 0, mentors: 0, assigned: 0, unassigned: 0, classes: 0 };
const currentYear = new Date().getFullYear();

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
  const [activeTab, setActiveTab] = useState<'subjects' | 'staff'>('subjects');
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [subjectsLoading, setSubjectsLoading] = useState(true);
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
  const [staffRole, setStaffRole] = useState<'ALL' | TeachingStaffDto['role']>('ALL');
  const [staffModalOpen, setStaffModalOpen] = useState(false);
  const [editingStaff, setEditingStaff] = useState<TeachingStaffDto | null>(null);
  const [staffModalRole, setStaffModalRole] = useState<TeachingStaffDto['role']>('LECTURER');
  const [staffCandidates, setStaffCandidates] = useState<TeachingStaffCandidateDto[]>([]);
  const [staffCandidateKey, setStaffCandidateKey] = useState('');
  const [staffEntryStatus, setStaffEntryStatus] = useState<'Active' | 'Inactive'>('Active');
  const [staffSaving, setStaffSaving] = useState(false);
  const [staffCandidatesLoading, setStaffCandidatesLoading] = useState(false);
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
  const staffRequestId = useRef(0);

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(search), 500);
    return () => window.clearTimeout(timeout);
  }, [search]);

  const loadSubjects = async () => {
    setSubjectsLoading(true);
    try {
      const params: { search?: string; status?: SubjectStatus } = {};
      if (debouncedSearch.trim()) params.search = debouncedSearch.trim();
      if (statusFilter !== 'ALL') params.status = statusFilter;
      const payload = responseData(await subjectApi.getAll(params));
      setSubjects(payload.subjects ?? []);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to load subjects').message);
    } finally {
      setSubjectsLoading(false);
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

  const openAddStaff = async (role: TeachingStaffDto['role']) => {
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

    setEditingStaff(null);
    setStaffModalRole(role);
    setStaffCandidateKey('');
    setStaffEntryStatus('Active');
    setStaffModalOpen(true);
    setStaffCandidatesLoading(true);
    try {
      const payload = responseData(await subjectApi.getTeachingStaffCandidates());
      setStaffCandidates(payload.candidates ?? []);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to load eligible lecturers and mentors').message);
      setStaffCandidates([]);
    } finally {
      setStaffCandidatesLoading(false);
    }
  };

  const openEditStaff = (member: TeachingStaffDto) => {
    setEditingStaff(member);
    setStaffEntryStatus(member.status);
    setStaffModalOpen(true);
  };

  const saveTeachingStaff = async () => {
    setStaffSaving(true);
    try {
      if (editingStaff) {
        await subjectApi.updateTeachingStaff(editingStaff._id, {
          status: staffEntryStatus,
          rowVersion: editingStaff.rowVersion,
        });
        toast.success(`${editingStaff.name} updated for ${selectedSemester} ${selectedYear}.`);
      } else {
        const candidate = staffCandidates.find(item => `${item.userId}:${item.role}` === staffCandidateKey);
        if (!candidate) {
          toast.error('Select an eligible lecturer or mentor.');
          return;
        }
        await subjectApi.addTeachingStaff({
          semester: selectedSemester,
          year: selectedYear,
          userId: candidate.userId,
          role: candidate.role,
        });
        toast.success(`${candidate.name} added to ${selectedSemester} ${selectedYear}.`);
      }

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

  const openCompleteSemester = async () => {
    if (!currentSemester) return;
    setSemesterLifecycleBusy(true);
    try {
      const preview = responseData(await subjectApi.getSemesterCompletionPreview(currentSemester.id));
      setSemesterLifecycleTarget({ semester: currentSemester, action: 'complete', preview });
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to preview semester completion').message);
    } finally {
      setSemesterLifecycleBusy(false);
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
    const query = staffSearch.trim().toLowerCase();
    return staff.filter((member) => {
      const matchesRole = staffRole === 'ALL' || member.role === staffRole;
      const matchesSearch = !query || [member.name, member.email, ...member.assignments.flatMap((item) => [item.classCode, item.subjectCode])]
        .some((value) => value?.toLowerCase().includes(query));
      return matchesRole && matchesSearch;
    });
  }, [staff, staffRole, staffSearch]);

  const availableStaffCandidates = useMemo(() => {
    const existingKeys = new Set(staff.map(member => `${member.userId}:${member.role}`));
    return staffCandidates.filter(candidate =>
      candidate.role === staffModalRole && !existingKeys.has(`${candidate.userId}:${candidate.role}`));
  }, [staff, staffCandidates, staffModalRole]);

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
    { label: 'Lecturers', value: staffSummary.lecturers, icon: GraduationCap, style: 'text-primary bg-primary-50' },
    { label: 'Mentors', value: staffSummary.mentors, icon: Users, style: 'text-secondary bg-secondary-50' },
    { label: 'Assigned', value: staffSummary.assigned, icon: CheckCircle2, style: 'text-success bg-success-50' },
    { label: 'Unassigned', value: staffSummary.unassigned, icon: ShieldAlert, style: 'text-warning-dark bg-warning-50' },
    { label: 'Classes', value: staffSummary.classes, icon: BookOpen, style: 'text-cyan-700 bg-cyan-50' },
  ];

  const staffSections = [
    {
      role: 'LECTURER' as const,
      title: 'Lecturers',
      description: 'Teaching staff available for class assignment in this semester.',
      icon: GraduationCap,
      iconStyle: 'bg-primary-50 text-primary',
      members: visibleStaff.filter(member => member.role === 'LECTURER'),
    },
    {
      role: 'MENTOR' as const,
      title: 'Mentors',
      description: 'Mentors available for class and team assignment in this semester.',
      icon: Users,
      iconStyle: 'bg-secondary-50 text-secondary',
      members: visibleStaff.filter(member => member.role === 'MENTOR'),
    },
  ].filter(section => staffRole === 'ALL' || section.role === staffRole);
  const selectedSemesterRecord = semesters.find(item => item.semester === selectedSemester && Number(item.year) === selectedYear);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 sm:text-3xl">
            {activeTab === 'subjects' ? 'Subject & Semester' : 'Lecturers & Mentors'}
          </h1>
          <p className="mt-1 text-slate-500">
            {activeTab === 'subjects' ? 'Manage academic subjects and the active semester.' : 'Review teaching assignments for each semester.'}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" icon={RefreshCw} onClick={() => void refresh()}>Refresh</Button>
          {activeTab === 'subjects' && <Button icon={Plus} onClick={openAdd}>Add Subject</Button>}
        </div>
      </div>

      <div className="inline-flex w-full gap-1 overflow-x-auto rounded-xl bg-slate-100 p-1 sm:w-auto">
        <button type="button" onClick={() => setActiveTab('subjects')} className={`inline-flex shrink-0 items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition ${activeTab === 'subjects' ? 'bg-white text-primary shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}>
          <BookOpen className="h-4 w-4" /> Subject & Semester
        </button>
        <button type="button" onClick={openStaffTab} className={`inline-flex shrink-0 items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition ${activeTab === 'staff' ? 'bg-white text-primary shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}>
          <Users className="h-4 w-4" /> Lecturers & Mentors by Semester
        </button>
      </div>

      {activeTab === 'subjects' ? (
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
                <div className="overflow-x-auto"><table className="w-full min-w-[620px]"><thead><tr className="border-b border-slate-100 bg-slate-50"><th className="px-6 py-3 text-left text-xs font-semibold uppercase text-slate-400">Subject Code</th><th className="px-6 py-3 text-left text-xs font-semibold uppercase text-slate-400">Subject Name</th><th className="px-6 py-3 text-left text-xs font-semibold uppercase text-slate-400">Status</th><th className="px-6 py-3 text-right text-xs font-semibold uppercase text-slate-400">Actions</th></tr></thead>
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
              <Button variant="outline" className="mt-3 w-full" onClick={() => void openCompleteSemester()} isLoading={semesterLifecycleBusy}>
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
                          disabled={!canActivate || savingSemester}
                          onClick={() => void activateSemester(item)}
                          className="mt-2 w-full rounded-lg border border-primary-200 bg-white px-2 py-1.5 font-semibold text-primary disabled:cursor-not-allowed disabled:border-slate-200 disabled:text-slate-400"
                          title={currentSemester ? 'Complete the active semester first' : timing !== 'In progress' ? 'Activation is available only between the configured dates' : 'Activate semester'}
                        >
                          Activate
                        </button>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>
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
            onImportCommitted={() => loadStaff(selectedSemester, selectedYear)}
          />
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">{staffStats.map(({ label, value, icon: Icon, style }) => <div key={label} className="rounded-xl border border-slate-200/70 bg-white p-4 shadow-sm"><div className={`flex h-9 w-9 items-center justify-center rounded-lg ${style}`}><Icon className="h-4 w-4" /></div><p className="mt-3 text-2xl font-bold text-slate-900">{value}</p><p className="text-sm text-slate-500">{label}</p></div>)}</div>
          <div className="flex flex-col gap-3 sm:flex-row"><div className="relative flex-1"><Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" /><input value={staffSearch} onChange={(event) => setStaffSearch(event.target.value)} placeholder="Search name, email, class or subject code..." className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-10 pr-4 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" /></div><select value={staffRole} onChange={(event) => setStaffRole(event.target.value as typeof staffRole)} className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none"><option value="ALL">All roles</option><option value="LECTURER">Lecturers only</option><option value="MENTOR">Mentors only</option></select></div>
          {staffLoading ? (
            <div className="overflow-hidden rounded-2xl border border-slate-200/70 bg-white shadow-sm">
              <LoadingSkeleton variant="table" lines={6} className="p-4" />
            </div>
          ) : (
            <div className="space-y-5">
              {staffSections.map(({ role, title, description, icon: Icon, iconStyle, members }) => (
                <section key={role} className="overflow-hidden rounded-2xl border border-slate-200/70 bg-white shadow-sm">
                  <header className="flex flex-col gap-3 border-b border-slate-100 bg-slate-50/70 px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex items-center gap-3">
                      <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${iconStyle}`}>
                        <Icon className="h-5 w-5" />
                      </span>
                      <div>
                        <div className="flex items-center gap-2">
                          <h2 className="font-bold text-slate-900">{title}</h2>
                          <span className="rounded-full bg-white px-2.5 py-0.5 text-xs font-semibold text-slate-600 shadow-sm">
                            {members.length}
                          </span>
                        </div>
                        <p className="mt-0.5 text-sm text-slate-500">{description}</p>
                      </div>
                    </div>
                    <Button size="sm" variant="outline" icon={Plus} onClick={() => void openAddStaff(role)}>
                      Add {role === 'LECTURER' ? 'Lecturer' : 'Mentor'}
                    </Button>
                  </header>

                  {members.length === 0 ? (
                    <div className="px-5 py-8 text-center">
                      <p className="text-sm font-semibold text-slate-700">No {title.toLowerCase()} found</p>
                      <p className="mt-1 text-sm text-slate-500">
                        {staffSearch.trim()
                          ? 'Try another search term or clear the current filters.'
                          : `Add ${title.toLowerCase()} to ${selectedSemester} ${selectedYear}.`}
                      </p>
                    </div>
                  ) : (
                    <div className="divide-y divide-slate-100">
                      {members.map((member) => (
                        <article key={member._id} className="flex flex-col gap-4 p-5 lg:flex-row lg:items-center">
                          <div className="flex min-w-0 flex-1 items-center gap-3">
                            {member.avatar ? (
                              <img src={member.avatar} alt="" className="h-10 w-10 rounded-full object-cover" />
                            ) : (
                              <span className="flex h-10 w-10 items-center justify-center rounded-full bg-slate-100 text-xs font-bold text-slate-600">
                                {initials(member.name)}
                              </span>
                            )}
                            <div className="min-w-0">
                              <p className="truncate font-semibold text-slate-900">{member.name}</p>
                              <p className="truncate text-sm text-slate-500">{member.email}</p>
                              {member.userStatus !== 'Active' && (
                                <p className="mt-0.5 text-xs font-medium text-red-600">User account: {member.userStatus}</p>
                              )}
                            </div>
                          </div>
                          <div className="flex flex-wrap items-center gap-2">
                            <Badge variant={member.status === 'Active' ? 'Active' : 'Inactive'}>{member.status}</Badge>
                            <span className="text-sm font-medium text-slate-600">{member.classCount} classes</span>
                          </div>
                          <div className="flex flex-1 flex-wrap gap-1.5 lg:justify-end">
                            {member.assignments.length ? member.assignments.map((assignment) => (
                              <span key={assignment._id} className="rounded-full border border-primary-100 bg-primary-50 px-2 py-1 text-xs font-semibold text-primary">
                                {assignment.classCode} · {assignment.subjectCode}
                              </span>
                            )) : (
                              <span className="rounded-full border border-slate-200 bg-slate-50 px-2 py-1 text-xs font-semibold text-slate-500">
                                Not assigned in {selectedSemester} {selectedYear}
                              </span>
                            )}
                          </div>
                          <button
                            type="button"
                            onClick={() => openEditStaff(member)}
                            className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-primary-50 hover:text-primary"
                            aria-label={`Edit ${member.name} semester status`}
                            title="Edit semester status"
                          >
                            <Edit3 className="h-4 w-4" />
                          </button>
                        </article>
                      ))}
                    </div>
                  )}
                </section>
              ))}
            </div>
          )}
        </section>
      )}

      <Modal
        isOpen={staffModalOpen}
        onClose={() => setStaffModalOpen(false)}
        title={editingStaff ? `Edit ${editingStaff.name}` : `Add Existing ${staffModalRole === 'LECTURER' ? 'Lecturer' : 'Mentor'} to ${selectedSemester} ${selectedYear}`}
        submitText={staffSaving ? 'Saving...' : editingStaff ? 'Update Status' : 'Add to Semester'}
        isSubmitting={staffSaving}
        onSubmit={saveTeachingStaff}
      >
        <div className="space-y-4">
          {!editingStaff && (
            <p className="rounded-xl border border-blue-200 bg-blue-50 px-3 py-2 text-xs leading-5 text-blue-700">
              This does not create a new account. Select an existing active {staffModalRole === 'LECTURER' ? 'Lecturer' : 'Mentor'} to make them available for assignments in this semester.
            </p>
          )}
          {editingStaff ? (
            <>
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
            </>
          ) : staffCandidatesLoading ? (
            <LoadingSkeleton variant="text" lines={4} />
          ) : (
            <>
              <label className="block text-sm font-medium text-slate-700">
                Existing active {staffModalRole === 'LECTURER' ? 'lecturer' : 'mentor'} *
                <select
                  value={staffCandidateKey}
                  onChange={(event) => setStaffCandidateKey(event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary"
                >
                  <option value="">Select a staff member</option>
                  {availableStaffCandidates.map(candidate => (
                    <option key={`${candidate.userId}:${candidate.role}`} value={`${candidate.userId}:${candidate.role}`}>
                      {candidate.name} — {candidate.role === 'LECTURER' ? 'Lecturer' : 'Mentor'} ({candidate.email})
                    </option>
                  ))}
                </select>
              </label>
              {availableStaffCandidates.length === 0 && (
                <p className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
                  No additional active {staffModalRole === 'LECTURER' ? 'Lecturer' : 'Mentor'} accounts are available for this semester.
                </p>
              )}
            </>
          )}
          {!editingStaff && (
            <p className="text-xs leading-5 text-slate-500">
              Need to create a new staff account?{' '}
              <Link to="/admin/users" className="font-semibold text-primary hover:underline">
                Go to User Management
              </Link>{' '}
              first, then return here to add the account to this semester.
            </p>
          )}
          <p className="rounded-xl bg-slate-50 px-3 py-2 text-xs leading-5 text-slate-600">
            Only active staff in this semester list are available for new class or team assignments.
          </p>
        </div>
      </Modal>

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
      />
    </div>
  );
};

export default SubjectManagement;
