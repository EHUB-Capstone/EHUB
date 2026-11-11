import { useId } from 'react';
import { useQuery } from '@tanstack/react-query';
import { projectDataApi } from '../../../api/projectDataApi';
import Badge from '../../../components/ui/Badge';
import LoadingSkeleton from '../../../components/ui/LoadingSkeleton';
import { useAuth } from '../../../hooks/useAuth';
import type {
  ProjectEvidence,
  ProjectEvidenceComponent,
  ProjectEvidenceEvaluation,
  ProjectEvidenceMentor,
  ProjectEvidenceMentoring,
} from '../../../types/projectData';
import {
  describeEvidenceMentorPeriod,
  describeEvidenceMentoring,
  describeEvidenceStatus,
  describeEvidenceWeight,
  evidenceMentorSlotLabel,
  evidenceSessionsLabel,
  formatEvidenceDate,
  formatEvidenceScore,
  hasEvidenceGrades,
  projectEvidenceKey,
} from '../../../utils/projectData';

function SectionTitle({ id, children }: { id: string; children: string }) {
  return <h3 id={id} className="text-subheading font-semibold text-secondary">{children}</h3>;
}

function ComponentRow({ component }: { component: ProjectEvidenceComponent }) {
  const status = describeEvidenceStatus(component.status);
  return (
    <li className="rounded-xl border border-slate-100 bg-white px-3 py-2.5">
      <div className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1">
        <div className="min-w-0">
          <p className="break-words text-sm font-semibold text-slate-700">{component.name}</p>
          <p className="text-xs text-slate-400">{describeEvidenceWeight(component)}</p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {component.status === 'Published' && (
            <span className="text-sm font-semibold text-slate-700">
              {formatEvidenceScore(component.score)}<span className="font-normal text-slate-400"> / 10</span>
            </span>
          )}
          <Badge variant={status.tone} size="sm">{status.label}</Badge>
        </div>
      </div>
      {component.feedback && (
        <blockquote
          className="mt-1.5 line-clamp-3 whitespace-pre-line break-words border-l-2 border-slate-200 pl-2 text-sm text-slate-600"
          title={component.feedback}
        >
          {component.feedback}
        </blockquote>
      )}
      {component.publishedAtUtc && (
        <p className="mt-1 text-xs text-slate-400">Published on {formatEvidenceDate(component.publishedAtUtc)}</p>
      )}
    </li>
  );
}

function EvaluationBlock({ evaluation }: { evaluation: ProjectEvidenceEvaluation }) {
  if (!hasEvidenceGrades(evaluation)) {
    return (
      <p className="text-sm text-slate-400">
        {evaluation.components.length === 0
          ? 'The course has no weighted checkpoints yet.'
          : 'No evaluation results yet.'}
      </p>
    );
  }

  return (
    <div className="space-y-3">
      <p className="text-sm text-slate-600">
        <span className="font-medium text-slate-500">Course score </span>
        {evaluation.courseScore === null ? (
          <span className="text-slate-400">not available until every part is published</span>
        ) : (
          <span className="text-body-lg font-semibold text-slate-700">
            {formatEvidenceScore(evaluation.courseScore)}<span className="text-sm font-normal text-slate-400"> / 10</span>
          </span>
        )}
      </p>
      <ul aria-label="Graded parts of the course" className="space-y-2">
        {evaluation.components.map(component => (
          <ComponentRow key={`${component.kind}-${component.checkpointNumber ?? component.name}`} component={component} />
        ))}
      </ul>
    </div>
  );
}

function MentorRow({ mentor }: { mentor: ProjectEvidenceMentor }) {
  return (
    <li className="rounded-xl border border-slate-100 bg-white px-3 py-2.5">
      <div className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1">
        <div className="min-w-0">
          <p className="break-words text-sm font-semibold text-slate-700">{mentor.fullName}</p>
          <p className="text-xs text-slate-400">
            {[evidenceMentorSlotLabel(mentor.slot), mentor.organization].filter(Boolean).join(' · ')}
          </p>
        </div>
        <Badge variant={mentor.isCurrent ? 'success' : 'neutral'} size="sm">
          {mentor.isCurrent ? 'Current' : 'Earlier'}
        </Badge>
      </div>
      <p className="mt-1 text-xs text-slate-500">
        {describeEvidenceMentorPeriod(mentor)} · {evidenceSessionsLabel(mentor.completedSessions)}
      </p>
      {mentor.expertise.length > 0 && (
        <ul aria-label={`Expertise of ${mentor.fullName}`} className="mt-1.5 flex flex-wrap gap-1.5">
          {mentor.expertise.map(tag => (
            <li key={tag} className="rounded-md bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{tag}</li>
          ))}
        </ul>
      )}
    </li>
  );
}

function MentoringBlock({ mentoring }: { mentoring: ProjectEvidenceMentoring }) {
  if (mentoring.mentors.length === 0) {
    return <p className="text-sm text-slate-400">No mentor has been assigned to this team.</p>;
  }

  return (
    <div className="space-y-3">
      <p className="text-sm text-slate-600">{describeEvidenceMentoring(mentoring)}</p>
      <ul aria-label="Mentors of the team" className="space-y-2">
        {mentoring.mentors.map(mentor => <MentorRow key={mentor.assignmentId} mentor={mentor} />)}
      </ul>
    </div>
  );
}

interface ProjectEvidenceViewProps {
  isLoading: boolean;
  isError: boolean;
  evidence: ProjectEvidence | undefined;
  onRetry: () => void;
}

/** The two blocks themselves, without any data fetching. */
export function ProjectEvidenceView({ isLoading, isError, evidence, onRetry }: ProjectEvidenceViewProps) {
  const evaluationId = useId();
  const mentoringId = useId();

  if (isLoading) return <LoadingSkeleton lines={4} />;

  if (isError || !evidence) {
    return (
      <p role="alert" className="flex flex-wrap items-center gap-2 text-sm text-danger">
        The evaluation and mentoring details could not be loaded.
        <button type="button" onClick={onRetry} className="font-semibold underline underline-offset-2">Retry</button>
      </p>
    );
  }

  return (
    <div className="space-y-6">
      <section aria-labelledby={evaluationId} className="space-y-3">
        <SectionTitle id={evaluationId}>Evaluation</SectionTitle>
        <EvaluationBlock evaluation={evidence.evaluation} />
      </section>
      <section aria-labelledby={mentoringId} className="space-y-3">
        <SectionTitle id={mentoringId}>Mentoring</SectionTitle>
        <MentoringBlock mentoring={evidence.mentoring} />
      </section>
    </div>
  );
}

interface ProjectEvidenceSectionProps {
  projectId: string;
}

/** Published scores and feedback of the project plus the mentors of its team; read only when the project is opened. */
export default function ProjectEvidenceSection({ projectId }: ProjectEvidenceSectionProps) {
  const { user } = useAuth();

  const evidence = useQuery({
    queryKey: projectEvidenceKey(user?.id, projectId),
    queryFn: async ({ signal }) => (await projectDataApi.getEvidence(projectId, signal)).data,
    enabled: Boolean(user?.id),
    staleTime: 0,
  });

  return (
    <ProjectEvidenceView
      isLoading={evidence.isPending && evidence.fetchStatus !== 'idle'}
      isError={evidence.isError}
      evidence={evidence.data}
      onRetry={() => { void evidence.refetch(); }}
    />
  );
}
