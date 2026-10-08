import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, Search, Check, Users, Minus, CircleAlert, CheckCircle2 } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { teamApi } from '../../api/teamApi';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import { canAssignMentorTypeToTeam, normalizeManagedTeam } from '../../utils/teamManagement';
import {
  assignMentorToTeams,
  getSelectionState,
  keepEligibleSelection,
  setVisibleSelection,
  summarizeBatch,
  toggleSelection,
  type BatchSummary,
} from '../../utils/mentorBatchAssignment';
import type { ManagedTeam, MentorAssignment, MentorCandidate } from '../../types/teamManagement';
import type { ApiEnvelope } from '../../types/classes';
import ConfirmDialog from '../ui/ConfirmDialog';
import { matchesSearchQuery } from '../../utils/searchText';

interface AssignMentorsModalProps {
  classId: string;
  currentMentors?: unknown[];
  onClose: () => void;
  /**
   * Called after every change (assignments added or ended). The parent should refresh its data in the
   * background and must not close this dialog, so several teams can be handled in one visit.
   */
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

const mentorTypeLabel: Record<MentorOption['mentorType'], string> = {
  Enterprise: 'Enterprise mentor',
  Academic: 'Lecturer mentor',
};

const compareNatural = (left: string, right: string) =>
  left.localeCompare(right, undefined, { numeric: true, sensitivity: 'base' });

export default function AssignMentorsModal({ classId, currentMentors: _currentMentors = [], onClose, onAssigned }: AssignMentorsModalProps) {
  const [mentors, setMentors] = useState<MentorOption[]>([]);
  const [teams, setTeams] = useState<ManagedTeam[]>([]);
  const [selectedMentorId, setSelectedMentorId] = useState('');
  const [selectedTeamIds, setSelectedTeamIds] = useState<string[]>([]);
  const [mentorSearchTerm, setMentorSearchTerm] = useState('');
  const [teamSearchTerm, setTeamSearchTerm] = useState('');
  const [activeView, setActiveView] = useState<'current' | 'assign'>('current');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<BatchSummary | null>(null);
  const [endingTeamId, setEndingTeamId] = useState('');
  const [pendingEndAssignment, setPendingEndAssignment] = useState<PendingEndAssignment | null>(null);
  const [endReason, setEndReason] = useState('');
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const loadData = useCallback(async (quiet: boolean) => {
    if (quiet) setRefreshing(true);
    try {
      const [mentorRes, teamRes] = await Promise.all([
        classApi.getMentorCandidates(classId),
        classApi.getTeams(classId),
      ]);
      if (!mounted.current) return;

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
      if (!quiet && !teamList.some(team => (team.currentMentorAssignments?.length || 0) > 0)) {
        setActiveView('assign');
      }
    } catch {
      if (mounted.current) toast.error('Failed to load mentors or teams');
    } finally {
      if (mounted.current) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [classId]);

  useEffect(() => {
    void loadData(false);
  }, [loadData]);

  const handleMentorChange = (mentorId: string) => {
    setSelectedMentorId(current => (current === mentorId ? '' : mentorId));
    setSelectedTeamIds([]);
    setTeamSearchTerm('');
    setResult(null);
  };

  const filteredMentors = useMemo(
    () => mentors.filter(m => matchesSearchQuery(mentorSearchTerm, [m.name, m.email])),
    [mentors, mentorSearchTerm],
  );
  const selectedMentor = mentors.find(mentor => mentor._id === selectedMentorId);
  const eligibleTeams = useMemo(
    () => (selectedMentor
      ? teams
        .filter(team => canAssignMentorTypeToTeam(team, selectedMentor.mentorType))
        .sort((left, right) => compareNatural(left.teamCode || left.teamName || '', right.teamCode || right.teamName || ''))
      : []),
    [teams, selectedMentor],
  );
  const filteredTeams = useMemo(
    () => eligibleTeams.filter(team => matchesSearchQuery(teamSearchTerm, [team.teamName, team.teamCode, team.groupName])),
    [eligibleTeams, teamSearchTerm],
  );
  // A tick never survives for a team that can no longer take this mentor (for example after a refresh).
  const chosenTeamIds = useMemo(
    () => keepEligibleSelection(selectedTeamIds, eligibleTeams.map(team => team._id)),
    [selectedTeamIds, eligibleTeams],
  );
  const filteredTeamIds = filteredTeams.map(team => team._id);
  const selectionState = getSelectionState(chosenTeamIds, filteredTeamIds);
  const currentAssignments = useMemo(
    () => teams
      .flatMap(team => (team.currentMentorAssignments || []).map(assignment => ({ team, assignment })))
      .sort((left, right) =>
        compareNatural(left.team.teamCode || left.team.teamName || '', right.team.teamCode || right.team.teamName || '')
        || left.assignment.slot.localeCompare(right.assignment.slot)),
    [teams],
  );

  const handleSubmit = async () => {
    if (!selectedMentor || chosenTeamIds.length === 0 || submitting) return;
    const chosen = eligibleTeams
      .filter(team => chosenTeamIds.includes(team._id))
      .map(team => ({ id: team._id, name: team.teamName || team.teamCode || 'Unnamed team' }));

    setSubmitting(true);
    setResult(null);
    try {
      const results = await assignMentorToTeams(
        chosen,
        teamId => teamApi.assignMentor(teamId, selectedMentor._id),
        error => parseApiError(error, 'Failed to assign mentor to team').message,
      );
      if (!mounted.current) return;

      const summary = summarizeBatch(results);
      setResult(summary);
      if (summary.tone === 'success') toast.success(summary.message);
      else if (summary.tone === 'error') toast.error(summary.message);
      else toast(summary.message, { icon: '⚠️' });

      // Teams that failed stay ticked so the admin can read the reason and retry them.
      setSelectedTeamIds(summary.failed.map(item => item.teamId));
      await Promise.all([loadData(true), Promise.resolve(onAssigned())]);
    } finally {
      if (mounted.current) setSubmitting(false);
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
      if (!mounted.current) return;
      toast.success('Mentor assignment ended');
      setPendingEndAssignment(null);
      setEndReason('');
      setResult(null);
      await Promise.all([loadData(true), Promise.resolve(onAssigned())]);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to end mentor assignment').message);
    } finally {
      if (mounted.current) setEndingTeamId('');
    }
  };

  const busy = submitting || Boolean(endingTeamId);
  const assignLabel = chosenTeamIds.length === 0
    ? 'Assign mentor'
    : `Assign to ${chosenTeamIds.length} team${chosenTeamIds.length === 1 ? '' : 's'}`;

  return (
    <>
      <div className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4" role="dialog" aria-modal="true" aria-labelledby="manage-mentors-title">
        <div className="absolute inset-0 bg-black/40 backdrop-blur-xs animate-fade-in" onClick={busy ? undefined : onClose} />
        <div className="relative flex max-h-[calc(100dvh-1rem)] w-full max-w-3xl flex-col overflow-hidden rounded-t-2xl bg-white shadow-float animate-scale-in sm:max-h-[calc(100dvh-3rem)] sm:rounded-2xl">
          <div className="flex shrink-0 items-center justify-between border-b border-slate-100 px-5 py-4">
            <div>
              <h2 id="manage-mentors-title" className="text-lg font-bold text-slate-900">Manage team mentors</h2>
              <p className="mt-0.5 text-xs font-medium text-slate-400">Assign one mentor to several teams, or end a current assignment</p>
            </div>
            <button type="button" onClick={onClose} disabled={busy} className="rounded-xl p-2 text-slate-400 transition-all hover:bg-slate-100 hover:text-slate-600 disabled:opacity-50" aria-label="Close mentor management">
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

          <div className={`min-h-0 flex-1 overflow-y-auto p-4 sm:p-5 ${refreshing ? 'opacity-70' : ''}`} aria-busy={refreshing}>
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
                            {mentorTypeLabel[assignment.slot]}
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
              <div className="space-y-3">
                {result && (
                  <div
                    role={result.tone === 'success' ? 'status' : 'alert'}
                    className={`rounded-xl px-3 py-2.5 text-xs ${result.tone === 'success' ? 'bg-green-50 text-green-700' : result.tone === 'partial' ? 'bg-amber-50 text-amber-800' : 'bg-red-50 text-red-700'}`}
                  >
                    <p className="flex items-center gap-1.5 font-semibold">
                      {result.tone === 'success' ? <CheckCircle2 className="h-4 w-4 shrink-0" /> : <CircleAlert className="h-4 w-4 shrink-0" />}
                      {result.message}
                    </p>
                    {result.failed.length > 0 && (
                      <ul className="mt-1.5 list-disc space-y-0.5 pl-6">
                        {result.failed.map(item => <li key={item.teamId}><strong>{item.teamName}</strong>: {item.error}</li>)}
                      </ul>
                    )}
                  </div>
                )}

                <div className="grid gap-4 md:grid-cols-2">
                  <section className="min-w-0" aria-label="Select mentor">
                    <div className="mb-2 flex items-center gap-2">
                      <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary text-[10px] font-bold text-white">1</span>
                      <h3 className="text-xs font-semibold text-slate-700">Select mentor</h3>
                    </div>
                    <div className="relative mb-2">
                      <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                      <input
                        type="search"
                        aria-label="Search mentors"
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
                            disabled={submitting}
                            onClick={() => handleMentorChange(mentor._id)}
                            className={`flex w-full items-center justify-between rounded-xl border p-2.5 text-left transition-all disabled:opacity-60 ${isChecked ? 'border-primary/30 bg-primary-50/50 shadow-xs' : 'border-slate-200/60 bg-white hover:bg-slate-50'}`}
                          >
                            <div className="flex min-w-0 items-center gap-2.5">
                              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-amber-100 text-xs font-bold text-amber-600">
                                {mentor.name?.charAt(0)?.toUpperCase() || 'M'}
                              </div>
                              <div className="min-w-0">
                                <p className="truncate text-xs font-semibold text-slate-800">{mentor.name}</p>
                                <p className="truncate text-[10px] text-slate-400">{mentorTypeLabel[mentor.mentorType]} · {mentor.activeTeamCount} team{mentor.activeTeamCount === 1 ? '' : 's'} this semester</p>
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

                  <section className="min-w-0" aria-label="Select teams">
                    <div className="mb-2 flex items-center gap-2">
                      <span className={`flex h-5 w-5 items-center justify-center rounded-full text-[10px] font-bold ${selectedMentorId ? 'bg-primary text-white' : 'bg-slate-200 text-slate-500'}`}>2</span>
                      <h3 className="text-xs font-semibold text-slate-700">Select teams</h3>
                      {selectedMentor && (
                        <span className={`ml-auto rounded-full px-2 py-0.5 text-[10px] font-semibold ${selectedMentor.mentorType === 'Academic' ? 'bg-blue-50 text-blue-700' : 'bg-orange-50 text-orange-700'}`}>
                          Needs {mentorTypeLabel[selectedMentor.mentorType].toLowerCase()}
                        </span>
                      )}
                    </div>
                    <div className="relative mb-2">
                      <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                      <input
                        type="search"
                        aria-label="Search teams"
                        placeholder="Search teams..."
                        value={teamSearchTerm}
                        onChange={(event) => setTeamSearchTerm(event.target.value)}
                        disabled={!selectedMentorId}
                        className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-3 text-xs outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:bg-slate-50"
                      />
                    </div>
                    {selectedMentorId && filteredTeams.length > 0 && (
                      <button
                        type="button"
                        role="checkbox"
                        aria-checked={selectionState === 'all' ? true : selectionState === 'some' ? 'mixed' : false}
                        disabled={submitting}
                        onClick={() => setSelectedTeamIds(current => setVisibleSelection(current, filteredTeamIds, selectionState !== 'all'))}
                        className="mb-1.5 flex w-full items-center gap-2 rounded-lg px-2 py-1 text-left text-[11px] font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-60"
                      >
                        <span className={`flex h-4 w-4 shrink-0 items-center justify-center rounded border ${selectionState === 'none' ? 'border-slate-300 bg-white' : 'border-primary bg-primary'}`}>
                          {selectionState === 'all' && <Check className="h-3 w-3 text-white" />}
                          {selectionState === 'some' && <Minus className="h-3 w-3 text-white" />}
                        </span>
                        {selectionState === 'all' ? 'Clear selection' : `Select all${teamSearchTerm ? ' shown' : ''} (${filteredTeams.length})`}
                      </button>
                    )}
                    <div className="h-56 space-y-1.5 overflow-y-auto rounded-xl border border-slate-100 bg-slate-50/50 p-2">
                      {!selectedMentorId ? (
                        <div className="flex h-full flex-col items-center justify-center px-5 text-center">
                          <Users className="mb-2 h-7 w-7 text-slate-300" />
                          <p className="text-xs font-medium text-slate-500">Select a mentor first</p>
                          <p className="mt-1 text-[10px] text-slate-400">Teams that still need this kind of mentor will appear here.</p>
                        </div>
                      ) : filteredTeams.length === 0 ? (
                        <div className="flex h-full flex-col items-center justify-center px-5 text-center">
                          <Users className="mb-2 h-7 w-7 text-slate-300" />
                          <p className="text-xs font-medium text-slate-500">
                            {teamSearchTerm ? 'No matching teams found' : `Every active team already has a ${mentorTypeLabel[selectedMentor?.mentorType ?? 'Enterprise'].toLowerCase()}`}
                          </p>
                          <p className="mt-1 text-[10px] text-slate-400">
                            {teamSearchTerm ? 'Try a different team name or code.' : 'End an existing assignment first if you need to replace a mentor.'}
                          </p>
                        </div>
                      ) : filteredTeams.map(team => {
                        const isChecked = chosenTeamIds.includes(team._id);
                        const otherSlot = selectedMentor?.mentorType === 'Enterprise' ? 'Academic' : 'Enterprise';
                        const otherMentor = (team.currentMentorAssignments || []).find(item => item.slot === otherSlot && item.status.trim().toLowerCase() === 'active');
                        return (
                          <button
                            key={team._id}
                            type="button"
                            role="checkbox"
                            aria-checked={isChecked}
                            disabled={submitting}
                            onClick={() => setSelectedTeamIds(current => toggleSelection(keepEligibleSelection(current, eligibleTeams.map(item => item._id)), team._id))}
                            className={`flex w-full items-center justify-between rounded-xl border p-2.5 text-left transition-all disabled:opacity-60 ${isChecked ? 'border-primary/30 bg-primary-50/50 shadow-xs' : 'border-slate-200/60 bg-white hover:bg-slate-50'}`}
                          >
                            <div className="flex min-w-0 items-center gap-2.5">
                              <Users className="h-4 w-4 shrink-0 text-slate-400" />
                              <div className="min-w-0">
                                <p className="truncate text-xs font-semibold text-slate-800">{team.teamName || 'Unnamed Team'}</p>
                                <p className="truncate text-[10px] text-slate-400">
                                  {team.teamCode || team.groupName || '—'} · {otherMentor ? `${mentorTypeLabel[otherSlot]}: ${otherMentor.mentor.fullName}` : `No ${mentorTypeLabel[otherSlot].toLowerCase()} yet`}
                                </p>
                              </div>
                            </div>
                            <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded border ${isChecked ? 'border-primary bg-primary' : 'border-slate-300 bg-white'}`}>
                              {isChecked && <Check className="h-3 w-3 text-white" />}
                            </span>
                          </button>
                        );
                      })}
                    </div>
                  </section>
                </div>
              </div>
            )}
          </div>

          {activeView === 'assign' && (
            <div className="flex shrink-0 flex-col gap-2 border-t border-slate-100 bg-white px-5 py-4 sm:flex-row sm:items-center">
              <p className="text-[11px] text-slate-400 sm:flex-1" aria-live="polite">
                {selectedMentor
                  ? `${chosenTeamIds.length} of ${eligibleTeams.length} team${eligibleTeams.length === 1 ? '' : 's'} selected for ${selectedMentor.name}`
                  : 'Choose a mentor, then tick the teams to assign.'}
              </p>
              <div className="flex gap-3">
                <button type="button" onClick={onClose} disabled={busy} className="rounded-xl border border-slate-200 px-4 py-2 text-xs font-semibold text-slate-500 transition-all hover:bg-slate-50 disabled:opacity-50">
                  Close
                </button>
                <button
                  type="button"
                  onClick={handleSubmit}
                  disabled={busy || loading || !selectedMentorId || chosenTeamIds.length === 0}
                  className="flex min-w-36 items-center justify-center gap-1.5 rounded-xl bg-primary px-4 py-2 text-xs font-semibold text-white shadow-sm transition-all hover:bg-primary-700 disabled:opacity-50"
                >
                  {submitting ? <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Saving...</> : assignLabel}
                </button>
              </div>
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
          ? `${pendingEndAssignment.assignment.mentor.fullName} will no longer be assigned as the ${mentorTypeLabel[pendingEndAssignment.assignment.slot].toLowerCase()} for ${pendingEndAssignment.team.teamName}.`
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
