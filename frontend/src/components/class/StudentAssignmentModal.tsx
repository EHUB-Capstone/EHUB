import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  AlertCircle,
  ArrowRight,
  Check,
  GraduationCap,
  Loader2,
  Search,
  UserRoundCheck,
  Users,
  X,
} from 'lucide-react';
import Button from '../ui/Button';
import type {
  AssignableStudent,
  StudentAssignmentDraft,
  StudentAssignmentMode,
  StudentAssignmentResult,
} from '../../types/studentAssignment';
import type { ManagedTeam, TeamClassOption } from '../../types/teamManagement';
import {
  applyStudentAssignment,
  studentBelongsToClass,
  validateStudentAssignment,
} from '../../utils/studentAssignment';
import { parseApiError } from '../../utils/apiError';
import {
  buildStudentTeamAssignments,
  entityId,
  getTeamMemberIds,
  TEAM_MEMBER_LIMIT,
} from '../../utils/teamManagement';

interface StudentAssignmentModalProps {
  classInfo: TeamClassOption;
  students: AssignableStudent[];
  teams: ManagedTeam[];
  initialMode?: StudentAssignmentMode;
  initialStudentIds?: string[];
  loadingCandidates?: boolean;
  onClose: () => void;
  onSave: (result: StudentAssignmentResult) => void | Promise<void>;
}

const MODES: Array<{
  value: StudentAssignmentMode;
  title: string;
  description: string;
}> = [
  {
    value: 'CLASS',
    title: 'Assign to class',
    description: 'Add selected students to this class roster.',
  },
  {
    value: 'TEAM',
    title: 'Assign to team',
    description: 'Place class students into an existing team.',
  },
];

