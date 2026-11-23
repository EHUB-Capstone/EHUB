import { ClipboardCheck } from 'lucide-react';
import Badge from '../ui/Badge';
import Card from '../ui/Card';
import type { ShowcaseReferenceDataDto, ShowcaseReferenceEvaluationDto } from '../../types/showcase';
import { useTranslation } from 'react-i18next';

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
  const { t } = useTranslation('showcase');
  return (
    <li className="rounded-lg border border-slate-200 p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-body-sm font-semibold text-secondary">{t('showcaseReferenceData.evaluation', { evaluatorRole: evaluation.evaluatorRole })}</span>
        <Badge variant="info">{evaluation.status}</Badge>
      </div>
      <p className="mt-1 text-heading text-secondary">
        {formatScore(evaluation.totalScore)}
        <span className="text-body-sm font-normal text-slate-600"> / {formatScore(evaluation.maxTotalScore)}</span>
      </p>
      {evaluation.publishedAt && (
        <p className="text-caption text-slate-600">{t('showcaseReferenceData.published', { v1: new Date(evaluation.publishedAt).toLocaleDateString() })}</p>
      )}
      <dl className="mt-2 space-y-2">
        <FeedbackLine label={t('showcaseReferenceData.strengths')} value={evaluation.strengths} />
        <FeedbackLine label={t('showcaseReferenceData.weaknesses')} value={evaluation.weaknesses} />
        <FeedbackLine label={t('showcaseReferenceData.suggestions')} value={evaluation.suggestions} />
      </dl>
    </li>
  );
}

/** Class, semester and evaluation results from earlier modules. The server only sends this to reviewers. */
export default function ShowcaseReferenceData({ data }: { data: ShowcaseReferenceDataDto }) {
  const { t } = useTranslation('showcase');
  return (
    <Card hover={false} padding="p-5" aria-labelledby="showcase-reference-title">
      <Card.Header className="mb-3">
        <Card.Title icon={ClipboardCheck}><span id="showcase-reference-title">{t('showcaseReferenceData.referenceData')}</span></Card.Title>
      </Card.Header>
      <dl className="grid grid-cols-2 gap-3 text-body-sm">
        <div>
          <dt className="text-caption font-semibold text-slate-600">{t('showcaseReferenceData.class')}</dt>
          <dd className="text-slate-700">{data.classCode}</dd>
        </div>
        <div>
          <dt className="text-caption font-semibold text-slate-600">{t('showcaseReferenceData.semester')}</dt>
          <dd className="text-slate-700">{data.semester ? data.semester.name || data.semester.code : t('showcaseReferenceData.notAvailable')}</dd>
        </div>
      </dl>
      <h3 className="mt-4 text-body-sm font-semibold text-secondary">{t('showcaseReferenceData.evaluationResults')}</h3>
      {data.evaluations.length === 0 ? (
        <p className="mt-1 text-body-sm italic text-slate-600">{t('showcaseReferenceData.noPublishedEvaluationYet')}</p>
      ) : (
        <ul className="mt-2 space-y-3">
          {data.evaluations.map(evaluation => <EvaluationItem key={evaluation.evaluatorRole} evaluation={evaluation} />)}
        </ul>
      )}
      <p className="mt-3 text-caption text-slate-600"></p>
    </Card>
  );
}
