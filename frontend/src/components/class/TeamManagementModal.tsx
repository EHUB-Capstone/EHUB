import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  AlertCircle,
  BookOpen,
  Check,
  Crown,
  FolderKanban,
  Search,
  UserCheck,
  UserPlus,
  Users,
  X,
} from 'lucide-react';
import Button from '../ui/Button';
import { classApi } from '../../api/classApi';
import { teamApi } from '../../api/teamApi';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import { getTeamGroupFromMajor } from '../../constants/majors';
import type {
  ManagedTeam,
  TeamClassOption,
  TeamDraft,
  TeamStudent,
} from '../../types/teamManagement';
import {
  buildStudentTeamAssignments,
  entityId,
  getTeamMemberIds,
  getTeamProject,
  normalizeManagedTeam,
  isMissingTeamMajor,
  TEAM_MEMBER_LIMIT,
  validateTeamDraft,
} from '../../utils/teamManagement';

interface TeamManagementModalProps {
  classInfo: TeamClassOption;
  students: TeamStudent[];
  teams: ManagedTeam[];
  team?: ManagedTeam | null;
  initialMemberIds?: string[];
  initialLeaderId?: string;
  onClose: () => void;
  onSave: (team: ManagedTeam) => void;
}

const PROJECT_STATUSES = [
  { value: 'DRAFT', label: 'Draft' },
  { value: 'IN_PROGRESS', label: 'In progress' },
  { value: 'VALIDATED', label: 'Validated' },
  { value: 'COMPLETED', label: 'Completed' },
];

