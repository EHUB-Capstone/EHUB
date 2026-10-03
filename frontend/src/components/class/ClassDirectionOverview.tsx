import { useCallback, useEffect, useRef, useState } from 'react';
import { Ban, CheckCircle2, ChevronDown, FileText, Loader2, MessageSquareText, RefreshCw, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../api/classApi';
import { toClassViewModel, unwrapApiData } from '../../utils/classMappers';
import { teamApi } from '../../api/teamApi';
import { normalizeManagedTeam } from '../../utils/teamManagement';
import { parseApiError } from '../../utils/apiError';
import { getDisplayTeamName } from '../../utils/teamDisplay';
import {
  directionOverviewTargetClassIds,
  resolveDirectionOverviewClassId,
  updateProjectDirectionOverviewTeams,
} from '../../utils/projectDirectionSync';
import type { ProjectDirectionSyncValue } from '../../utils/projectDirectionSync';
import { subscribeProjectDirectionRealtime } from '../../api/projectDirectionRealtime';
import ConfirmDialog from '../ui/ConfirmDialog';

const statusStyles = {
  PENDING: 'bg-amber-50 text-amber-700 border-amber-200',
  APPROVED: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  CHANGES_REQUESTED: 'bg-red-50 text-red-700 border-red-200',
  NOT_SUBMITTED: 'bg-slate-50 text-slate-500 border-slate-200',
};

const statusLabels = {
  PENDING: 'Pending review',
  APPROVED: 'Approved',
  CHANGES_REQUESTED: 'Changes requested',
  NOT_SUBMITTED: 'Not submitted',
};

export default function ClassDirectionOverview({ semester, year, initialClassId = '', focusTeamId = '', onSelectedClassChange }) {
  const initialClassIdRef = useRef(initialClassId);
  const [classes, setClasses] = useState([]);
  const [selectedClassId, setSelectedClassId] = useState('');
  const [overview, setOverview] = useState(null);
  const [loadingClasses, setLoadingClasses] = useState(true);
  const [loadingTeams, setLoadingTeams] = useState(false);
  const [comments, setComments] = useState({});
  const [reviewingTeamId, setReviewingTeamId] = useState('');
  const [rejectTarget, setRejectTarget] = useState<{ teamId: string; projectName: string } | null>(null);
  const [expandedApprovedTeamIds, setExpandedApprovedTeamIds] = useState(() => new Set<string>());
  const overviewRef = useRef(null);
  const overviewRequestIdRef = useRef(0);
  const reviewInFlightRef = useRef(false);
  const pendingRealtimeDirectionsRef = useRef(new Map<string, ProjectDirectionSyncValue>());

  useEffect(() => {
    initialClassIdRef.current = initialClassId;
    // The URL deep-link is an external navigation source that must update the selected class.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSelectedClassId(initialClassId);
  }, [initialClassId]);

  useEffect(() => {
    let active = true;
    const loadClasses = async () => {
      setLoadingClasses(true);
      try {
        const response = await classApi.getAll({ semesterCode: semester && year ? `${semester}${year}` : undefined, year, pageSize: 100 });
        if (!active) return;
        const payload = unwrapApiData(response);
        const list = (payload?.items || []).map(toClassViewModel);
        setClasses(list);
        setSelectedClassId((current) => {
          return resolveDirectionOverviewClassId(
            list.map((item) => item._id),
            initialClassIdRef.current,
            current,
          );
        });
      } catch (error) {
        if (active) toast.error(error?.message || 'Failed to load classes');
      } finally {
        if (active) setLoadingClasses(false);
      }
    };
    loadClasses();
    return () => { active = false; };
  }, [semester, year]);

  const loadOverview = useCallback(async (classId: string) => {
    const requestId = ++overviewRequestIdRef.current;
    setLoadingTeams(true);
    try {
      const targetClassIds = directionOverviewTargetClassIds(
        classes.map(item => item._id),
        classId,
      );
      const targetClasses = classes.filter(item => targetClassIds.includes(item._id));
      const teamsByClass = await Promise.all(targetClasses.map(async classItem => {
        const response = await classApi.getTeams(classItem._id);
        const teamData = unwrapApiData(response);
        return (Array.isArray(teamData) ? teamData : []).map(team => ({
          ...normalizeManagedTeam(team),
          overviewClassId: classItem._id,
          overviewClassCode: classItem.classCode,
        }));
      }));
      const teams = teamsByClass.flat();
      const directionTeams = await Promise.all(teams.map(async team => {
        let direction = null;
        try {
          direction = unwrapApiData(await teamApi.getProjectDirection(team._id));
        } catch (error) {
          if (parseApiError(error, '').code !== 'PROJECT_DIRECTION_NOT_FOUND') throw error;
        }
        const status = direction?.status === 'Submitted' ? 'PENDING'
          : direction?.status === 'Approved' ? 'APPROVED'
            : direction?.status === 'NeedsRevision' ? 'CHANGES_REQUESTED'
              : 'NOT_SUBMITTED';
        const leader = team.members?.find(member => member.roleInTeam?.toUpperCase() === 'LEADER');
        return {
          ...team,
          leaderId: leader?.studentId || team.leaderId,
          leaderName: typeof leader?.studentId === 'object' ? leader.studentId.fullName : 'Not assigned',
          projectDirection: direction?.summary || '',
          projectDirectionTitle: direction?.title || '',
          projectDirectionIsProfileChangeProposal: Boolean(direction?.isProjectProfileChangeProposal),
          projectDirectionCurrentTitle: direction?.currentTitle || '',
          projectDirectionCurrentSummary: direction?.currentSummary || '',
          projectDirectionStartupIndustries: direction?.startupIndustries || [],
          projectDirectionStatus: status,
          projectDirectionReviewComment: direction?.reviews?.[0]?.comment || null,
          projectDirectionRowVersion: direction?.rowVersion || '',
        };
      }));
      if (requestId !== overviewRequestIdRef.current) return;

      const mergedTeams = directionTeams.map((team) => {
        const pendingDirection = pendingRealtimeDirectionsRef.current.get(team._id);
        if (!pendingDirection || team.projectDirectionStatus === 'APPROVED') return team;
        const loadedVersion = Number(team.projectDirectionRowVersion);
        const pendingVersion = Number(pendingDirection.rowVersion);
        if (Number.isFinite(loadedVersion) && Number.isFinite(pendingVersion) && pendingVersion < loadedVersion)
          return team;
        return updateProjectDirectionOverviewTeams([team], team._id, pendingDirection)[0];
      });
      pendingRealtimeDirectionsRef.current.clear();
      const nextOverview = { teams: mergedTeams };
      overviewRef.current = nextOverview;
      setOverview(nextOverview);
    } catch (error) {
      toast.error(error?.response?.data?.error || error?.message || 'Failed to load project directions');
    } finally {
      if (requestId === overviewRequestIdRef.current) setLoadingTeams(false);
    }
  }, [classes]);

  useEffect(() => {
    if (loadingClasses) return;
    pendingRealtimeDirectionsRef.current.clear();
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadOverview(selectedClassId);
  }, [loadOverview, loadingClasses, selectedClassId]);

  useEffect(() => {
    let active = true;
    const visibleClassIds = new Set(directionOverviewTargetClassIds(
      classes.map(item => item._id),
      selectedClassId,
    ));
    const synchronizeAfterReconnect = async () => {
      const currentTeams = overviewRef.current?.teams || [];
      if (!active || reviewInFlightRef.current || currentTeams.length === 0) return;
      try {
        const latestDirections = await Promise.all(currentTeams.map(async (team) => {
          try {
            return unwrapApiData<ProjectDirectionSyncValue>(await teamApi.getProjectDirection(team._id));
          } catch {
            return null;
          }
        }));
        if (!active || reviewInFlightRef.current) return;
        let nextTeams = overviewRef.current?.teams || [];
        latestDirections.forEach((direction, index) => {
          if (direction) nextTeams = updateProjectDirectionOverviewTeams(nextTeams, currentTeams[index]._id, direction);
        });
        const nextOverview = { ...overviewRef.current, teams: nextTeams };
        overviewRef.current = nextOverview;
        setOverview(nextOverview);
      } catch {
        // A later WebSocket event or reconnect will retry synchronization.
      }
    };
    const unsubscribe = subscribeProjectDirectionRealtime(
      (event) => {
        if (!active || reviewInFlightRef.current || event.eventType !== 'ProjectDirectionSubmitted' || !visibleClassIds.has(event.classId)) return;
        const previousTeam = (overviewRef.current?.teams || []).find((team) => team._id === event.teamId);
        if (!previousTeam) {
          pendingRealtimeDirectionsRef.current.set(event.teamId, event.direction);
          return;
        }
        if (previousTeam.projectDirectionStatus === 'PENDING' || previousTeam.projectDirectionStatus === 'APPROVED') return;
        const nextOverview = {
          ...overviewRef.current,
          teams: updateProjectDirectionOverviewTeams(overviewRef.current?.teams || [], event.teamId, event.direction),
        };
        overviewRef.current = nextOverview;
        setOverview(nextOverview);
        const teamName = getDisplayTeamName(previousTeam) || previousTeam.teamCode || 'A team';
        toast(`${teamName} submitted a project direction for review.`, { id: `project-direction-submitted-${event.direction.rowVersion}` });
      },
      () => void synchronizeAfterReconnect(),
    );
    return () => {
      active = false;
      unsubscribe();
    };
  }, [classes, selectedClassId]);

  const review = async (teamId, decision) => {
    const comment = (comments[teamId] || '').trim();
    if (comment.length > 0 && comment.length < 3) {
      toast.error('Review comment must be at least 3 characters when provided');
      return false;
    }
    reviewInFlightRef.current = true;
    overviewRequestIdRef.current += 1;
    setReviewingTeamId(teamId);
    try {
      const team = (overview?.teams || []).find(item => item._id === teamId);
      const apiDecision = decision === 'APPROVED'
        ? 'Approved'
        : decision === 'REJECTED' ? 'Rejected' : 'NeedsRevision';
      const response = await teamApi.reviewProjectDirection(teamId, {
        decision: apiDecision,
        comment,
        rowVersion: team?.projectDirectionRowVersion,
      });
      const reviewedDirection = unwrapApiData(response);
      setOverview((current) => {
        if (!current) return current;
        const nextOverview = {
          ...current,
          teams: updateProjectDirectionOverviewTeams(current.teams || [], teamId, reviewedDirection),
        };
        overviewRef.current = nextOverview;
        return nextOverview;
      });
      toast.success(decision === 'APPROVED'
        ? 'Project direction approved'
        : decision === 'REJECTED' ? 'Project Profile change rejected' : 'Changes requested');
      setComments((current) => ({ ...current, [teamId]: '' }));
      if (decision === 'APPROVED' || decision === 'REJECTED') {
        setExpandedApprovedTeamIds((current) => {
          const next = new Set(current);
          next.delete(teamId);
          return next;
        });
      }
      return true;
    } catch (error) {
      toast.error(error?.response?.data?.error || error?.message || 'Review failed');
      return false;
    } finally {
      reviewInFlightRef.current = false;
      setReviewingTeamId('');
    }
  };

  const teams = overview?.teams || [];
  const displayedTeams = focusTeamId
    ? [...teams].sort((left, right) => Number(right._id === focusTeamId) - Number(left._id === focusTeamId))
    : teams;
  const toggleApprovedTeam = (teamId: string) => {
    setExpandedApprovedTeamIds((current) => {
      const next = new Set(current);
      if (next.has(teamId)) next.delete(teamId);
      else next.add(teamId);
      return next;
    });
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3 border-b border-slate-200 pb-4">
        <div className="min-w-[260px] flex-1">
          <label className="mb-1.5 block text-xs font-semibold uppercase text-slate-500">Class</label>
          <select
            value={selectedClassId}
            onChange={(event) => {
              setSelectedClassId(event.target.value);
              onSelectedClassChange?.(event.target.value);
            }}
            disabled={loadingClasses}
            className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/15"
          >
            <option value="">{classes.length === 0 ? 'No classes in this semester' : 'All Classes'}</option>
            {classes.map((cls) => (
              <option key={cls._id} value={cls._id}>{cls.classCode}</option>
            ))}
          </select>
        </div>
        <button
          type="button"
          onClick={() => loadOverview(selectedClassId)}
          disabled={classes.length === 0 || loadingTeams || Boolean(reviewingTeamId)}
          title="Refresh overview"
          className="inline-flex h-10 w-10 items-center justify-center rounded-lg border border-slate-200 text-slate-600 transition hover:bg-slate-50 disabled:opacity-50"
        >
          <RefreshCw className={`h-4 w-4 ${loadingTeams ? 'animate-spin' : ''}`} />
        </button>
      </div>

      {loadingTeams ? (
        <div className="flex justify-center py-12"><Loader2 className="h-7 w-7 animate-spin text-primary" /></div>
      ) : teams.length === 0 ? (
        <div className="border-y border-slate-200 py-12 text-center">
          <FileText className="mx-auto h-8 w-8 text-slate-300" />
          <p className="mt-2 text-sm text-slate-500">{selectedClassId ? 'This class has no teams yet.' : 'The assigned classes have no teams yet.'}</p>
        </div>
      ) : (
        <div className="divide-y divide-slate-200 border-y border-slate-200">
          {displayedTeams.map((team) => {
            const status = team.projectDirectionStatus || 'NOT_SUBMITTED';
            const hasDirection = Boolean(team.projectDirection);
            const isProfileChangeProposal = Boolean(team.projectDirectionIsProfileChangeProposal);
            const startupIndustries = Array.isArray(team.projectDirectionStartupIndustries)
              ? team.projectDirectionStartupIndustries
              : [];
            const busy = reviewingTeamId === team._id;
            const isApproved = status === 'APPROVED';
            const approvedExpanded = !isApproved || expandedApprovedTeamIds.has(team._id) || team._id === focusTeamId;
            return (
              <section key={team._id} className={`${isApproved && !approvedExpanded ? 'py-3' : 'py-5 first:pt-4'} ${team._id === focusTeamId ? 'rounded-xl bg-primary-50/40 px-4 ring-2 ring-primary/15' : ''}`}>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h3 className="font-bold text-slate-900">
                      {team.projectDirectionTitle || team.projectName || getDisplayTeamName(team) || team.teamCode}
                    </h3>
                    <p className="mt-0.5 text-xs text-slate-500">
                      Class: {team.overviewClassCode || '—'} · Team: {team.teamName || 'Unnamed team'} · Leader: {team.leaderName}
                    </p>
                    {isProfileChangeProposal && <span className="mt-2 inline-flex rounded-full border border-violet-200 bg-violet-50 px-2.5 py-1 text-xs font-bold text-violet-700">Project Profile change request</span>}
                  </div>
                  <div className="flex items-center gap-2">
                    <span className={`rounded-full border px-2.5 py-1 text-xs font-semibold ${statusStyles[status]}`}>
                      {statusLabels[status] || status}
                    </span>
                    {isApproved && (
                      <button
                        type="button"
                        onClick={() => toggleApprovedTeam(team._id)}
                        aria-expanded={approvedExpanded}
                        aria-label={`${approvedExpanded ? 'Collapse' : 'Expand'} approved proposal for ${team.teamName || team.projectDirectionTitle || 'team'}`}
                        className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-semibold text-slate-600 transition hover:border-emerald-300 hover:text-emerald-700"
                      >
                        {approvedExpanded ? 'Collapse' : 'Expand'}
                        <ChevronDown className={`h-3.5 w-3.5 transition-transform ${approvedExpanded ? 'rotate-180' : ''}`} />
                      </button>
                    )}
                  </div>
                </div>

                {approvedExpanded && (hasDirection ? (
                  <>
                    {isProfileChangeProposal ? (
                      <div className="mt-4 grid gap-3 lg:grid-cols-2">
                        <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                          <p className="text-xs font-bold uppercase tracking-wide text-slate-500">Current approved profile</p>
                          <p className="mt-3 text-xs font-semibold text-slate-500">Project name</p>
                          <p className="mt-1 font-semibold text-slate-900">{team.projectDirectionCurrentTitle}</p>
                          <p className="mt-3 text-xs font-semibold text-slate-500">Description</p>
                          <p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-700">{team.projectDirectionCurrentSummary}</p>
                        </div>
                        <div className="rounded-xl border border-violet-200 bg-violet-50/50 p-4">
                          <p className="text-xs font-bold uppercase tracking-wide text-violet-700">Proposed change</p>
                          <p className="mt-3 text-xs font-semibold text-violet-600">Project name</p>
                          <p className="mt-1 font-semibold text-slate-900">{team.projectDirectionTitle}</p>
                          <p className="mt-3 text-xs font-semibold text-violet-600">Description</p>
                          <p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-700">{team.projectDirection}</p>
                        </div>
                      </div>
                    ) : (
                      <p className="mt-4 whitespace-pre-wrap text-sm leading-7 text-slate-700">{team.projectDirection}</p>
                    )}
                    {startupIndustries.length > 0 && (
                      <div className="mt-4">
                        <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Startup Industry</p>
                        <div className="mt-2 flex flex-wrap gap-2">
                          {startupIndustries.map((industry) => (
                            <span
                              key={industry}
                              className="rounded-full border border-orange-200 bg-orange-50 px-2.5 py-1 text-xs font-medium text-orange-700"
                            >
                              {industry}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}
                    {team.projectDirectionReviewComment && (
                      <div className="mt-3 border-l-2 border-blue-300 pl-3">
                        <p className="text-xs font-semibold text-blue-700">Previous lecturer comment</p>
                        <p className="mt-1 text-sm text-slate-700">{team.projectDirectionReviewComment}</p>
                      </div>
                    )}
                    <div className="mt-4">
                      <label className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-slate-600">
                        <MessageSquareText className="h-3.5 w-3.5" /> Review comment <span className="font-normal text-slate-400">(optional)</span>
                      </label>
                      <textarea
                        value={comments[team._id] || ''}
                        onChange={(event) => setComments((current) => ({ ...current, [team._id]: event.target.value }))}
                        rows={3}
                        maxLength={1000}
                        placeholder="Optional: explain what is approved or what the team should revise..."
                        className="w-full resize-y rounded-lg border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/15"
                      />
                      <div className="mt-2 flex flex-wrap justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => review(team._id, 'CHANGES_REQUESTED')}
                          disabled={busy || status !== 'PENDING'}
                          className="inline-flex items-center gap-2 rounded-lg border border-red-200 px-3 py-2 text-sm font-semibold text-red-600 transition hover:bg-red-50 disabled:opacity-50"
                        >
                          <XCircle className="h-4 w-4" /> Request changes
                        </button>
                        <button
                          type="button"
                          onClick={() => review(team._id, 'APPROVED')}
                          disabled={busy || status !== 'PENDING'}
                          className="inline-flex items-center gap-2 rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:opacity-50"
                        >
                          {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />}
                          Approve
                        </button>
                        {isProfileChangeProposal && (
                          <button
                            type="button"
                            onClick={() => setRejectTarget({
                              teamId: team._id,
                              projectName: team.projectDirectionTitle || team.projectName || 'this Project Profile change',
                            })}
                            disabled={busy || status !== 'PENDING'}
                            className="inline-flex items-center gap-2 rounded-lg bg-red-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-red-700 disabled:opacity-50"
                          >
                            <Ban className="h-4 w-4" /> Reject
                          </button>
                        )}
                      </div>
                    </div>
                  </>
                ) : (
                  <p className="mt-4 border-l-2 border-slate-200 pl-3 text-sm text-slate-500">
                    The team leader has not submitted a project direction.
                  </p>
                ))}
              </section>
            );
          })}
        </div>
      )}
      <ConfirmDialog
        isOpen={Boolean(rejectTarget)}
        onClose={() => {
          if (!reviewingTeamId) setRejectTarget(null);
        }}
        onConfirm={async () => {
          if (!rejectTarget) return;
          const succeeded = await review(rejectTarget.teamId, 'REJECTED');
          if (succeeded) setRejectTarget(null);
        }}
        title="Reject Project Profile change?"
        description={`Reject the proposed changes for “${rejectTarget?.projectName || ''}”? The previously approved Project Profile will remain unchanged.`}
        confirmText="Reject change"
        confirmVariant="danger"
        isSubmitting={Boolean(rejectTarget && reviewingTeamId === rejectTarget.teamId)}
      />
    </div>
  );
}
