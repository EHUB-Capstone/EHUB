import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ChevronDown, ChevronRight, ClipboardCheck, MessageSquare, Search } from 'lucide-react';
import { teamFeedbackOverviewApi } from '../../api/teamFeedbackOverviewApi';
import type {
  TeamFeedbackAuthor,
  TeamFeedbackCheckpoint,
  TeamFeedbackEvaluation,
  TeamFeedbackFilters,
  TeamFeedbackOverview as TeamFeedbackOverviewData,
  TeamFeedbackSource,
  TeamFeedbackThread,
} from '../../types/teamFeedbackOverview';
import { parseApiError } from '../../utils/apiError';
import {
  countFeedbackItems,
  defaultTeamFeedbackFilters,
  filterFeedbackCheckpoints,
} from '../../utils/teamFeedbackOverview';
import Badge from '../ui/Badge';
import Button from '../ui/Button';
import Card from '../ui/Card';
import EmptyState from '../ui/EmptyState';
import ErrorState from '../ui/ErrorState';
import LoadingSkeleton from '../ui/LoadingSkeleton';

interface TeamFeedbackOverviewProps {
  teamId: string;
  /** Called when the user wants to jump to the checkpoint section of the workspace. */
  onOpenCheckpoints?: () => void;
}

type LoadState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: TeamFeedbackOverviewData };

const roleTones: Record<string, string> = {
  Admin: 'navy',
  Lecturer: 'brand',
  Mentor: 'info',
  Student: 'neutral',
};

const evaluationTones: Record<string, string> = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  Published: 'Published',
  Locked: 'neutral',
};

const formatDate = (value: string | null) => {
  if (!value) return '';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString();
};

function AuthorLine({ author, date }: { author: TeamFeedbackAuthor; date: string | null }) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="text-body-sm font-semibold text-slate-900">{author.name || 'Unknown user'}</span>
      {author.role && <Badge variant={roleTones[author.role] ?? 'neutral'}>{author.role}</Badge>}
      {date && <span className="text-caption text-slate-500">{formatDate(date)}</span>}
    </div>
  );
}

