import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  BookOpen, Calendar, CheckCircle2, Edit3, Filter, GraduationCap, Plus,
  LockKeyhole, RefreshCw, Search, ShieldAlert, Sparkles, Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { subjectApi } from '../../api/subjectApi';
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
  const [selectedSemester, setSelectedSemester] = useState<SemesterCode>('SP');
  const [selectedYear, setSelectedYear] = useState(currentYear);
  const [availableYears, setAvailableYears] = useState<number[]>([currentYear]);
  const [canPlanNextYear, setCanPlanNextYear] = useState(false);
  const [savingSemester, setSavingSemester] = useState(false);
  const [staff, setStaff] = useState<TeachingStaffDto[]>([]);
  const [staffSummary, setStaffSummary] = useState<TeachingStaffSummary>(emptySummary);
  const [staffLoading, setStaffLoading] = useState(false);
  const [staffSearch, setStaffSearch] = useState('');
  const [staffRole, setStaffRole] = useState<'ALL' | TeachingStaffDto['role']>('ALL');
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

  const loadSemester = async () => {
    try {
      const [currentResponse, listResponse] = await Promise.all([
        subjectApi.getCurrentSemester(),
        subjectApi.getSemesters(),
      ]);
      const payload = responseData(currentResponse);
      const listPayload = responseData(listResponse);
      const semester = payload.currentSemester as SemesterDto | undefined;
      const years = Array.isArray(payload.availableYears) && payload.availableYears.length
        ? payload.availableYears.map(Number)
        : [currentYear];
      if (semester) {
        setCurrentSemester(semester);
        setSelectedSemester(semester.semester);
        setSelectedYear(Number(semester.year));
      }
      else {
        setCurrentSemester(null);
      }
      setSemesters(listPayload.semesters ?? []);
      setAvailableYears(years);
      setCanPlanNextYear(Boolean(payload.isDecember || payload.canPlanNextYear));
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to load the active semester').message);
    }
  };

  const loadStaff = async () => {
    setStaffLoading(true);
    try {
      const payload = responseData(await subjectApi.getTeachingStaff({ semester: selectedSemester, year: selectedYear }));
      setStaff(payload.staff ?? payload.teachingStaff ?? []);
      setStaffSummary({ ...emptySummary, ...(payload.summary ?? {}) });
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to load teaching staff').message);
      setStaff([]);
      setStaffSummary(emptySummary);
    } finally {
      setStaffLoading(false);
    }
  };

  useEffect(() => { void loadSemester(); }, []);
  useEffect(() => { void loadSubjects(); }, [debouncedSearch, statusFilter]);
  useEffect(() => {
    if (activeTab === 'staff') void loadStaff();
  }, [activeTab, selectedSemester, selectedYear]);

  const refresh = async () => {
    await loadSemester();
    if (activeTab === 'subjects') await loadSubjects();
    else await loadStaff();
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

  const saveSemester = async () => {
    setSavingSemester(true);
    try {
      const payload = responseData(await subjectApi.updateCurrentSemester(selectedSemester, selectedYear));
      const nextSemester = payload.currentSemester;
      setCurrentSemester(nextSemester);
      toast.success(`Active semester set to ${semesterLabel(nextSemester)}`);
      await loadSemester();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to update active semester').message);
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

  const activeSemesterBlocksActivation = Boolean(currentSemester);

  const staffStats = [
    { label: 'Lecturers', value: staffSummary.lecturers, icon: GraduationCap, style: 'text-primary bg-primary-50' },
    { label: 'Mentors', value: staffSummary.mentors, icon: Users, style: 'text-secondary bg-secondary-50' },
    { label: 'Assigned', value: staffSummary.assigned, icon: CheckCircle2, style: 'text-success bg-success-50' },
    { label: 'Unassigned', value: staffSummary.unassigned, icon: ShieldAlert, style: 'text-warning-dark bg-warning-50' },
    { label: 'Classes', value: staffSummary.classes, icon: BookOpen, style: 'text-cyan-700 bg-cyan-50' },
  ];

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
        <button type="button" onClick={() => setActiveTab('staff')} className={`inline-flex shrink-0 items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition ${activeTab === 'staff' ? 'bg-white text-primary shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}>
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
            <div className="flex items-center gap-2 border-b border-slate-100 pb-3"><Calendar className="h-5 w-5 text-primary" /><h2 className="font-bold text-slate-800">Active Semester</h2></div>
            <div className="mt-4 rounded-xl border border-slate-100 bg-slate-50 p-3">
              <p className="text-xs font-medium text-slate-400">Current Setting</p>
              <p className="mt-1 text-lg font-bold text-slate-900">{currentSemester ? semesterLabel(currentSemester) : 'No active semester'}</p>
              {currentSemester && <p className="mt-1 text-xs font-medium text-green-700">Active</p>}
            </div>
            <div className="mt-4 space-y-3"><label className="block text-xs font-semibold uppercase text-slate-400">Semester<select value={selectedSemester} onChange={(event) => setSelectedSemester(event.target.value as SemesterCode)} disabled={activeSemesterBlocksActivation} className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-normal text-slate-700 outline-none focus:border-primary disabled:cursor-not-allowed disabled:bg-slate-100"><option value="SP">SP (Spring)</option><option value="SU">SU (Summer)</option><option value="FA">FA (Fall)</option></select></label><label className="block text-xs font-semibold uppercase text-slate-400">Year<select value={selectedYear} onChange={(event) => setSelectedYear(Number(event.target.value))} disabled={activeSemesterBlocksActivation} className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-normal text-slate-700 outline-none focus:border-primary disabled:cursor-not-allowed disabled:bg-slate-100">{availableYears.map((year) => <option key={year} value={year}>{year}</option>)}</select></label><Button className="w-full" onClick={() => void saveSemester()} isLoading={savingSemester} disabled={activeSemesterBlocksActivation}>{savingSemester ? 'Saving...' : activeSemesterBlocksActivation ? 'Semester is active' : 'Set Active Semester'}</Button></div>
            {activeSemesterBlocksActivation && (
              <p className="mt-2 text-xs leading-5 text-slate-500">Complete the active semester before activating another one.</p>
            )}
            {currentSemester && (
              <Button variant="outline" className="mt-2 w-full" onClick={() => void openCompleteSemester()} isLoading={semesterLifecycleBusy}>
                Complete Active Semester
              </Button>
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
            {canPlanNextYear && <p className="mt-3 rounded-xl border border-success-light bg-success-50 p-3 text-xs font-medium text-success-dark">December planning is available for next year.</p>}
            <p className="mt-3 flex gap-2 rounded-xl border border-warning-light bg-warning-50 p-3 text-xs leading-5 text-warning-dark"><Sparkles className="mt-0.5 h-4 w-4 shrink-0" />Changing the active semester limits new class creation to the selected semester.</p>
          </aside>
        </div>
      ) : (
        <section className="space-y-5">
          <div className="flex flex-col gap-3 rounded-2xl border border-slate-200/70 bg-white p-4 shadow-sm md:flex-row md:items-end md:justify-between"><div className="flex flex-col gap-3 sm:flex-row"><label className="text-xs font-semibold uppercase text-slate-400">Semester<select value={selectedSemester} onChange={(event) => setSelectedSemester(event.target.value as SemesterCode)} className="mt-1.5 block rounded-xl border border-slate-200 px-3 py-2 text-sm font-normal text-slate-700 outline-none"><option value="SP">SP (Spring)</option><option value="SU">SU (Summer)</option><option value="FA">FA (Fall)</option></select></label><label className="text-xs font-semibold uppercase text-slate-400">Year<select value={selectedYear} onChange={(event) => setSelectedYear(Number(event.target.value))} className="mt-1.5 block rounded-xl border border-slate-200 px-3 py-2 text-sm font-normal text-slate-700 outline-none">{availableYears.map((year) => <option key={year} value={year}>{year}</option>)}</select></label></div><Button variant="outline" icon={Users} onClick={() => navigate('/admin/classes')}>Manage Assignments</Button></div>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">{staffStats.map(({ label, value, icon: Icon, style }) => <div key={label} className="rounded-xl border border-slate-200/70 bg-white p-4 shadow-sm"><div className={`flex h-9 w-9 items-center justify-center rounded-lg ${style}`}><Icon className="h-4 w-4" /></div><p className="mt-3 text-2xl font-bold text-slate-900">{value}</p><p className="text-sm text-slate-500">{label}</p></div>)}</div>
          <div className="flex flex-col gap-3 sm:flex-row"><div className="relative flex-1"><Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" /><input value={staffSearch} onChange={(event) => setStaffSearch(event.target.value)} placeholder="Search name, email, class or subject code..." className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-10 pr-4 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" /></div><select value={staffRole} onChange={(event) => setStaffRole(event.target.value as typeof staffRole)} className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none"><option value="ALL">All roles</option><option value="LECTURER">Lecturers only</option><option value="MENTOR">Mentors only</option></select></div>
          <div className="overflow-hidden rounded-2xl border border-slate-200/70 bg-white shadow-sm">{staffLoading ? <LoadingSkeleton variant="table" lines={6} className="p-4" /> : visibleStaff.length === 0 ? <EmptyState icon={Users} title="No teaching staff found" description="Try another semester, role, or search term." /> : <div className="divide-y divide-slate-100">{visibleStaff.map((member) => <article key={member._id} className="flex flex-col gap-4 p-5 lg:flex-row lg:items-center"><div className="flex min-w-0 flex-1 items-center gap-3">{member.avatar ? <img src={member.avatar} alt="" className="h-10 w-10 rounded-full object-cover" /> : <span className="flex h-10 w-10 items-center justify-center rounded-full bg-slate-100 text-xs font-bold text-slate-600">{initials(member.name)}</span>}<div className="min-w-0"><p className="truncate font-semibold text-slate-900">{member.name}</p><p className="truncate text-sm text-slate-500">{member.email}</p></div></div><div className="flex items-center gap-2"><Badge variant={member.role === 'LECTURER' ? 'Submitted' : 'Reviewed'}>{member.role === 'LECTURER' ? 'Lecturer' : 'Mentor'}</Badge><Badge variant={member.status.toLowerCase() === 'active' ? 'Active' : 'Inactive'}>{member.status}</Badge><span className="text-sm font-medium text-slate-600">{member.classCount} classes</span></div><div className="flex flex-1 flex-wrap gap-1.5 lg:justify-end">{member.assignments.length ? member.assignments.map((assignment) => <span key={assignment._id} className="rounded-full border border-primary-100 bg-primary-50 px-2 py-1 text-xs font-semibold text-primary">{assignment.classCode} · {assignment.subjectCode}</span>) : <span className="rounded-full border border-danger-light bg-danger-50 px-2 py-1 text-xs font-semibold text-danger">Not assigned in {selectedSemester} {selectedYear}</span>}</div></article>)}</div>}</div>
        </section>
      )}

      <Modal isOpen={modalOpen} onClose={() => setModalOpen(false)} title={editingSubject ? 'Edit Subject' : 'Add Subject'} submitText={savingSubject ? 'Saving...' : 'Save Subject'} isSubmitting={savingSubject} onSubmit={saveSubject}>
        <div className="space-y-4"><label className="block text-sm font-medium text-slate-700">Subject Code *<input disabled={Boolean(editingSubject)} value={form.subjectCode} onChange={(event) => setForm({ ...form, subjectCode: event.target.value })} placeholder="e.g. EXE301" className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 font-mono text-sm outline-none focus:border-primary disabled:bg-slate-50" /></label><label className="block text-sm font-medium text-slate-700">Subject Name *<input value={form.subjectName} onChange={(event) => setForm({ ...form, subjectName: event.target.value })} placeholder="e.g. Experiential Entrepreneurship 3" className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-primary" /></label><label className="block text-sm font-medium text-slate-700">Status<select value={form.status} onChange={(event) => setForm({ ...form, status: event.target.value as SubjectStatus })} className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary"><option value="active">Active</option><option value="disabled">Disabled</option></select></label></div>
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