export default function StudentAssignmentModal({
  classInfo,
  students,
  teams,
  initialMode = 'CLASS',
  initialStudentIds = [],
  loadingCandidates = false,
  onClose,
  onSave,
}: StudentAssignmentModalProps) {
  const knownStudentIds = new Set(students.map((student) => student._id));
  const [draft, setDraft] = useState<StudentAssignmentDraft>({
    mode: initialMode,
    classId: classInfo.id,
    teamId: '',
    studentIds: [...new Set(initialStudentIds.filter((studentId) => knownStudentIds.has(studentId)))],
  });
  const [search, setSearch] = useState('');
  const [attemptedSubmit, setAttemptedSubmit] = useState(false);
  const [saving, setSaving] = useState(false);

  const classTeams = useMemo(
    () => teams.filter((team) => !entityId(team.classId) || entityId(team.classId) === classInfo.id),
    [classInfo.id, teams],
  );
  const assignments = useMemo(
    () => buildStudentTeamAssignments(classTeams, students),
    [classTeams, students],
  );
  const validation = useMemo(
    () => validateStudentAssignment(draft, students, classTeams),
    [classTeams, draft, students],
  );
  const targetTeam = classTeams.find((team) => team._id === draft.teamId);
  const targetMemberCount = targetTeam ? getTeamMemberIds(targetTeam).length : 0;
  const availableTeamSlots = Math.max(0, TEAM_MEMBER_LIMIT - targetMemberCount);

  const visibleStudents = useMemo(() => {
    const query = search.trim().toLowerCase();
    return students
      .filter((student) => !query || [student.fullName, student.rollNumber, student.email, student.major]
        .some((value) => value?.toLowerCase().includes(query)))
      .sort((left, right) => {
        const leftSelected = draft.studentIds.includes(left._id) ? 1 : 0;
        const rightSelected = draft.studentIds.includes(right._id) ? 1 : 0;
        if (leftSelected !== rightSelected) return rightSelected - leftSelected;
        const leftInClass = studentBelongsToClass(left, classInfo.id) ? 1 : 0;
        const rightInClass = studentBelongsToClass(right, classInfo.id) ? 1 : 0;
        if (leftInClass !== rightInClass) return rightInClass - leftInClass;
        return left.fullName.localeCompare(right.fullName);
      });
  }, [classInfo.id, draft.studentIds, search, students]);

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleEscape);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener('keydown', handleEscape);
    };
  }, [onClose]);

  const changeMode = (mode: StudentAssignmentMode) => {
    setAttemptedSubmit(false);
    setDraft((current) => ({ ...current, mode, teamId: mode === 'TEAM' ? current.teamId : '' }));
  };

  const toggleStudent = (student: AssignableStudent) => {
    const belongsToClass = studentBelongsToClass(student, classInfo.id);
    const assignment = assignments.get(student._id);
    const alreadyAssigned = draft.mode === 'CLASS'
      ? belongsToClass
      : Boolean(draft.teamId && assignment?.teamId === draft.teamId);

    if (alreadyAssigned) {
      toast(`${student.fullName} is already in the selected ${draft.mode === 'CLASS' ? 'class' : 'team'}.`);
      return;
    }

    setDraft((current) => ({
      ...current,
      studentIds: current.studentIds.includes(student._id)
        ? current.studentIds.filter((studentId) => studentId !== student._id)
        : [...current.studentIds, student._id],
    }));
  };

  const handleSubmit = async () => {
    setAttemptedSubmit(true);
    if (!validation.isValid) {
      if (validation.studentsOutsideClass.length > 0) {
        toast.error('Team assignment blocked: selected students must belong to this class.');
      } else {
        toast.error('Please correct the highlighted assignment information.');
      }
      return;
    }

    setSaving(true);
    try {
      const result = applyStudentAssignment(draft, students, classTeams);
      await onSave(result);
      toast.success(
        draft.mode === 'CLASS'
          ? `${draft.studentIds.length} student${draft.studentIds.length === 1 ? '' : 's'} assigned to ${classInfo.code}`
          : `${draft.studentIds.length} student${draft.studentIds.length === 1 ? '' : 's'} assigned to ${targetTeam?.teamName || 'team'}`,
      );
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to update the student assignment.').message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[76] flex items-end justify-center p-0 sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby="student-assignment-title">
      <button type="button" className="absolute inset-0 cursor-default bg-slate-900/45 backdrop-blur-sm" onClick={onClose} aria-label="Close student assignment dialog" />
      <div className="relative flex max-h-[95vh] w-full max-w-5xl flex-col overflow-hidden rounded-t-2xl border border-slate-200/60 bg-white shadow-float animate-scale-in sm:max-h-[92vh] sm:rounded-2xl">
        <header className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-100 px-5 py-4 sm:px-6">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary-50 text-primary">
              <UserRoundCheck className="h-5 w-5" />
            </div>
            <div>
              <h2 id="student-assignment-title" className="text-lg font-bold text-slate-900">Assign students</h2>
              <p className="text-sm text-slate-500">Keep class and team placement consistent</p>
            </div>
          </div>
          <button type="button" onClick={onClose} className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700" aria-label="Close">
            <X className="h-5 w-5" />
          </button>
        </header>

        <div className="flex-1 overflow-y-auto px-5 py-5 sm:px-6">
          <div className="grid gap-6 lg:grid-cols-[minmax(0,0.75fr)_minmax(0,1.25fr)]">
            <div className="space-y-4">
              <section className="space-y-3 rounded-2xl border border-slate-200 p-4">
                <div className="flex items-center gap-2">
                  <GraduationCap className="h-4 w-4 text-primary" />
                  <h3 className="text-sm font-bold text-slate-900">Assignment type</h3>
                </div>
                {MODES.map((mode) => (
                  <button
                    key={mode.value}
                    type="button"
                    onClick={() => changeMode(mode.value)}
                    className={`flex w-full items-start gap-3 rounded-xl border p-3 text-left transition-all ${draft.mode === mode.value ? 'border-primary bg-primary-50' : 'border-slate-200 hover:border-primary/40 hover:bg-slate-50'}`}
                  >
                    <span className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full border ${draft.mode === mode.value ? 'border-primary bg-primary text-white' : 'border-slate-300 bg-white text-transparent'}`}>
                      <Check className="h-3 w-3" />
                    </span>
                    <span>
                      <span className="block text-sm font-semibold text-slate-800">{mode.title}</span>
                      <span className="mt-0.5 block text-xs text-slate-500">{mode.description}</span>
                    </span>
                  </button>
                ))}
              </section>

              <section className="space-y-4 rounded-2xl border border-slate-200 p-4">
                <div>
                  <label className="mb-1.5 block text-xs font-semibold text-slate-600">Selected class <span className="text-red-500">*</span></label>
                  <div className="flex items-center gap-3 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5">
                    <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-secondary-100 text-secondary"><GraduationCap className="h-4 w-4" /></span>
                    <span>
                      <span className="block text-sm font-semibold text-slate-800">{classInfo.code}</span>
                      {classInfo.name && <span className="block text-xs text-slate-500">{classInfo.name}</span>}
                    </span>
                  </div>
                </div>

                {draft.mode === 'TEAM' && (
                  <div>
                    <label htmlFor="assignment-team" className="mb-1.5 block text-xs font-semibold text-slate-600">Team <span className="text-red-500">*</span></label>
                    <select
                      id="assignment-team"
                      value={draft.teamId}
                      onChange={(event) => setDraft((current) => ({ ...current, teamId: event.target.value }))}
                      className={`w-full rounded-xl border bg-white px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 ${attemptedSubmit && validation.errors.teamId ? 'border-red-300 bg-red-50' : 'border-slate-200'}`}
                    >
                      <option value="">Select a team</option>
                      {classTeams.map((team) => (
                        <option key={team._id} value={team._id}>{team.teamName} ({getTeamMemberIds(team).length}/{TEAM_MEMBER_LIMIT})</option>
                      ))}
                    </select>
                    {attemptedSubmit && validation.errors.teamId && <p className="mt-1 text-xs text-red-600">{validation.errors.teamId}</p>}
                    {targetTeam && <p className="mt-1.5 text-xs text-slate-500">{availableTeamSlots} place{availableTeamSlots === 1 ? '' : 's'} available in this team.</p>}
                  </div>
                )}
              </section>

              <section className="rounded-2xl border border-secondary-100 bg-secondary-50/50 p-4">
                <div className="flex items-center gap-2 text-sm font-bold text-slate-900">
                  <ArrowRight className="h-4 w-4 text-secondary" /> Assignment summary
                </div>
                <div className="mt-3 space-y-2 text-xs text-slate-600">
                  <p><strong>{draft.studentIds.length}</strong> student{draft.studentIds.length === 1 ? '' : 's'} selected</p>
                  <p>Destination: <strong>{draft.mode === 'TEAM' ? targetTeam?.teamName || 'Select a team' : classInfo.code}</strong></p>
                  <p>{draft.mode === 'TEAM' ? 'Only students already in this class can be assigned.' : 'Moving a student from another class clears their previous team.'}</p>
                </div>
              </section>
            </div>

            <section className="flex min-h-[520px] flex-col rounded-2xl border border-slate-200">
              <div className="border-b border-slate-100 p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <Users className="h-4 w-4 text-primary" />
                    <div>
                      <h3 className="text-sm font-bold text-slate-900">Students <span className="text-red-500">*</span></h3>
                      <p className="text-xs text-slate-500">Select one or more student records</p>
                    </div>
                  </div>
                  {loadingCandidates ? (
                    <span className="flex items-center gap-1.5 text-xs text-slate-400"><Loader2 className="h-3.5 w-3.5 animate-spin" /> Loading directory</span>
                  ) : (
                    <span className="rounded-full bg-primary-50 px-2.5 py-1 text-xs font-bold text-primary">{draft.studentIds.length} selected</span>
                  )}
                </div>
                <div className="relative mt-3">
                  <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                  <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search name, student code, email or major" className="w-full rounded-xl border border-slate-200 py-2.5 pl-9 pr-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
                </div>
                {attemptedSubmit && validation.errors.studentIds && (
                  <div className="mt-3 flex items-start gap-2 rounded-lg bg-red-50 p-2.5 text-xs text-red-700"><AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" /><span>{validation.errors.studentIds}</span></div>
                )}
              </div>

              <div className="flex-1 space-y-2 overflow-y-auto p-3">
                {visibleStudents.length === 0 ? (
                  <p className="py-12 text-center text-sm text-slate-400">No students match this search.</p>
                ) : visibleStudents.map((student) => {
                  const selected = draft.studentIds.includes(student._id);
                  const belongsToClass = studentBelongsToClass(student, classInfo.id);
                  const assignment = assignments.get(student._id);
                  const alreadyAtDestination = draft.mode === 'CLASS'
                    ? belongsToClass
                    : Boolean(draft.teamId && assignment?.teamId === draft.teamId);
                  const conflict = draft.mode === 'TEAM' && assignment && assignment.teamId !== draft.teamId;
                  const outsideClass = draft.mode === 'TEAM' && !belongsToClass;
                  const invalidAfterSubmit = attemptedSubmit && (
                    validation.studentsOutsideClass.includes(student._id)
                    || validation.teamConflicts.has(student._id)
                  );

                  return (
                    <button
                      key={student._id}
                      type="button"
                      onClick={() => toggleStudent(student)}
                      aria-pressed={selected}
                      className={`flex w-full items-center gap-3 rounded-xl border p-3 text-left transition-all ${invalidAfterSubmit ? 'border-red-300 bg-red-50' : selected ? 'border-primary bg-primary-50' : alreadyAtDestination ? 'border-slate-100 bg-slate-50 opacity-65' : 'border-slate-200 hover:border-primary/50 hover:bg-slate-50'}`}
                    >
                      <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-md border ${selected ? 'border-primary bg-primary text-white' : 'border-slate-300 bg-white text-transparent'}`}>
                        <Check className="h-3.5 w-3.5" />
                      </span>
                      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-secondary-300 to-secondary text-xs font-bold text-white">{student.fullName.charAt(0).toUpperCase()}</span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-semibold text-slate-800">{student.fullName}</span>
                        <span className="block truncate text-xs text-slate-500">{student.rollNumber || 'No student code'}{student.major ? ` · ${student.major}` : ''}</span>
                      </span>
                      <span className="flex max-w-40 shrink-0 flex-col items-end gap-1">
                        {outsideClass ? (
                          <span className="rounded-full bg-red-100 px-2 py-1 text-right text-[10px] font-semibold text-red-700">Not in {classInfo.code}</span>
                        ) : belongsToClass ? (
                          <span className="rounded-full bg-green-100 px-2 py-1 text-[10px] font-semibold text-green-700">In {classInfo.code}</span>
                        ) : (
                          <span className="rounded-full bg-slate-100 px-2 py-1 text-[10px] font-semibold text-slate-600">Unassigned</span>
                        )}
                        {conflict && <span className="max-w-40 truncate text-[10px] font-semibold text-amber-700">Team: {assignment.teamName}</span>}
                        {alreadyAtDestination && <span className="text-[10px] font-semibold text-slate-500">Already assigned</span>}
                      </span>
                    </button>
                  );
                })}
              </div>
            </section>
          </div>
        </div>

        <footer className="flex shrink-0 flex-col-reverse gap-2 border-t border-slate-100 bg-slate-50/70 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <p className="text-xs text-slate-500">Changes are applied consistently to the class roster and team member list.</p>
          <div className="flex gap-2">
            <Button variant="outline" onClick={onClose} disabled={saving}>Cancel</Button>
            <Button variant="gradient" icon={saving ? Loader2 : UserRoundCheck} onClick={handleSubmit} disabled={saving}>
              {saving ? 'Saving assignment...' : draft.mode === 'CLASS' ? 'Assign to class' : 'Assign to team'}
            </Button>
          </div>
        </footer>
      </div>
    </div>
  );
}
