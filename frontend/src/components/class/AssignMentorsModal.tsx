import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, Search, Check, Users, Minus, CircleAlert, CheckCircle2 } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { teamApi } from '../../api/teamApi';
import { useDialogA11y } from '../../hooks/useDialogA11y';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import { normalizeManagedTeam } from '../../utils/teamManagement';
import { getSelectionState, keepEligibleSelection, setVisibleSelection, toggleSelection } from '../../utils/mentorBatchAssignment';
import {
  buildTeamSlotRows,
  eligibleTeamsForKind,
  filterMentorOptions,
  filterTeamSlotRows,
  type MentorKindFilter,
  type MentorOption,
  type MentorSlot,
  type SlotFilter,
} from '../../utils/mentorSlotBoard';
import { MENTOR_KIND_STYLES } from '../../utils/mentorKindStyles';
import { matchesSearchQuery } from '../../utils/searchText';
import type { ManagedTeam, MentorAssignment, MentorCandidate } from '../../types/teamManagement';
import type { ApiEnvelope } from '../../types/classes';
import MentorKindTag from '../admin/MentorKindTag';
import ConfirmDialog from '../ui/ConfirmDialog';
import MentorSlotDialog from './MentorSlotDialog';
import MentorTeamsBoard from './MentorTeamsBoard';

interface AssignMentorsModalProps {
  classId: string;
  onClose: () => void;
  /**
   * Called after every change (assignments added, replaced or ended). The parent should refresh its data in the
   * background and must not close this dialog, so several teams can be handled in one visit.
   */
  onAssigned: () => Promise<void> | void;
}

interface SlotTarget {
  team: ManagedTeam;
  slot: MentorSlot;
  current: MentorAssignment | null;
}

interface PendingEndAssignment {
  team: ManagedTeam;
  assignment: MentorAssignment;
}

const KIND_CHIPS: { value: MentorKindFilter; label: string }[] = [
  { value: 'ALL', label: 'All' },
  { value: 'Enterprise', label: MENTOR_KIND_STYLES.Enterprise.label },
  { value: 'Academic', label: MENTOR_KIND_STYLES.Academic.label },
];