export default function TeamManagementModal({
  classInfo,
  students,
  teams,
  team = null,
  initialMemberIds = [],
  initialLeaderId = '',
  onClose,
  onSave,
}: TeamManagementModalProps) {
  const currentProject = team ? getTeamProject(team) : null;
  const initialIds = team ? getTeamMemberIds(team) : initialMemberIds;
  const knownStudentIds = new Set(students.map((student) => student._id));
  const [draft, setDraft] = useState<TeamDraft>({
    teamName: team?.teamName || '',
    classId: classInfo.id,
    memberIds: [...new Set(initialIds.filter((studentId) => knownStudentIds.has(studentId)))],
    leaderId: team ? entityId(team.leaderId) : initialLeaderId,
    description: team?.description || '',
    projectName: currentProject?.name || '',
    projectDescription: currentProject?.description || '',
    projectStatus: currentProject?.status || 'DRAFT',
    startupField: currentProject?.startupField || '',
  });
  const [search, setSearch] = useState('');
  const [classMentors, setClassMentors] = useState<any[]>([]);
  const [mentorId, setMentorId] = useState(
    team?.currentMentorAssignment?.mentor?.mentorProfileId
    || entityId(team?.mentorId)
    || '',
  );
  const [attemptedSubmit, setAttemptedSubmit] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const currentTeamId = team?._id || '';
  const validation = useMemo(
    () => validateTeamDraft(draft, teams, students, currentTeamId),
    [currentTeamId, draft, students, teams],
  );
  const assignments = useMemo(
    () => buildStudentTeamAssignments(teams, students),
    [students, teams],
  );
  const selectedStudents = useMemo(
    () => draft.memberIds
      .map((studentId) => students.find((student) => student._id === studentId))
      .filter((student): student is TeamStudent => Boolean(student)),
    [draft.memberIds, students],
  );
  const visibleStudents = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return students;
    return students.filter((student) => [student.fullName, student.rollNumber, student.email, student.major]
      .some((value) => value?.toLowerCase().includes(query)));
  }, [search, students]);
  const formationSummary = useMemo(() => {
    const majorCodes = [...new Set(selectedStudents
      .map((student) => student.major?.trim().toUpperCase())
      .filter(Boolean))];
    const missingMajorStudents = selectedStudents.filter((student) => isMissingTeamMajor(student.major));
    const hasGroupOne = selectedStudents.some((student) => getTeamGroupFromMajor(student.major) === 'GROUP_1');
    const hasGroupTwo = selectedStudents.some((student) => getTeamGroupFromMajor(student.major) === 'GROUP_2');
    const isStandardSize = draft.memberIds.length >= 4 && draft.memberIds.length <= 6;

    return {
      majorCodes,
      missingMajorStudents,
      hasGroupOne,
      hasGroupTwo,
      isStandardSize,
      unassignedStudentCount: students.filter((student) => !student.teamId).length,
    };
  }, [draft.memberIds.length, selectedStudents, students, team]);

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

  useEffect(() => {
    let cancelled = false;

    const loadClassMentors = async () => {
      try {
        const response = await classApi.getClassMentors(classInfo.id);
        const assignments = unwrapApiData<any>(response);
        const mentors = new Map<string, any>();
        (Array.isArray(assignments) ? assignments : []).forEach((assignment) => {
          const mentor = assignment?.mentor;
          const id = mentor?.mentorProfileId || mentor?.id;
          if (id) mentors.set(String(id), mentor);
        });
        if (!cancelled) setClassMentors([...mentors.values()]);
      } catch {
        if (!cancelled) setClassMentors([]);
      }
    };

    void loadClassMentors();
    return () => {
      cancelled = true;
    };
  }, [classInfo.id]);

  const updateDraft = <Field extends keyof TeamDraft>(field: Field, value: TeamDraft[Field]) => {
    setDraft((current) => ({ ...current, [field]: value }));
  };

  const toggleMember = (student: TeamStudent) => {
    const assignment = assignments.get(student._id);
    if (assignment && assignment.teamId !== currentTeamId) {
      toast.error(`${student.fullName} is already assigned to ${assignment.teamName}`);
      return;
    }

    setDraft((current) => {
      const isSelected = current.memberIds.includes(student._id);
      const nextMemberIds = isSelected
        ? current.memberIds.filter((studentId) => studentId !== student._id)
        : [...current.memberIds, student._id];
      return {
        ...current,
        memberIds: nextMemberIds,
        leaderId: isSelected && current.leaderId === student._id ? '' : current.leaderId,
      };
    });
  };

  const handleSubmit = async () => {
    setAttemptedSubmit(true);
    if (!validation.isValid) {
      toast.error('Please correct the highlighted team information.');
      return;
    }

    setSubmitting(true);
    try {
      const response = team
        ? await teamApi.updateMembers(team._id, {
            teamName: draft.teamName.trim(),
            description: draft.description.trim(),
            memberIds: draft.memberIds,
            leaderStudentId: draft.leaderId,
            rowVersion: team.rowVersion,
          })
        : await classApi.generateTeam(classInfo.id, {
            studentIds: draft.memberIds,
            leaderStudentId: draft.leaderId,
            mode: 'standard',
            teamName: draft.teamName.trim() || null,
            description: draft.description.trim() || null,
            mentorId: mentorId || null,
          });
      const payload = unwrapApiData<any>(response);
      const savedTeam = team
        ? normalizeManagedTeam(payload)
        : payload.team
          ? normalizeManagedTeam(payload.team)
          : normalizeManagedTeam(payload);
      const currentMentorId = team?.currentMentorAssignment?.mentor?.mentorProfileId
        || entityId(team?.mentorId)
        || '';
      if (team && mentorId && mentorId !== currentMentorId) {
        await teamApi.assignMentor(savedTeam._id, mentorId);
      }
      onSave(savedTeam);
      toast.success(
        team
          ? 'Team members updated successfully'
          : 'Team created successfully',
      );
    } catch (error) {
      toast.error(parseApiError(error, team ? 'Failed to update team.' : 'Failed to create team.').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[75] flex items-end justify-center p-0 sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby="team-management-title">
      <button type="button" className="absolute inset-0 cursor-default bg-slate-900/45 backdrop-blur-sm" onClick={onClose} aria-label="Close team dialog" />
      <div className="relative flex max-h-[95vh] w-full max-w-5xl flex-col overflow-hidden rounded-t-2xl border border-slate-200/60 bg-white shadow-float animate-scale-in sm:max-h-[92vh] sm:rounded-2xl">
        <header className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-100 px-5 py-4 sm:px-6">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary-50 text-primary">
              {team ? <UserCheck className="h-5 w-5" /> : <UserPlus className="h-5 w-5" />}
            </div>
            <div>
              <h2 id="team-management-title" className="text-lg font-bold text-slate-900">{team ? 'Update team' : 'Create team'}</h2>
              <p className="text-sm text-slate-500">Manage team information, members and linked project</p>
            </div>
          </div>
          <button type="button" onClick={onClose} className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700" aria-label="Close">
            <X className="h-5 w-5" />
          </button>
        </header>

        <div className="flex-1 overflow-y-auto px-5 py-5 sm:px-6">
          <div className="grid gap-6 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
            <div className="space-y-5">
              <section className="space-y-4 rounded-2xl border border-slate-200 p-4">
                <div className="flex items-center gap-2">
                  <BookOpen className="h-4 w-4 text-primary" />
                  <h3 className="text-sm font-bold text-slate-900">Team information</h3>
                </div>

                <div>
                  <label htmlFor="team-name" className="mb-1.5 block text-xs font-semibold text-slate-600">Team name {team && <span className="text-red-500">*</span>}</label>
                  <input
                    id="team-name"
                    value={draft.teamName}
                    onChange={(event) => updateDraft('teamName', event.target.value)}
                    placeholder={team ? 'Example: Nova Founders' : 'Leave blank to generate the next team name'}
                    maxLength={60}
                    className={`w-full rounded-xl border px-3 py-2.5 text-sm outline-none transition-all focus:ring-2 focus:ring-primary/20 ${attemptedSubmit && validation.errors.teamName ? 'border-red-300 bg-red-50' : 'border-slate-200 focus:border-primary'}`}
                  />
                  {attemptedSubmit && validation.errors.teamName && <p className="mt-1 text-xs text-red-600">{validation.errors.teamName}</p>}
                </div>

                <div>
                  <label className="mb-1.5 block text-xs font-semibold text-slate-600">Class <span className="text-red-500">*</span></label>
                  <div className="flex items-center gap-3 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5">
                    <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-secondary-100 text-secondary"><BookOpen className="h-4 w-4" /></div>
                    <div>
                      <p className="text-sm font-semibold text-slate-800">{classInfo.code}</p>
                      {classInfo.name && <p className="text-xs text-slate-500">{classInfo.name}</p>}
                    </div>
                  </div>
                </div>

                <div>
                  <label htmlFor="team-description" className="mb-1.5 block text-xs font-semibold text-slate-600">Team description</label>
                  <textarea
                    id="team-description"
                    value={draft.description}
                    onChange={(event) => updateDraft('description', event.target.value)}
                    placeholder="Short description of the team’s focus"
                    maxLength={500}
                    rows={3}
                    className="w-full resize-none rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                  />
                </div>

                <div>
                  <label htmlFor="team-mentor" className="mb-1.5 block text-xs font-semibold text-slate-600">Assign mentor</label>
                  <select
                    id="team-mentor"
                    value={mentorId}
                    onChange={(event) => setMentorId(event.target.value)}
                    className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:opacity-50"
                  >
                    <option value="">No mentor</option>
                    {classMentors.map((mentor) => (
                      <option key={mentor.mentorProfileId} value={mentor.mentorProfileId}>
                        {mentor.fullName} ({mentor.email})
                      </option>
                    ))}
                  </select>
                  {!classMentors.length && <p className="mt-1 text-xs text-slate-500">Only mentors already assigned to this class are available.</p>}
                </div>
              </section>

              <section className="hidden space-y-4 rounded-2xl border border-secondary-100 bg-secondary-50/50 p-4">
                <div className="flex items-center gap-2">
                  <FolderKanban className="h-4 w-4 text-secondary" />
                  <div>
                    <h3 className="text-sm font-bold text-slate-900">Project direction</h3>
                    <p className="text-xs text-slate-500">Created separately by the team leader after this official team is saved.</p>
                  </div>
                </div>

                <div>
                  <label htmlFor="project-name" className="mb-1.5 block text-xs font-semibold text-slate-600">Project name</label>
                  <input id="project-name" value={draft.projectName} onChange={(event) => updateDraft('projectName', event.target.value)} placeholder="Example: EcoTrack" maxLength={100} className={`w-full rounded-xl border bg-white px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-secondary/20 ${attemptedSubmit && validation.errors.projectName ? 'border-red-300' : 'border-slate-200 focus:border-secondary'}`} />
                  {attemptedSubmit && validation.errors.projectName && <p className="mt-1 text-xs text-red-600">{validation.errors.projectName}</p>}
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <label htmlFor="project-status" className="mb-1.5 block text-xs font-semibold text-slate-600">Status</label>
                    <select id="project-status" value={draft.projectStatus} onChange={(event) => updateDraft('projectStatus', event.target.value)} disabled={!draft.projectName.trim()} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-secondary disabled:opacity-50">
                      {PROJECT_STATUSES.map((status) => <option key={status.value} value={status.value}>{status.label}</option>)}
                    </select>
                  </div>
                  <div>
                    <label htmlFor="startup-field" className="mb-1.5 block text-xs font-semibold text-slate-600">Startup field</label>
                    <input id="startup-field" value={draft.startupField} onChange={(event) => updateDraft('startupField', event.target.value)} disabled={!draft.projectName.trim()} placeholder="EdTech, FinTech…" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-secondary disabled:opacity-50" />
                  </div>
                </div>

                <div>
                  <label htmlFor="project-summary" className="mb-1.5 block text-xs font-semibold text-slate-600">Project summary</label>
                  <textarea id="project-summary" value={draft.projectDescription} onChange={(event) => updateDraft('projectDescription', event.target.value)} disabled={!draft.projectName.trim()} placeholder="Problem and solution overview" maxLength={500} rows={3} className={`w-full resize-none rounded-xl border bg-white px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-secondary/20 disabled:opacity-50 ${attemptedSubmit && validation.errors.projectDescription ? 'border-red-300' : 'border-slate-200 focus:border-secondary'}`} />
                  {attemptedSubmit && validation.errors.projectDescription && <p className="mt-1 text-xs text-red-600">{validation.errors.projectDescription}</p>}
                </div>
              </section>
            </div>

            <section className="flex min-h-[520px] flex-col rounded-2xl border border-slate-200">
              <div className="border-b border-slate-100 p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <Users className="h-4 w-4 text-primary" />
                    <div>
                      <h3 className="text-sm font-bold text-slate-900">Team members <span className="text-red-500">*</span></h3>
                      <p className="text-xs text-slate-500">{draft.memberIds.length}/{TEAM_MEMBER_LIMIT} students selected</p>
                    </div>
                  </div>
                  {draft.memberIds.length > 0 && (
                    <span className="rounded-full bg-primary-50 px-2.5 py-1 text-xs font-bold text-primary">{draft.memberIds.length} selected</span>
                  )}
                </div>

                <div className="relative mt-3">
                  <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                  <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search name, student code, email or major" className="w-full rounded-xl border border-slate-200 py-2.5 pl-9 pr-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
                </div>
                {attemptedSubmit && validation.errors.memberIds && (
                  <div className="mt-3 flex items-start gap-2 rounded-lg bg-red-50 p-2.5 text-xs text-red-700"><AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />{validation.errors.memberIds}</div>
                )}
                <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
                  <span className={`rounded-lg px-2 py-1.5 font-semibold ${formationSummary.isStandardSize ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
                    {formationSummary.isStandardSize ? '4-6 members' : 'Invalid member count'}
                  </span>
                  <span className={`rounded-lg px-2 py-1.5 font-semibold ${formationSummary.hasGroupOne ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>Has GROUP_1</span>
                  <span className={`rounded-lg px-2 py-1.5 font-semibold ${formationSummary.hasGroupTwo ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>Has GROUP_2</span>
                  <span className={`rounded-lg px-2 py-1.5 font-semibold ${draft.leaderId && draft.memberIds.includes(draft.leaderId) ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>Leader selected</span>
                </div>
                <p className="mt-2 text-xs text-slate-500">{students.length} total students · {formationSummary.unassignedStudentCount} without a team</p>
                {formationSummary.majorCodes.length > 0 && <p className="mt-1 text-xs text-slate-500">Majors: {formationSummary.majorCodes.join(', ')}</p>}
                {formationSummary.missingMajorStudents.length > 0 && <p className="mt-1 text-xs font-medium text-amber-700">{formationSummary.missingMajorStudents.length} selected student(s) have no major and do not satisfy a major-group check.</p>}
              </div>

              <div className="flex-1 space-y-2 overflow-y-auto p-3">
                {visibleStudents.length === 0 ? (
                  <p className="py-12 text-center text-sm text-slate-400">No students match this search.</p>
                ) : visibleStudents.map((student) => {
                  const assignment = assignments.get(student._id);
                  const assignedElsewhere = Boolean(assignment && assignment.teamId !== currentTeamId);
                  const selected = draft.memberIds.includes(student._id);
                  return (
                    <button
                      key={student._id}
                      type="button"
                      disabled={assignedElsewhere || (!selected && draft.memberIds.length >= TEAM_MEMBER_LIMIT)}
                      onClick={() => toggleMember(student)}
                      className={`flex w-full items-center gap-3 rounded-xl border p-3 text-left transition-all ${selected ? 'border-primary bg-primary-50' : assignedElsewhere ? 'cursor-not-allowed border-slate-100 bg-slate-50 opacity-65' : 'border-slate-200 hover:border-primary/50 hover:bg-slate-50'} disabled:pointer-events-none`}
                    >
                      <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-md border ${selected ? 'border-primary bg-primary text-white' : 'border-slate-300 bg-white text-transparent'}`}>
                        <Check className="h-3.5 w-3.5" />
                      </span>
                      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-secondary-300 to-secondary text-xs font-bold text-white">{student.fullName.charAt(0).toUpperCase()}</span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-semibold text-slate-800">{student.fullName}</span>
                        <span className="block truncate text-xs text-slate-500">{student.rollNumber || 'No student code'}{student.major ? ` · ${student.major}` : ''}</span>
                      </span>
                      {assignedElsewhere ? (
                        <span className="max-w-36 rounded-full bg-amber-100 px-2 py-1 text-right text-[10px] font-semibold text-amber-700">In {assignment?.teamName}</span>
                      ) : selected ? (
                        <span className="text-xs font-semibold text-primary">Selected</span>
                      ) : null}
                    </button>
                  );
                })}
              </div>

              <div className="border-t border-slate-100 bg-slate-50/60 p-4">
                <label htmlFor="team-leader" className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-slate-600"><Crown className="h-3.5 w-3.5 text-amber-500" /> Team leader</label>
                <select id="team-leader" value={draft.leaderId} onChange={(event) => updateDraft('leaderId', event.target.value)} disabled={selectedStudents.length === 0} className={`w-full rounded-xl border bg-white px-3 py-2.5 text-sm outline-none focus:border-primary disabled:opacity-50 ${attemptedSubmit && validation.errors.leaderId ? 'border-red-300' : 'border-slate-200'}`}>
                  <option value="">No leader selected</option>
                  {selectedStudents.map((student) => <option key={student._id} value={student._id}>{student.fullName} ({student.rollNumber || student._id})</option>)}
                </select>
                {attemptedSubmit && validation.errors.leaderId && <p className="mt-1 text-xs text-red-600">{validation.errors.leaderId}</p>}
              </div>
            </section>
          </div>
        </div>

        <footer className="flex shrink-0 flex-col-reverse gap-2 border-t border-slate-100 bg-slate-50/70 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <p className="text-xs text-slate-500"><strong>{draft.memberIds.length}</strong> member{draft.memberIds.length === 1 ? '' : 's'} · {draft.projectName.trim() ? 'Project linked' : 'No project linked'}</p>
          <div className="flex gap-2">
            <Button variant="outline" onClick={onClose}>Cancel</Button>
            <Button
              variant="gradient"
              icon={team ? UserCheck : UserPlus}
              isLoading={submitting}
              disabled={!validation.isValid}
              onClick={handleSubmit}
            >
              {team ? 'Save members' : 'Create team'}
            </Button>
          </div>
        </footer>
      </div>
    </div>
  );
}
