import { useCallback, useEffect, useState } from 'react';
import { ChevronDown, ChevronRight, Crown, Loader2, RefreshCw } from 'lucide-react';
import { teamLineageApi } from '../../api/teamLineageApi';
import type { TeamLineage, TeamLineageSubmission, TeamLineageTerm } from '../../types/teamLineage';
import { parseApiError } from '../../utils/apiError';

interface TeamLineageHistoryProps {
  teamId: string;
}

type SubmissionState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; items: TeamLineageSubmission[] };

function TermCard({ teamId, term }: { teamId: string; term: TeamLineageTerm }) {
  const [open, setOpen] = useState(false);
  const [submissions, setSubmissions] = useState<SubmissionState | null>(null);

  const loadSubmissions = useCallback(async (signal?: AbortSignal) => {
    setSubmissions({ status: 'loading' });
    try {
      const response = await teamLineageApi.getTermSubmissions(teamId, term.teamId, { signal });
      setSubmissions({ status: 'ready', items: response.data ?? [] });
    } catch (error: unknown) {
      if ((error as { code?: string })?.code === 'ERR_CANCELED') return;
      setSubmissions({ status: 'error', message: parseApiError(error, 'Unable to load submissions.').message });
    }
  }, [teamId, term.teamId]);

  useEffect(() => {
    if (!open || !term.canViewSubmissions || submissions) return undefined;
    const controller = new AbortController();
    void loadSubmissions(controller.signal);
    return () => controller.abort();
  }, [open, term.canViewSubmissions, submissions, loadSubmissions]);

  return (
    <li className="rounded-xl border border-slate-200 bg-white">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left"
      >
        <span className="min-w-0">
          <span className="flex flex-wrap items-center gap-2">
            <span className="font-semibold text-slate-900">{term.semesterCode}</span>
            <span className="text-xs text-slate-500">{term.classCode}</span>
            {term.isCurrent && (
              <span className="rounded-full bg-green-100 px-2 py-0.5 text-[10px] font-bold uppercase text-green-700">Current</span>
            )}
          </span>
          <span className="mt-0.5 block truncate text-xs text-slate-500">
            {term.teamName}
            {term.projectName ? ` · ${term.projectName}` : ''}
            {term.projectStatus ? ` (${term.projectStatus})` : ''}
          </span>
        </span>
        {open ? <ChevronDown className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" /> : <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />}
      </button>

      {open && (
        <div className="space-y-4 border-t border-slate-100 px-4 py-3 text-sm">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Members</p>
            <ul className="mt-1.5 flex flex-wrap gap-2">
              {term.members.map((member) => (
                <li key={member.studentId} className="flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1 text-xs text-slate-700">
                  {member.isLeader && <Crown className="h-3 w-3 text-amber-500" aria-label="Leader" />}
                  {member.fullName}
                </li>
              ))}
            </ul>
          </div>

          {term.canViewSubmissions ? (
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Submissions</p>
              {submissions?.status === 'loading' && (
                <p className="mt-2 flex items-center gap-2 text-xs text-slate-500"><Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" /> Loading submissions…</p>
              )}
              {submissions?.status === 'error' && (
                <div className="mt-2 flex items-center gap-3 text-xs text-red-600" role="alert">
                  <span>{submissions.message}</span>
                  <button type="button" onClick={() => void loadSubmissions()} className="flex items-center gap-1 font-semibold underline">
                    <RefreshCw className="h-3 w-3" aria-hidden="true" /> Retry
                  </button>
                </div>
              )}
              {submissions?.status === 'ready' && submissions.items.length === 0 && (
                <p className="mt-2 text-xs text-slate-500">No submissions in this semester.</p>
              )}
              {submissions?.status === 'ready' && submissions.items.length > 0 && (
                <ul className="mt-2 divide-y divide-slate-100 rounded-lg border border-slate-100">
                  {submissions.items.map((item) => (
                    <li key={item.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-xs">
                      <span className="min-w-0">
                        <span className="font-semibold text-slate-800">{item.checkpointName}</span>
                        <span className="text-slate-500"> · {item.title} (v{item.versionNumber}, {item.status})</span>
                      </span>
                      {item.evaluations === null ? (
                        <span className="text-slate-400">Scores hidden</span>
                      ) : item.evaluations.length === 0 ? (
                        <span className="text-slate-400">Not scored</span>
                      ) : (
                        <span className="font-semibold text-slate-700">
                          {item.evaluations.map((evaluation) => `${evaluation.totalScore}/${evaluation.maxTotalScore}`).join(', ')}
                        </span>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          ) : (
            <p className="text-xs text-slate-500">You cannot view submissions of this semester.</p>
          )}
        </div>
      )}
    </li>
  );
}

export default function TeamLineageHistory({ teamId }: TeamLineageHistoryProps) {
  const [lineage, setLineage] = useState<TeamLineage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      const response = await teamLineageApi.getLineage(teamId, { signal });
      setLineage(response.data ?? null);
    } catch (caught: unknown) {
      if ((caught as { code?: string })?.code === 'ERR_CANCELED') return;
      setError(parseApiError(caught, 'Unable to load the team history.').message);
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  if (loading) {
    return <p className="flex items-center gap-2 py-8 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading team history…</p>;
  }

  if (error) {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700" role="alert">
        <p>{error}</p>
        <button type="button" onClick={() => void load()} className="mt-2 flex items-center gap-1 font-semibold underline">
          <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" /> Retry
        </button>
      </div>
    );
  }

  if (!lineage || lineage.terms.length === 0) {
    return <p className="py-8 text-sm text-slate-500">No semester history is available for this team.</p>;
  }

  return (
    <ol className="space-y-3" aria-label="Team history by semester">
      {[...lineage.terms].reverse().map((term) => (
        <TermCard key={term.teamId} teamId={teamId} term={term} />
      ))}
    </ol>
  );
}
