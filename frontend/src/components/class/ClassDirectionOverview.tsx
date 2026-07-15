// @ts-nocheck
import { useEffect, useState } from 'react';
import { CheckCircle2, FileText, Loader2, MessageSquareText, RefreshCw, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../api/classApi';
import { workspaceApi } from '../../api/workspaceApi';
import { getDisplayTeamName } from '../../utils/teamDisplay';

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

export default function ClassDirectionOverview({ semester, year }) {
  const [classes, setClasses] = useState([]);
  const [selectedClassId, setSelectedClassId] = useState('');
  const [overview, setOverview] = useState(null);
  const [loadingClasses, setLoadingClasses] = useState(true);
  const [loadingTeams, setLoadingTeams] = useState(false);
  const [comments, setComments] = useState({});
  const [reviewingTeamId, setReviewingTeamId] = useState('');

  useEffect(() => {
    let active = true;
    const loadClasses = async () => {
      setLoadingClasses(true);
      try {
        const response = await classApi.getAll({ semester, year });
        if (!active) return;
        const list = response?.data?.classes || response?.classes || [];
        setClasses(list);
        setSelectedClassId((current) => list.some((item) => item._id === current) ? current : (list[0]?._id || ''));
      } catch (error) {
        if (active) toast.error(error?.message || 'Failed to load classes');
      } finally {
        if (active) setLoadingClasses(false);
      }
    };
    loadClasses();
    return () => { active = false; };
  }, [semester, year]);

  const loadOverview = async (classId = selectedClassId) => {
    if (!classId) {
      setOverview(null);
      return;
    }
    setLoadingTeams(true);
    try {
      const response = await workspaceApi.getClassProjectDirections(classId);
      setOverview(response?.data || response || null);
    } catch (error) {
      toast.error(error?.response?.data?.error || error?.message || 'Failed to load project directions');
    } finally {
      setLoadingTeams(false);
    }
  };

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadOverview(selectedClassId);
    // loadOverview intentionally follows the selected class.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedClassId]);

  const review = async (teamId, decision) => {
    const comment = (comments[teamId] || '').trim();
    if (comment.length < 3) {
      toast.error('Please enter a review comment');
      return;
    }
    setReviewingTeamId(teamId);
    try {
      await workspaceApi.reviewProjectDirection(teamId, decision, comment);
      toast.success(decision === 'APPROVED' ? 'Project direction approved' : 'Changes requested');
      setComments((current) => ({ ...current, [teamId]: '' }));
      await loadOverview();
    } catch (error) {
      toast.error(error?.response?.data?.error || error?.message || 'Review failed');
    } finally {
      setReviewingTeamId('');
    }
  };

  const teams = overview?.teams || [];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3 border-b border-slate-200 pb-4">
        <div className="min-w-[260px] flex-1">
          <label className="mb-1.5 block text-xs font-semibold uppercase text-slate-500">Class</label>
          <select
            value={selectedClassId}
            onChange={(event) => setSelectedClassId(event.target.value)}
            disabled={loadingClasses}
            className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/15"
          >
            {classes.length === 0 && <option value="">No classes in this semester</option>}
            {classes.map((cls) => (
              <option key={cls._id} value={cls._id}>{cls.classCode}</option>
            ))}
          </select>
        </div>
        <button
          type="button"
          onClick={() => loadOverview()}
          disabled={!selectedClassId || loadingTeams}
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
          <p className="mt-2 text-sm text-slate-500">This class has no teams yet.</p>
        </div>
      ) : (
        <div className="divide-y divide-slate-200 border-y border-slate-200">
          {teams.map((team) => {
            const status = team.projectDirectionStatus || 'NOT_SUBMITTED';
            const hasDirection = Boolean(team.projectDirection);
            const busy = reviewingTeamId === team._id;
            return (
              <section key={team._id} className="py-5 first:pt-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h3 className="font-bold text-slate-900">{getDisplayTeamName(team) || team.teamCode}</h3>
                    <p className="mt-0.5 text-xs text-slate-500">
                      {team.teamCode} · Leader: {team.leaderId?.fullName || 'Not assigned'}
                    </p>
                  </div>
                  <span className={`rounded-full border px-2.5 py-1 text-xs font-semibold ${statusStyles[status]}`}>
                    {statusLabels[status] || status}
                  </span>
                </div>

                {hasDirection ? (
                  <>
                    <p className="mt-4 whitespace-pre-wrap text-sm leading-7 text-slate-700">{team.projectDirection}</p>
                    {team.projectDirectionReviewComment && (
                      <div className="mt-3 border-l-2 border-blue-300 pl-3">
                        <p className="text-xs font-semibold text-blue-700">Previous lecturer comment</p>
                        <p className="mt-1 text-sm text-slate-700">{team.projectDirectionReviewComment}</p>
                      </div>
                    )}
                    <div className="mt-4">
                      <label className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-slate-600">
                        <MessageSquareText className="h-3.5 w-3.5" /> Review comment
                      </label>
                      <textarea
                        value={comments[team._id] || ''}
                        onChange={(event) => setComments((current) => ({ ...current, [team._id]: event.target.value }))}
                        rows={3}
                        maxLength={1000}
                        placeholder="Explain what is approved or what the team should revise..."
                        className="w-full resize-y rounded-lg border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/15"
                      />
                      <div className="mt-2 flex flex-wrap justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => review(team._id, 'CHANGES_REQUESTED')}
                          disabled={busy}
                          className="inline-flex items-center gap-2 rounded-lg border border-red-200 px-3 py-2 text-sm font-semibold text-red-600 transition hover:bg-red-50 disabled:opacity-50"
                        >
                          <XCircle className="h-4 w-4" /> Request changes
                        </button>
                        <button
                          type="button"
                          onClick={() => review(team._id, 'APPROVED')}
                          disabled={busy}
                          className="inline-flex items-center gap-2 rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:opacity-50"
                        >
                          {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />}
                          Approve
                        </button>
                      </div>
                    </div>
                  </>
                ) : (
                  <p className="mt-4 border-l-2 border-slate-200 pl-3 text-sm text-slate-500">
                    The team leader has not submitted a project direction.
                  </p>
                )}
              </section>
            );
          })}
        </div>
      )}
    </div>
  );
}