function ThreadItem({ thread }: { thread: TeamFeedbackThread }) {
  const content = thread.isTechnicalReview ? thread.content.replace(/^TECHNICAL REVIEW\n/, '') : thread.content;
  return (
    <li>
      <Card tone="inset" hover={false} padding="p-4" className="space-y-2">
        <AuthorLine author={thread.author} date={thread.createdAt} />
        <div className="flex flex-wrap gap-2">
          {thread.isTechnicalReview && <Badge variant="info">Technical review</Badge>}
          {thread.resolved && <Badge variant="success">Resolved</Badge>}
          <Badge variant="neutral">Submission v{thread.submissionVersion}</Badge>
        </div>
        <p className="whitespace-pre-wrap text-body-sm text-slate-700">{content}</p>
        {thread.replies.length > 0 && (
          <ul className="mt-2 space-y-2 border-l-2 border-slate-200 pl-3" aria-label="Replies">
            {thread.replies.map((reply) => (
              <li key={reply.id} className="space-y-1">
                <AuthorLine author={reply.author} date={reply.createdAt} />
                <p className="whitespace-pre-wrap text-body-sm text-slate-700">{reply.content}</p>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </li>
  );
}

function EvaluationField({ label, value }: { label: string; value: string | null }) {
  if (!value?.trim()) return null;
  return (
    <div>
      <p className="text-overline uppercase text-slate-500">{label}</p>
      <p className="whitespace-pre-wrap text-body-sm text-slate-700">{value}</p>
    </div>
  );
}

function EvaluationItem({ evaluation }: { evaluation: TeamFeedbackEvaluation }) {
  return (
    <li>
      <Card tone="inset" hover={false} padding="p-4" className="space-y-3">
        <AuthorLine author={evaluation.evaluator} date={evaluation.publishedAt ?? evaluation.submittedAt} />
        <Badge variant={evaluationTones[evaluation.status] ?? 'neutral'}>{evaluation.status}</Badge>
        <EvaluationField label="Overall feedback" value={evaluation.overallFeedback} />
        <EvaluationField label="Strengths" value={evaluation.strengths} />
        <EvaluationField label="Weaknesses" value={evaluation.weaknesses} />
        <EvaluationField label="Suggestions" value={evaluation.suggestions} />
        {evaluation.criterionComments.length > 0 && (
          <div>
            <p className="text-overline uppercase text-slate-500">Criteria comments</p>
            <ul className="mt-1 space-y-1">
              {evaluation.criterionComments.map((item) => (
                <li key={item.criterionName} className="text-body-sm text-slate-700">
                  <span className="font-semibold text-slate-900">{item.criterionName}: </span>
                  <span className="whitespace-pre-wrap">{item.comment}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </Card>
    </li>
  );
}

function CheckpointBlock({
  checkpoint,
  open,
  onToggle,
  onOpenCheckpoints,
}: {
  checkpoint: TeamFeedbackCheckpoint;
  open: boolean;
  onToggle: () => void;
  onOpenCheckpoints?: () => void;
}) {
  const total = checkpoint.threads.length + checkpoint.evaluations.length;
  return (
    <li>
      <Card hover={false} padding="p-0">
        <button
          type="button"
          onClick={onToggle}
          aria-expanded={open}
          className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left"
        >
          <span className="min-w-0">
            <span className="block text-subheading text-secondary">
              Checkpoint {checkpoint.checkpointNumber}
              {checkpoint.title ? ` · ${checkpoint.title}` : ''}
            </span>
            <span className="text-caption text-slate-500">
              {total === 0 ? 'No feedback yet' : `${checkpoint.threads.length} comments · ${checkpoint.evaluations.length} evaluations`}
            </span>
          </span>
          {open
            ? <ChevronDown className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
            : <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />}
        </button>
        {open && (
          <div className="space-y-4 border-t border-slate-100 px-4 py-4">
            {total === 0 && <p className="text-body-sm text-slate-500">No feedback has been given for this checkpoint.</p>}
            {checkpoint.evaluations.length > 0 && (
              <section aria-label={`Evaluations of checkpoint ${checkpoint.checkpointNumber}`}>
                <h4 className="mb-2 flex items-center gap-1.5 text-body-sm font-semibold text-slate-700">
                  <ClipboardCheck className="h-4 w-4" aria-hidden="true" /> Evaluations
                </h4>
                <ul className="space-y-2">
                  {checkpoint.evaluations.map((evaluation) => <EvaluationItem key={evaluation.id} evaluation={evaluation} />)}
                </ul>
              </section>
            )}
            {checkpoint.threads.length > 0 && (
              <section aria-label={`Comments of checkpoint ${checkpoint.checkpointNumber}`}>
                <h4 className="mb-2 flex items-center gap-1.5 text-body-sm font-semibold text-slate-700">
                  <MessageSquare className="h-4 w-4" aria-hidden="true" /> Comments
                </h4>
                <ul className="space-y-2">
                  {checkpoint.threads.map((thread) => <ThreadItem key={thread.id} thread={thread} />)}
                </ul>
              </section>
            )}
            {onOpenCheckpoints && (
              <Button variant="outline" size="sm" onClick={onOpenCheckpoints}>Open workspace checkpoints</Button>
            )}
          </div>
        )}
      </Card>
    </li>
  );
}

export default function TeamFeedbackOverview({ teamId, onOpenCheckpoints }: TeamFeedbackOverviewProps) {
  const [state, setState] = useState<LoadState>({ status: 'loading' });
  const [filters, setFilters] = useState<TeamFeedbackFilters>(defaultTeamFeedbackFilters);
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set());
  const controllerRef = useRef<AbortController | null>(null);

  const load = useCallback(async () => {
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    setState({ status: 'loading' });
    try {
      const response = await teamFeedbackOverviewApi.getOverview(teamId, { signal: controller.signal });
      if (controller.signal.aborted) return;
      setState({ status: 'ready', data: response.data });
    } catch (error: unknown) {
      if (controller.signal.aborted || (error as { code?: string })?.code === 'ERR_CANCELED') return;
      setState({ status: 'error', message: parseApiError(error, 'Unable to load the feedback overview.').message });
    }
  }, [teamId]);

  useEffect(() => {
    void load();
    return () => controllerRef.current?.abort();
  }, [load]);

  const visibleCheckpoints = useMemo(
    () => (state.status === 'ready' ? filterFeedbackCheckpoints(state.data.checkpoints, filters) : []),
    [state, filters],
  );

  const toggle = (checkpointId: string) => setCollapsed((current) => {
    const next = new Set(current);
    if (!next.delete(checkpointId)) next.add(checkpointId);
    return next;
  });

  if (state.status === 'loading') {
    return <LoadingSkeleton variant="table" lines={4} />;
  }

  if (state.status === 'error') {
    return <ErrorState title="Feedback overview unavailable" message={state.message} onRetry={() => void load()} />;
  }

  const allCheckpoints = state.data.checkpoints;
  const hasAnyFeedback = countFeedbackItems(allCheckpoints) > 0;
  const filtersActive = filters.checkpointId !== 'all' || filters.source !== 'all' || filters.query.trim() !== '';

  if (allCheckpoints.length === 0 || !hasAnyFeedback) {
    return (
      <EmptyState
        icon={MessageSquare}
        title="No feedback yet"
        description="Feedback from lecturers, mentors and admins on your checkpoints will be collected here."
      />
    );
  }

  const fieldClass = 'h-9 rounded-lg border border-slate-300 bg-white px-3 text-body-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-primary';

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex min-w-[12rem] flex-1 flex-col gap-1 text-caption font-semibold text-slate-600">
          Search
          <span className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden="true" />
            <input
              type="search"
              value={filters.query}
              onChange={(event) => setFilters((current) => ({ ...current, query: event.target.value }))}
              placeholder="Search feedback or author"
              className={`${fieldClass} w-full pl-9`}
            />
          </span>
        </label>
        <label className="flex flex-col gap-1 text-caption font-semibold text-slate-600">
          Checkpoint
          <select
            value={filters.checkpointId}
            onChange={(event) => setFilters((current) => ({ ...current, checkpointId: event.target.value }))}
            className={fieldClass}
          >
            <option value="all">All checkpoints</option>
            {allCheckpoints.map((checkpoint) => (
              <option key={checkpoint.checkpointId} value={checkpoint.checkpointId}>
                Checkpoint {checkpoint.checkpointNumber}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-caption font-semibold text-slate-600">
          Source
          <select
            value={filters.source}
            onChange={(event) => setFilters((current) => ({ ...current, source: event.target.value as TeamFeedbackSource }))}
            className={fieldClass}
          >
            <option value="all">All feedback</option>
            <option value="comments">Comments</option>
            <option value="evaluations">Evaluations</option>
          </select>
        </label>
        {filtersActive && (
          <Button variant="ghost" size="sm" onClick={() => setFilters(defaultTeamFeedbackFilters)}>Clear filters</Button>
        )}
      </div>

      {visibleCheckpoints.length === 0 ? (
        <EmptyState
          icon={Search}
          title="No matching feedback"
          description="Try a different checkpoint, source or search term."
          className="py-10"
        />
      ) : (
        <ol className="space-y-3" aria-label="Feedback by checkpoint">
          {visibleCheckpoints.map((checkpoint) => (
            <CheckpointBlock
              key={checkpoint.checkpointId}
              checkpoint={checkpoint}
              open={!collapsed.has(checkpoint.checkpointId)}
              onToggle={() => toggle(checkpoint.checkpointId)}
              onOpenCheckpoints={onOpenCheckpoints}
            />
          ))}
        </ol>
      )}
    </div>
  );
}