export default function AssignMentorsModal({ classId, onClose, onAssigned }: AssignMentorsModalProps) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const [mentors, setMentors] = useState<MentorOption[]>([]);
  const [teams, setTeams] = useState<ManagedTeam[]>([]);
  const [view, setView] = useState<'teams' | 'bulk'>('teams');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState(false);
  const [slotTarget, setSlotTarget] = useState<SlotTarget | null>(null);
  const [pendingEnd, setPendingEnd] = useState<PendingEndAssignment | null>(null);
  const [endReason, setEndReason] = useState('');
  const [ending, setEnding] = useState(false);
  const [teamSearch, setTeamSearch] = useState('');
  const [slotFilter, setSlotFilter] = useState<SlotFilter>('all');
  // Bulk view: one mentor, many teams.
  const [selectedMentorId, setSelectedMentorId] = useState('');
  const [selectedTeamIds, setSelectedTeamIds] = useState<string[]>([]);
  const [mentorSearch, setMentorSearch] = useState('');
  const [kindFilter, setKindFilter] = useState<MentorKindFilter>('ALL');
  const [bulkTeamSearch, setBulkTeamSearch] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [bulkResult, setBulkResult] = useState<{ tone: 'success' | 'error'; message: string } | null>(null);
  const mounted = useRef(true);

  const nestedOpen = slotTarget !== null || pendingEnd !== null;
  const busy = submitting || ending;
  useDialogA11y(dialogRef, { onClose, enabled: !nestedOpen, busy });

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const loadData = useCallback(async (quiet: boolean) => {
    if (quiet) setRefreshing(true);
    setLoadError(false);
    try {
      const [mentorRes, teamRes] = await Promise.all([classApi.getMentorCandidates(classId), classApi.getTeams(classId)]);
      if (!mounted.current) return;
      const candidates = unwrapApiData<MentorCandidate[]>(mentorRes as ApiEnvelope<MentorCandidate[]> | MentorCandidate[]) || [];
      const teamData = unwrapApiData(teamRes) || [];
      setMentors(candidates.map(candidate => ({
        _id: candidate.mentor.mentorProfileId,
        name: candidate.mentor.fullName,
        email: candidate.mentor.email,
        organization: candidate.mentor.organization,
        mentorType: candidate.mentor.mentorType,
        contractType: candidate.mentor.contractType,
        activeTeamCount: candidate.activeTeamCount,
      })));
      setTeams((Array.isArray(teamData) ? teamData : []).map(normalizeManagedTeam));
    } catch {
      if (mounted.current) {
        setLoadError(true);
        toast.error('Failed to load mentors or teams');
      }
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

  const refreshAfterChange = async () => {
    await Promise.all([loadData(true), Promise.resolve(onAssigned())]);
  };

  // ── Teams view ──────────────────────────────────────────────────────────────
  const slotRows = useMemo(() => buildTeamSlotRows(teams), [teams]);
  const visibleRows = useMemo(() => filterTeamSlotRows(slotRows, slotFilter, teamSearch), [slotRows, slotFilter, teamSearch]);
  const missingTeamCount = slotRows.filter(row => row.missingCount > 0).length;

  const confirmEnd = async () => {
    if (!pendingEnd || endReason.trim().length < 3) return;
    setEnding(true);
    try {
      await teamApi.endMentorAssignment(pendingEnd.team._id, pendingEnd.assignment.assignmentId, endReason.trim());
      if (!mounted.current) return;
      toast.success('Mentor assignment ended');
      setPendingEnd(null);
      setEndReason('');
      await refreshAfterChange();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to end mentor assignment').message);
    } finally {
      if (mounted.current) setEnding(false);
    }
  };

  // ── Bulk view ───────────────────────────────────────────────────────────────
  const filteredMentors = useMemo(() => filterMentorOptions(mentors, mentorSearch, kindFilter), [mentors, mentorSearch, kindFilter]);
  const selectedMentor = mentors.find(mentor => mentor._id === selectedMentorId);
  const eligibleTeams = useMemo(
    () => (selectedMentor ? eligibleTeamsForKind(teams, selectedMentor.mentorType) : []),
    [teams, selectedMentor],
  );
  const filteredTeams = useMemo(
    () => eligibleTeams.filter(team => matchesSearchQuery(bulkTeamSearch, [team.teamName, team.teamCode, team.groupName])),
    [eligibleTeams, bulkTeamSearch],
  );
  // A tick never survives for a team that can no longer take this mentor (for example after a refresh).
  const chosenTeamIds = useMemo(
    () => keepEligibleSelection(selectedTeamIds, eligibleTeams.map(team => team._id)),
    [selectedTeamIds, eligibleTeams],
  );
  const filteredTeamIds = filteredTeams.map(team => team._id);
  const selectionState = getSelectionState(chosenTeamIds, filteredTeamIds);

  const chooseMentor = (mentorId: string) => {
    setSelectedMentorId(current => (current === mentorId ? '' : mentorId));
    setSelectedTeamIds([]);
    setBulkTeamSearch('');
    setBulkResult(null);
  };

  const submitBulk = async () => {
    if (!selectedMentor || chosenTeamIds.length === 0 || submitting) return;
    setSubmitting(true);
    setBulkResult(null);
    try {
      // One request, one transaction: either every chosen team gets the mentor or none does.
      await classApi.assignMentorBatch(classId, selectedMentor._id, chosenTeamIds);
      if (!mounted.current) return;
      const message = `${selectedMentor.name} assigned to ${chosenTeamIds.length} team${chosenTeamIds.length === 1 ? '' : 's'}.`;
      setBulkResult({ tone: 'success', message });
      toast.success(message);
      setSelectedTeamIds([]);
      await refreshAfterChange();
    } catch (error) {
      // The ticks stay so the admin can read the reason and try again.
      const message = parseApiError(error, 'Failed to assign the mentor').message;
      if (mounted.current) setBulkResult({ tone: 'error', message });
      toast.error(message);
    } finally {
      if (mounted.current) setSubmitting(false);
    }
  };

  const assignLabel = chosenTeamIds.length === 0
    ? 'Assign mentor'
    : `Assign to ${chosenTeamIds.length} team${chosenTeamIds.length === 1 ? '' : 's'}`;

  return (
    <>
      <div className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4" role="dialog" aria-modal="true" aria-labelledby="manage-mentors-title">
        <div className="absolute inset-0 bg-black/40 backdrop-blur-xs animate-fade-in" onClick={busy ? undefined : onClose} />
        <div ref={dialogRef} tabIndex={-1} className="relative flex max-h-[calc(100dvh-1rem)] w-full max-w-4xl flex-col overflow-hidden rounded-t-2xl bg-white shadow-float outline-none animate-scale-in sm:max-h-[calc(100dvh-3rem)] sm:rounded-2xl">
          <div className="flex shrink-0 items-center justify-between border-b border-slate-100 px-5 py-4">
            <div>
              <h2 id="manage-mentors-title" className="text-lg font-bold text-slate-900">Manage team mentors</h2>
              <p className="mt-0.5 text-xs font-medium text-slate-400">See which teams still need a mentor, assign, replace or end a mentor</p>
            </div>
            <button type="button" onClick={onClose} disabled={busy} className="rounded-xl p-2 text-slate-400 transition-all hover:bg-slate-100 hover:text-slate-600 disabled:opacity-50" aria-label="Close mentor management">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="shrink-0 border-b border-slate-100 px-5 pt-3">
            <div className="flex gap-1" role="tablist" aria-label="Mentor management views">
              {([['teams', 'By team'], ['bulk', 'One mentor, many teams']] as const).map(([key, label]) => (
                <button
                  key={key}
                  type="button"
                  role="tab"
                  aria-selected={view === key}
                  onClick={() => setView(key)}
                  className={`border-b-2 px-3 py-2 text-xs font-semibold transition ${view === key ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
                >
                  {label}
                  {key === 'teams' && missingTeamCount > 0 && (
                    <span className="ml-1.5 rounded-full bg-amber-100 px-1.5 py-0.5 text-[10px] text-amber-800">{missingTeamCount} need a mentor</span>
                  )}
                </button>
              ))}
            </div>
          </div>

          <div className={`min-h-0 flex-1 overflow-y-auto p-4 sm:p-5 ${refreshing ? 'opacity-70' : ''}`} aria-busy={refreshing}>
            {loading ? (
              <div className="flex items-center justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-primary" /></div>
            ) : loadError && teams.length === 0 ? (
              <div className="flex flex-col items-center gap-3 py-12 text-center">
                <CircleAlert className="h-6 w-6 text-red-500" />
                <p className="text-sm text-red-700">Mentors or teams could not be loaded.</p>
                <button type="button" onClick={() => { setLoading(true); void loadData(false); }} className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50">Retry</button>
              </div>
            ) : view === 'teams' ? (
              <div className="space-y-3">
                {mentors.length === 0 && (
                  <p className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                    No mentor is active in this semester yet. Add mentors to the semester first (Subject Management &gt; Lecturers &amp; Mentors &gt; Add mentors), then assign them here.
                  </p>
                )}
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
                  <div className="relative flex-1">
                    <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                    <input
                      type="search"
                      aria-label="Search teams"
                      placeholder="Search teams..."
                      value={teamSearch}
                      onChange={event => setTeamSearch(event.target.value)}
                      className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-3 text-xs outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                    />
                  </div>
                  <div role="group" aria-label="Team filter" className="inline-flex rounded-lg border border-slate-200 bg-white p-0.5">
                    {([['all', `All teams (${slotRows.length})`], ['missing', `Missing a mentor (${missingTeamCount})`]] as const).map(([key, label]) => (
                      <button
                        key={key}
                        type="button"
                        aria-pressed={slotFilter === key}
                        onClick={() => setSlotFilter(key)}
                        className={`whitespace-nowrap rounded-md px-3 py-1 text-xs font-semibold transition-colors ${slotFilter === key ? 'bg-primary text-white' : 'text-slate-600 hover:text-slate-900'}`}
                      >
                        {label}
                      </button>
                    ))}
                  </div>
                </div>
                {visibleRows.length === 0 ? (
                  <div className="flex min-h-48 flex-col items-center justify-center rounded-xl border border-dashed border-slate-200 bg-slate-50/60 px-6 text-center">
                    <Users className="mb-3 h-8 w-8 text-slate-300" />
                    <p className="text-sm font-semibold text-slate-700">{slotRows.length === 0 ? 'No active teams in this class' : slotFilter === 'missing' && !teamSearch ? 'Every team has both mentors' : 'No matching teams'}</p>
                  </div>
                ) : (
                  <MentorTeamsBoard
                    rows={visibleRows}
                    disabled={busy}
                    onAssign={(team, slot) => setSlotTarget({ team, slot, current: null })}
                    onReplace={(team, slot, assignment) => setSlotTarget({ team, slot, current: assignment })}
                    onEnd={(team, assignment) => { setPendingEnd({ team, assignment }); setEndReason(''); }}
                  />
                )}
              </div>
            ) : (
              <div className="space-y-3">
                {bulkResult && (
                  <div role={bulkResult.tone === 'success' ? 'status' : 'alert'} className={`flex items-start gap-1.5 rounded-xl px-3 py-2.5 text-xs font-semibold ${bulkResult.tone === 'success' ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
                    {bulkResult.tone === 'success' ? <CheckCircle2 className="h-4 w-4 shrink-0" /> : <CircleAlert className="h-4 w-4 shrink-0" />}
                    {bulkResult.message}
                  </div>
                )}
                <div className="grid gap-4 md:grid-cols-2">
                  <section className="min-w-0" aria-label="Select mentor">
                    <div className="mb-2 flex items-center gap-2">
                      <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary text-[10px] font-bold text-white">1</span>
                      <h3 className="text-xs font-semibold text-slate-700">Select mentor</h3>
                    </div>
                    <div className="mb-2 flex flex-wrap gap-1.5" role="group" aria-label="Mentor type">
                      {KIND_CHIPS.map(chip => (
                        <button
                          key={chip.value}
                          type="button"
                          aria-pressed={kindFilter === chip.value}
                          onClick={() => setKindFilter(chip.value)}
                          className={`rounded-full border px-2.5 py-1 text-[11px] font-semibold transition ${kindFilter === chip.value ? 'border-primary bg-primary text-white' : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300'}`}
                        >
                          {chip.label}
                        </button>
                      ))}
                    </div>
                    <div className="relative mb-2">
                      <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                      <input
                        type="search"
                        aria-label="Search mentors"
                        placeholder="Search by name, email or contract..."
                        value={mentorSearch}
                        onChange={event => setMentorSearch(event.target.value)}
                        className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-3 text-xs outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
                      />
                    </div>
                    <div className="h-64 space-y-1.5 overflow-y-auto rounded-xl border border-slate-100 bg-slate-50/50 p-2">
                      {filteredMentors.length === 0 ? (
                        <p className="px-4 py-8 text-center text-xs text-slate-400">
                          {mentors.length === 0
                            ? 'No mentor is active in this semester yet. Add mentors to the semester first (Subject Management > Lecturers & Mentors > Add mentors).'
                            : 'No mentors match this filter.'}
                        </p>
                      ) : filteredMentors.map(mentor => {
                        const isChecked = selectedMentorId === mentor._id;
                        const style = MENTOR_KIND_STYLES[mentor.mentorType];
                        return (
                          <button
                            key={mentor._id}
                            type="button"
                            aria-pressed={isChecked}
                            disabled={submitting}
                            onClick={() => chooseMentor(mentor._id)}
                            className={`flex w-full items-center justify-between rounded-xl border p-2.5 text-left transition-all disabled:opacity-60 ${isChecked ? 'border-primary/30 bg-primary-50/50 shadow-xs' : 'border-slate-200 bg-white hover:border-slate-300'}`}
                          >
                            <div className="flex min-w-0 items-center gap-2.5">
                              <div className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-bold ${style.avatar}`}>
                                {mentor.name?.charAt(0)?.toUpperCase() || 'M'}
                              </div>
                              <div className="min-w-0">
                                <p className="truncate text-xs font-semibold text-slate-800">{mentor.name}</p>
                                <p className="truncate text-[10px] text-slate-400">{style.label}{mentor.contractType ? ` · ${mentor.contractType}` : ''} · {mentor.activeTeamCount} team{mentor.activeTeamCount === 1 ? '' : 's'} this semester</p>
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
                      {selectedMentor && <span className="ml-auto"><MentorKindTag type={selectedMentor.mentorType} /></span>}
                    </div>
                    <div className="relative mb-2">
                      <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                      <input
                        type="search"
                        aria-label="Search teams to assign"
                        placeholder="Search teams..."
                        value={bulkTeamSearch}
                        onChange={event => setBulkTeamSearch(event.target.value)}
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
                        {selectionState === 'all' ? 'Clear selection' : `Select all${bulkTeamSearch ? ' shown' : ''} (${filteredTeams.length})`}
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
                            {bulkTeamSearch ? 'No matching teams found' : `Every active team already has a ${MENTOR_KIND_STYLES[selectedMentor?.mentorType ?? 'Enterprise'].label.toLowerCase()}`}
                          </p>
                          <p className="mt-1 text-[10px] text-slate-400">{bulkTeamSearch ? 'Try a different team name or code.' : 'Use Replace in the By team view to change a mentor.'}</p>
                        </div>
                      ) : filteredTeams.map(team => {
                        const isChecked = chosenTeamIds.includes(team._id);
                        const otherSlot: MentorSlot = selectedMentor?.mentorType === 'Enterprise' ? 'Academic' : 'Enterprise';
                        const otherMentor = (team.currentMentorAssignments || []).find(item => item.slot === otherSlot && item.status.trim().toLowerCase() === 'active');
                        return (
                          <button
                            key={team._id}
                            type="button"
                            role="checkbox"
                            aria-checked={isChecked}
                            disabled={submitting}
                            onClick={() => setSelectedTeamIds(current => toggleSelection(keepEligibleSelection(current, eligibleTeams.map(item => item._id)), team._id))}
                            className={`flex w-full items-center justify-between rounded-xl border p-2.5 text-left transition-all disabled:opacity-60 ${isChecked ? 'border-primary/30 bg-primary-50/50 shadow-xs' : 'border-slate-200 bg-white hover:border-slate-300'}`}
                          >
                            <div className="flex min-w-0 items-center gap-2.5">
                              <Users className="h-4 w-4 shrink-0 text-slate-400" />
                              <div className="min-w-0">
                                <p className="truncate text-xs font-semibold text-slate-800">{team.teamName || 'Unnamed Team'}</p>
                                <p className="truncate text-[10px] text-slate-400">
                                  {team.teamCode || team.groupName || '—'} · {otherMentor ? `${MENTOR_KIND_STYLES[otherSlot].label}: ${otherMentor.mentor.fullName}` : `No ${MENTOR_KIND_STYLES[otherSlot].label.toLowerCase()} yet`}
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

          <div className="flex shrink-0 flex-col gap-2 border-t border-slate-100 bg-white px-5 py-4 sm:flex-row sm:items-center">
            <p className="text-[11px] text-slate-400 sm:flex-1" aria-live="polite">
              {view === 'bulk'
                ? (selectedMentor
                  ? `${chosenTeamIds.length} of ${eligibleTeams.length} team${eligibleTeams.length === 1 ? '' : 's'} selected for ${selectedMentor.name}. Saved all at once or not at all.`
                  : 'Choose a mentor, then tick the teams to assign.')
                : 'Replace ends the current mentor and starts the new one in a single save.'}
            </p>
            <div className="flex gap-3">
              <button type="button" onClick={onClose} disabled={busy} className="rounded-xl border border-slate-200 px-4 py-2 text-xs font-semibold text-slate-500 transition-all hover:bg-slate-50 disabled:opacity-50">
                Close
              </button>
              {view === 'bulk' && (
                <button
                  type="button"
                  onClick={() => void submitBulk()}
                  disabled={busy || loading || !selectedMentorId || chosenTeamIds.length === 0}
                  className="flex min-w-36 items-center justify-center gap-1.5 rounded-xl bg-primary px-4 py-2 text-xs font-semibold text-white shadow-sm transition-all hover:bg-primary-700 disabled:opacity-50"
                >
                  {submitting ? <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Saving...</> : assignLabel}
                </button>
              )}
            </div>
          </div>
        </div>
      </div>

      {slotTarget && (
        <MentorSlotDialog
          team={slotTarget.team}
          slot={slotTarget.slot}
          current={slotTarget.current}
          mentors={mentors}
          onClose={() => setSlotTarget(null)}
          onSaved={refreshAfterChange}
        />
      )}

      <ConfirmDialog
        isOpen={Boolean(pendingEnd)}
        onClose={() => { if (!ending) { setPendingEnd(null); setEndReason(''); } }}
        onConfirm={confirmEnd}
        title="End mentor assignment?"
        description={pendingEnd
          ? `${pendingEnd.assignment.mentor.fullName} will no longer be assigned as the ${MENTOR_KIND_STYLES[pendingEnd.assignment.slot].label.toLowerCase()} for ${pendingEnd.team.teamName}.`
          : ''}
        reason={endReason}
        onReasonChange={setEndReason}
        reasonLabel="Reason for ending assignment"
        reasonRequired
        isSubmitting={ending}
        confirmText="End assignment"
        confirmVariant="danger"
        actionLayout="confirmWide"
      />
    </>
  );
}
