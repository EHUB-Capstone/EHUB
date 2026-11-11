import { ClipboardCheck } from 'lucide-react';
import Badge from '../ui/Badge';
import Card from '../ui/Card';
import type { ShowcaseReferenceDataDto, ShowcaseReferenceEvaluationDto } from '../../types/showcase';

function formatScore(value: number) {
  return Number.isInteger(value) ? String(value) : value.toFixed(2);
}

function FeedbackLine({ label, value }: { label: string; value: string | null }) {
  if (!value?.trim()) return null;
  return (
    <div>
      <dt className="text-caption font-semibold text-slate-600">{label}</dt>
      <dd className="whitespace-pre-line text-body-sm text-slate-700">{value}</dd>
    </div>
  );
}

function EvaluationItem({ evaluation }: { evaluation: ShowcaseReferenceEvaluationDto }) {
  return (
    <li className="rounded-lg border border-slate-200 p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-body-sm font-semibold text-secondary">{evaluation.evaluatorRole} evaluation</span>
        <Badge variant="info">{evaluation.status}</Badge>
      </div>
      <p className="mt-1 text-heading text-secondary">
        {formatScore(evaluation.totalScore)}
        <span className="text-body-sm font-normal text-slate-600"> / {formatScore(evaluation.maxTotalScore)}</span>
      </p>
      {evaluation.publishedAt && (
        <p className="text-caption text-slate-600">Published {new Date(evaluation.publishedAt).toLocaleDateString()}</p>
      )}
      <dl className="mt-2 space-y-2">
        <FeedbackLine label="Strengths" value={evaluation.strengths} />
        <FeedbackLine label="Weaknesses" value={evaluation.weaknesses} />
        <FeedbackLine label="Suggestions" value={evaluation.suggestions} />
      </dl>
    </li>
  );
}

/** Class, semester and evaluation results from earlier modules. The server only sends this to reviewers. */
export default function ShowcaseReferenceData({ data }: { data: ShowcaseReferenceDataDto }) {
  return (
    <Card hover={false} padding="p-5" aria-labelledby="showcase-reference-title">
      <Card.Header className="mb-3">
        <Card.Title icon={ClipboardCheck}><span id="showcase-reference-title">Reference data</span></Card.Title>
      </Card.Header>
      <dl className="grid grid-cols-2 gap-3 text-body-sm">
        <div>
          <dt className="text-caption font-semibold text-slate-600">Class</dt>
          <dd className="text-slate-700">{data.classCode}</dd>
        </div>
        <div>
          <dt className="text-caption font-semibold text-slate-600">Semester</dt>
          <dd className="text-slate-700">{data.semester ? data.semester.name || data.semester.code : 'Not available'}</dd>
        </div>
      </dl>
      <h3 className="mt-4 text-body-sm font-semibold text-secondary">Evaluation results</h3>
      {data.evaluations.length === 0 ? (
        <p className="mt-1 text-body-sm italic text-slate-600">No published evaluation yet.</p>
      ) : (
        <ul className="mt-2 space-y-3">
          {data.evaluations.map(evaluation => <EvaluationItem key={evaluation.evaluatorRole} evaluation={evaluation} />)}
        </ul>
      )}
      <p className="mt-3 text-caption text-slate-600"></p>
    </Card>
  );
}
