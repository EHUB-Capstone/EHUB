import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, Search, Check, Users } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { teamApi } from '../../api/teamApi';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import { canAssignMentorTypeToTeam, normalizeManagedTeam } from '../../utils/teamManagement';
import type { ManagedTeam, MentorAssignment, MentorCandidate } from '../../types/teamManagement';
import type { ApiEnvelope } from '../../types/classes';
import ConfirmDialog from '../ui/ConfirmDialog';

interface AssignMentorsModalProps {
  classId: string;
  currentMentors?: unknown[];
  onClose: () => void;
  onAssigned: () => Promise<void> | void;
}

interface MentorOption {
  _id: string;
  name: string;
  email: string;
  organization?: string | null;
  mentorType: 'Enterprise' | 'Academic';
  activeTeamCount: number;
}

interface PendingEndAssignment {
  team: ManagedTeam;
  assignment: MentorAssignment;
}

export default function AssignMentorsModal({ classId, currentMentors: _currentMentors = [], onClose, onAssigned }: AssignMentorsModalProps) {
  const [mentors, setMentors] = useState<MentorOption[]>([]);
  const [teams, setTeams] = useState<ManagedTeam[]>([]);
  const [selectedMentorId, setSelectedMentorId] = useState('');
  const [selectedTeamId, setSelectedTeamId] = useState('');
  const [mentorSearchTerm, setMentorSearchTerm] = useState('');
  const [teamSearchTerm, setTeamSearchTerm] = useState('');
  const [activeView, setActiveView] = useState<'current' | 'assign'>('current');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [endingTeamId, setEndingTeamId] = useState('');
  const [pendingEndAssignment, setPendingEndAssignment] = useState<PendingEndAssignment | null>(null);
  const [endReason, setEndReason] = useState('');

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [mentorRes, teamRes] = await Promise.all([
          classApi.getMentorCandidates(classId),
          classApi.getTeams(classId),
        ]);

        const mentorCandidates = unwrapApiData<MentorCandidate[]>(mentorRes as ApiEnvelope<MentorCandidate[]> | MentorCandidate[]) || [];
        const teamData = unwrapApiData(teamRes) || [];
        const mentorList = mentorCandidates.map(candidate => ({
          _id: candidate.mentor.mentorProfileId,
          name: candidate.mentor.fullName,
          email: candidate.mentor.email,
          organization: candidate.mentor.organization,
          mentorType: candidate.mentor.mentorType,
          activeTeamCount: candidate.activeTeamCount,
        }));
        const teamList = (Array.isArray(teamData) ? teamData : []).map(normalizeManagedTeam);

        setMentors(mentorList);
        setTeams(teamList);
        if (!teamList.some(team => (team.currentMentorAssignments?.length || 0) > 0)) {
          setActiveView('assign');
        }
      } catch {
        toast.error('Failed to load mentors or teams');
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [classId]);

  const handleMentorChange = (mentorId: string) => {
    const nextMentorId = selectedMentorId === mentorId ? '' : mentorId;
    setSelectedMentorId(nextMentorId);
    setSelectedTeamId('');
    setTeamSearchTerm('');
  };

  const handleSubmit = async () => {
    if (!selectedMentorId) {
      toast.error('Please choose a mentor');
      return;
    }
    if (!selectedTeamId) {
      toast.error('Please choose a team');
      return;
    }

    setSubmitting(true);
    try {
      await teamApi.assignMentor(selectedTeamId, selectedMentorId);
      toast.success('Mentor assigned to team successfully!');
      await onAssigned();
    } catch (e) {
      toast.error(parseApiError(e, 'Failed to assign mentor to team').message);
    } finally {
      setSubmitting(false);
    }
  };

  const requestEndAssignment = (team: ManagedTeam, assignment: MentorAssignment) => {
    setPendingEndAssignment({ team, assignment });
    setEndReason('');
  };

  const closeEndAssignmentDialog = () => {
    if (endingTeamId) return;
    setPendingEndAssignment(null);
    setEndReason('');
  };

  const handleEndAssignment = async () => {
    if (!pendingEndAssignment || endReason.trim().length < 3) return;
    const { team, assignment } = pendingEndAssignment;
    setEndingTeamId(assignment.assignmentId);
    try {
      await teamApi.endMentorAssignment(team._id, assignment.assignmentId, endReason.trim());
      toast.success('Mentor assignment ended');
      setTeams(current => current.map(item => item._id === team._id
        ? {
            ...item,
            currentMentorAssignments: (item.currentMentorAssignments || []).filter(currentAssignment => currentAssignment.assignmentId !== assignment.assignmentId),
            currentMentorAssignment: item.currentMentorAssignment?.assignmentId === assignment.assignmentId ? null : item.currentMentorAssignment,
          }
        : item));
      setPendingEndAssignment(null);
      setEndReason('');
      await onAssigned();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to end mentor assignment').message);
    } finally {
      setEndingTeamId('');
    }
  };

  const filteredMentors = mentors.filter(m =>
    m.name?.toLowerCase().includes(mentorSearchTerm.toLowerCase()) ||
    m.email?.toLowerCase().includes(mentorSearchTerm.toLowerCase())
  );

  const selectedMentor = mentors.find(mentor => mentor._id === selectedMentorId);
  const eligibleTeams = selectedMentor
    ? teams.filter(team => canAssignMentorTypeToTeam(team, selectedMentor.mentorType))
    : [];
  const filteredTeams = eligibleTeams.filter(team =>
    team.teamName?.toLowerCase().includes(teamSearchTerm.toLowerCase()) ||
    team.teamCode?.toLowerCase().includes(teamSearchTerm.toLowerCase()) ||
    team.groupName?.toLowerCase().includes(teamSearchTerm.toLowerCase())
  );
  const currentAssignments = teams.flatMap(team => (team.currentMentorAssignments || []).map(assignment => ({ team, assignment })));

  return (
    <>
      <div className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4" role="dialog" aria-modal="true" aria-labelledby="manage-mentors-title">
        <div className="absolute inset-0 bg-black/40 backdrop-blur-xs animate-fade-in" onClick={submitting || endingTeamId ? undefined : onClose} />
        <div className="relative flex max-h-[calc(100dvh-1rem)] w-full max-w-3xl flex-col overflow-hidden rounded-t-2xl bg-white shadow-float animate-scale-in sm:max-h-[calc(100dvh-3rem)] sm:rounded-2xl">
          <div className="flex shrink-0 items-center justify-between border-b border-slate-100 px-5 py-4">
            <div>
              <h2 id="manage-mentors-title" className="text-lg font-bold text-slate-900">Manage team mentors</h2>
              <p className="mt-0.5 text-xs font-medium text-slate-400">Review current assignments or assign a mentor to a team</p>
            </div>
            <button type="button" onClick={onClose} disabled={submitting || Boolean(endingTeamId)} className="rounded-xl p-2 text-slate-400 transition-all hover:bg-slate-100 hover:text-slate-600 disabled:opacity-50" aria-label="Close mentor management">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="shrink-0 border-b border-slate-100 px-5 pt-3">
            <div className="flex gap-1" role="tablist" aria-label="Mentor management views">
              <button
                type="button"
                role="tab"
                aria-selected={activeView === 'current'}
                onClick={() => setActiveView('current')}
                className={`border-b-2 px-3 py-2 text-xs font-semibold transition ${activeView === 'current' ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
              >
                Current assignments
                <span className="ml-1.5 rounded-full bg-slate-100 px-1.5 py-0.5 text-[10px] text-slate-600">{currentAssignments.length}</span>
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={activeView === 'assign'}
                onClick={() => setActiveView('assign')}
                className={`border-b-2 px-3 py-2 text-xs font-semibold transition ${activeView === 'assign' ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
              >
                Assign new
              </button>
            </div>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-5">
            {loading ? (
              <div className="flex items-center justify-center py-16">
                <Loader2 className="h-6 w-6 animate-spin text-primary" />
              </div>
            ) : activeView === 'current' ? (
              currentAssignments.length === 0 ? (
                <div className="flex min-h-64 flex-col items-center justify-center rounded-xl border border-dashed border-slate-200 bg-slate-50/60 px-6 text-center">
                  <Users className="mb-3 h-8 w-8 text-slate-300" />
                  <p className="text-sm font-semibold text-slate-700">No active mentor assignments</p>
                  <p className="mt-1 text-xs text-slate-400">Assign a mentor to a team to get started.</p>
                  <button type="button" onClick={() => setActiveView('assign')} className="mt-4 rounded-lg bg-primary px-3.5 py-2 text-xs font-semibold text-white transition hover:bg-primary-700">
                    Assign mentor
                  </button>
                </div>
              ) : (
                <div className="grid gap-2.5 sm:grid-cols-2">
                  {currentAssignments.map(({ team, assignment }) => (
                    <article key={assignment.assignmentId} className="flex min-w-0 items-start justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50/60 p-3.5">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-1.5">
                          <p className="truncate text-xs font-bold text-slate-800">{team.teamName || 'Unnamed Team'}</p>
                          <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${assignment.slot === 'Academic' ? 'bg-blue-50 text-blue-700' : 'bg-orange-50 text-orange-700'}`}>
                            {assignment.slot}
                          </span>
                        </div>
                        <p className="mt-1 truncate text-xs font-medium text-slate-600" title={assignment.mentor.fullName}>{assignment.mentor.fullName}</p>
                        <p className="mt-0.5 truncate text-[10px] text-slate-400" title={assignment.mentor.email}>{assignment.mentor.email}</p>
                      </div>
                      <button
                        type="button"
                        disabled={endingTeamId === assignment.assignmentId}
                        onClick={() => requestEndAssignment(team, assignment)}
                        className="shrink-0 rounded-lg border border-red-100 bg-white px-2.5 py-1.5 text-[11px] font-semibold text-red-600 transition hover:border-red-200 hover:bg-red-50 disabled:opacity-50"
                      >
                        {endingTeamId === assignment.assignmentId ? 'Ending…' : 'End'}
                      </button>
                    </article>
                  ))}
                </div>
              )
            ) : (
              <div className="grid gap-4 md:grid-cols-2">
                <section className="min-w-0">
                  <div className="mb-2 flex items-center gap-2">
                    <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary text-[10px] font-bold text-white">1</span>
                    <h3 className="text-xs font-semibold text-slate-700">Select mentor</h3>
                  </div>
                  <div className="relative mb-2">
                    <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                    <input
                      type="search"
                      placeholder="Search by name or email..."
                      value={mentorSearchTerm}
                      onChange={(event) => setMentorSearchTerm(event.target.value)}
                      className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-3 text-xs outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
                    />
                  </div>
                  <div className="h-64 space-y-1.5 overflow-y-auto rounded-xl border border-slate-100 bg-slate-50/50 p-2">
                    {filteredMentors.length === 0 ? (
                      <p className="py-8 text-center text-xs text-slate-400">No mentors found</p>
                    ) : filteredMentors.map(mentor => {
                      const isChecked = selectedMentorId === mentor._id;
                      return (
                        <button
                          key={mentor._id}
                          type="button"
                          aria-pressed={isChecked}
                          onClick={() => handleMentorChange(mentor._id)}
                          className={`flex w-full items-center justify-between rounded-xl border p-2.5 text-left transition-all ${isChecked ? 'border-primary/30 bg-primary-50/50 shadow-xs' : 'border-slate-200/60 bg-white hover:bg-slate-50'}`}
                        >
                          <div className="flex min-w-0 items-center gap-2.5">
                            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-amber-100 text-xs font-bold text-amber-600">
                              {mentor.name?.charAt(0)?.toUpperCase() || 'M'}
                            </div>
                            <div className="min-w-0">
                              <p className="truncate text-xs font-semibold text-slate-800">{mentor.name}</p>
                              <p className="truncate text-[10px] text-slate-400">{mentor.mentorType} · {mentor.activeTeamCount} teams this semester</p>
                            </div>
                          </div>
                          <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border ${isChecked ? 'border-primary bg-primary' : 'border-slate-200 bg-white'}`}>
                            {isChecked && <Check className="h-3 w-3 text-white" />}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </section>

                <section className="min-w-0">
                  <div className="mb-2 flex items-center gap-2">
                    <span className={`flex h-5 w-5 items-center justify-center rounded-full text-[10px] font-bold ${selectedMentorId ? 'bg-primary text-white' : 'bg-slate-200 text-slate-500'}`}>2</span>
                    <h3 className="text-xs font-semibold text-slate-700">Select team</h3>
                    {selectedMentor && (
                      <span className={`ml-auto rounded-full px-2 py-0.5 text-[10px] font-semibold ${selectedMentor.mentorType === 'Academic' ? 'bg-blue-50 text-blue-700' : 'bg-orange-50 text-orange-700'}`}>
                        Needs {selectedMentor.mentorType}
                      </span>
                    )}
                  </div>
                  <div className="relative mb-2">
                    <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                    <input
                      type="search"
                      placeholder="Search teams..."
                      value={teamSearchTerm}
                      onChange={(event) => setTeamSearchTerm(event.target.value)}
                      disabled={!selectedMentorId}
                      className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-3 text-xs outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-400"
                    />
                  </div>
                  <div className="h-64 space-y-1.5 overflow-y-auto rounded-xl border border-slate-100 bg-slate-50/50 p-2">
                    {!selectedMentorId ? (
                      <div className="flex h-full flex-col items-center justify-center px-5 text-center">
                        <Users className="mb-2 h-7 w-7 text-slate-300" />
                        <p className="text-xs font-medium text-slate-500">Select a mentor first</p>
                        <p className="mt-1 text-[10px] text-slate-400">Available teams will appear here.</p>
                      </div>
                    ) : filteredTeams.length === 0 ? (
                      <div className="flex h-full flex-col items-center justify-center px-5 text-center">
                        <Users className="mb-2 h-7 w-7 text-slate-300" />
                        <p className="text-xs font-medium text-slate-500">
                          {teamSearchTerm ? 'No matching teams found' : `All active teams already have an ${selectedMentor?.mentorType} mentor`}
                        </p>
                        <p className="mt-1 text-[10px] text-slate-400">
                          {teamSearchTerm ? 'Try a different team name or code.' : 'End an existing assignment first if you need to replace a mentor.'}
                        </p>
                      </div>
                    ) : filteredTeams.map(team => {
                      const isChecked = selectedTeamId === team._id;
                      return (
                        <button
                          key={team._id}
                          type="button"
                          aria-pressed={isChecked}
                          onClick={() => setSelectedTeamId(current => current === team._id ? '' : team._id)}
                          className={`flex w-full items-center justify-between rounded-xl border p-2.5 text-left transition-all ${isChecked ? 'border-primary/30 bg-primary-50/50 shadow-xs' : 'border-slate-200/60 bg-white hover:bg-slate-50'}`}
                        >
                          <div className="flex min-w-0 items-center gap-2.5">
                            <Users className="h-4 w-4 shrink-0 text-slate-400" />
                            <div className="min-w-0">
                              <p className="truncate text-xs font-semibold text-slate-800">{team.teamName || 'Unnamed Team'}</p>
                              <p className="truncate text-[10px] text-slate-400">{team.teamCode || team.groupName || '—'}</p>
                            </div>
                          </div>
                          <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border ${isChecked ? 'border-primary bg-primary' : 'border-slate-200 bg-white'}`}>
                            {isChecked && <Check className="h-3 w-3 text-white" />}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </section>
              </div>
            )}
          </div>

          {activeView === 'assign' && (
            <div className="flex shrink-0 gap-3 border-t border-slate-100 bg-white px-5 py-4">
              <button type="button" onClick={onClose} disabled={submitting} className="flex-1 rounded-xl border border-slate-200 px-4 py-2 text-xs font-semibold text-slate-500 transition-all hover:bg-slate-50 disabled:opacity-50">
                Cancel
              </button>
              <button
                type="button"
                onClick={handleSubmit}
                disabled={submitting || loading || !selectedMentorId || !selectedTeamId}
                className="flex flex-1 items-center justify-center gap-1.5 rounded-xl bg-primary px-4 py-2 text-xs font-semibold text-white shadow-sm transition-all hover:bg-primary-700 disabled:opacity-50"
              >
                {submitting ? <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Saving...</> : 'Assign mentor'}
              </button>
            </div>
          )}
        </div>
      </div>

      <ConfirmDialog
        isOpen={Boolean(pendingEndAssignment)}
        onClose={closeEndAssignmentDialog}
        onConfirm={handleEndAssignment}
        title="End mentor assignment?"
        description={pendingEndAssignment
          ? `${pendingEndAssignment.assignment.mentor.fullName} will no longer be assigned as the ${pendingEndAssignment.assignment.slot} mentor for ${pendingEndAssignment.team.teamName}.`
          : ''}
        reason={endReason}
        onReasonChange={setEndReason}
        reasonLabel="Reason for ending assignment"
        reasonRequired
        isSubmitting={Boolean(endingTeamId)}
        confirmText="End assignment"
        confirmVariant="danger"
        actionLayout="confirmWide"
      />
    </>
  );
}
