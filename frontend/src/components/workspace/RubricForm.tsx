// @ts-nocheck
import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';

const normalizeCriteria = (criteria = []) => {
  const source = Array.isArray(criteria) ? criteria : [];
  return source.map((item) => ({
    criterionKey: item.key || item.criterionKey,
    criterionName: item.label || item.criterionName,
    description: item.description || '',
    weight: Number(item.weight ?? 1),
    maxScore: Number(item.maxScore ?? 10),
  }));
};

const initialRows = (criteria, initialData) => {
  const byKey = new Map((initialData?.rubricScores || []).map((item) => [item.criterionKey, item]));
  return criteria.map((criterion) => {
    const existing = byKey.get(criterion.criterionKey) || {};
    return {
      ...criterion,
      score: existing.score ?? existing.manualScore ?? '',
      comment: existing.comment || '',
    };
  });
};

export default function RubricForm({
  initialData,
  onSubmit,
  readOnly = false,
  hideSensitiveScores = false,
  criteria: customCriteria,
  checkpointNumber,
  checkpointTitle,
  compact = false,
}) {
  const criteria = useMemo(() => normalizeCriteria(customCriteria), [customCriteria]);
  const [rubricScores, setRubricScores] = useState(() => initialRows(criteria, initialData));
  const [overallFeedback, setOverallFeedback] = useState(initialData?.overallFeedback || '');
  const [attemptedSubmit, setAttemptedSubmit] = useState(false);
  const isSubmitted = initialData?.status === 'SUBMITTED' || initialData?.status === 'PUBLISHED';

  useEffect(() => {
    // Keep existing scores and comments visible when loading or switching evaluations.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setRubricScores(initialRows(criteria, initialData));
    setOverallFeedback(initialData?.overallFeedback || '');
    setAttemptedSubmit(false);
  }, [criteria, initialData]);

  const updateRow = (index, patch) => {
    if (readOnly) return;
    setRubricScores((previous) => previous.map((row, rowIndex) => rowIndex === index ? { ...row, ...patch } : row));
  };

  const handleScoreChange = (index, value) => {
    const normalized = value.replace(',', '.');
    if (!/^\d*(?:\.\d*)?$/.test(normalized)) return;
    updateRow(index, { score: normalized });
  };

  const scoreError = (item, required = false) => {
    if (item.score === '' || item.score == null) return required ? 'Enter a score.' : '';
    const score = Number(item.score);
    if (!Number.isFinite(score) || score < 0 || score > item.maxScore) {
      return `Enter a score from 0 to ${item.maxScore}.`;
    }
    return '';
  };

  const checkpointTotal = useMemo(() => rubricScores.reduce((sum, item) => {
    const score = Number(item.score);
    return sum + (item.score !== '' && Number.isFinite(score) && score >= 0 && score <= item.maxScore
      ? score * item.weight / 100 : 0);
  }, 0), [rubricScores]);

  const handleSubmit = (status) => {
    if (readOnly || !onSubmit) return;
    if (rubricScores.length === 0) {
      toast.error('No rubric criteria are configured for this checkpoint.');
      return;
    }
    const required = status === 'SUBMITTED';
    setAttemptedSubmit(required);
    if (rubricScores.some((item) => scoreError(item, required))) {
      toast.error(required
        ? 'Score every configured criterion before submitting the evaluation.'
        : 'Correct invalid scores before saving the draft.');
      return;
    }

    onSubmit({
      checkpointNumber,
      checkpointTitle,
      rubricScores: rubricScores.map((item) => ({
        criterionKey: item.criterionKey,
        score: item.score === '' || item.score == null ? null : Number(item.score),
        comment: item.comment,
      })),
      overallFeedback,
      status: isSubmitted ? 'SUBMITTED' : status,
    });
  };

  const title = checkpointTitle && checkpointTitle !== `Checkpoint ${checkpointNumber}`
    ? checkpointTitle : `Checkpoint ${checkpointNumber}`;

  return (
    <div className={`rounded-2xl border border-slate-200 bg-white shadow-sm ${compact ? 'p-3 sm:p-4' : 'p-4 sm:p-5'}`}>
      <div className="mb-3 flex items-center justify-between gap-3">
        <div className="min-w-0">
          <h3 className="truncate text-sm font-bold text-slate-900">{title} · Enter scores</h3>
          <p className="text-xs text-slate-500">Type a score for each criterion.{initialData?.status ? ` · ${initialData.status}` : ''}</p>
        </div>
        {!hideSensitiveScores && (
          <div className="shrink-0 rounded-xl bg-blue-50 px-3 py-1.5 text-right">
            <span className="text-xs font-semibold text-blue-600">Total </span>
            <span className="text-lg font-black text-blue-700">{checkpointTotal.toFixed(2)}</span>
            <span className="text-xs text-blue-500"> / 10</span>
          </div>
        )}
      </div>

      <div className="divide-y divide-slate-100 rounded-xl border border-slate-200">
        {rubricScores.length === 0 && (
          <p className="px-3 py-4 text-sm text-slate-500">No rubric criteria are configured for this checkpoint.</p>
        )}
        {rubricScores.map((item, index) => {
          const error = scoreError(item, attemptedSubmit);
          return (
            <div key={`${item.criterionKey}-${index}`} className="flex min-h-12 items-center gap-2 px-3 py-1.5 sm:gap-3">
              <div className="min-w-0 flex-1" title={item.description || undefined}>
                <label htmlFor={`rubric-score-${checkpointNumber}-${index}`} className="block truncate text-sm font-medium text-slate-800">
                  {item.criterionName}
                </label>
                <span className="text-[11px] text-slate-400">{item.weight}% weight</span>
              </div>
              {hideSensitiveScores ? (
                <span className="text-xs text-slate-500">Score hidden</span>
              ) : (
                <div className="flex w-28 shrink-0 flex-col items-end">
                  <div className={`flex w-full items-center rounded-lg border bg-white px-2 ${error ? 'border-red-400' : 'border-slate-200 focus-within:border-primary'}`}>
                    <input
                      id={`rubric-score-${checkpointNumber}-${index}`}
                      type="text"
                      inputMode="decimal"
                      value={item.score ?? ''}
                      onChange={(event) => handleScoreChange(index, event.target.value)}
                      disabled={readOnly}
                      placeholder="0"
                      aria-invalid={Boolean(error)}
                      aria-describedby={error ? `rubric-error-${checkpointNumber}-${index}` : undefined}
                      className="min-w-0 w-full border-0 bg-transparent py-1.5 text-right text-sm font-bold text-slate-900 outline-none disabled:text-slate-400"
                    />
                    <span className="shrink-0 pl-1 text-xs text-slate-400">/ {item.maxScore}</span>
                  </div>
                  {error && <span id={`rubric-error-${checkpointNumber}-${index}`} className="text-right text-[10px] text-red-600">{error}</span>}
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="mt-3">
        <label htmlFor={`overall-feedback-${checkpointNumber}`} className="mb-1 block text-xs font-semibold text-slate-600">Overall feedback (optional)</label>
        <textarea
          id={`overall-feedback-${checkpointNumber}`}
          rows={compact ? 2 : 3}
          value={overallFeedback}
          onChange={(event) => setOverallFeedback(event.target.value)}
          disabled={readOnly}
          placeholder="General feedback for the team..."
          className="block w-full resize-none rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 outline-none focus:border-primary disabled:bg-slate-50"
        />
      </div>

      {rubricScores.length > 0 && (
        <details className="mt-2 text-xs text-slate-600">
          <summary className="cursor-pointer font-semibold">Criterion comments (optional)</summary>
          <div className="mt-2 space-y-2">
            {rubricScores.map((item, index) => (
              <label key={item.criterionKey} className="block">
                <span className="mb-1 block">{item.criterionName}</span>
                <input
                  type="text"
                  value={item.comment}
                  onChange={(event) => updateRow(index, { comment: event.target.value })}
                  disabled={readOnly}
                  className="w-full rounded-lg border border-slate-200 px-3 py-1.5 text-sm"
                />
              </label>
            ))}
          </div>
        </details>
      )}

      {onSubmit && !hideSensitiveScores && (
        <div className="mt-3 flex justify-end gap-2">
          {!isSubmitted && (
            <button type="button" disabled={readOnly || rubricScores.length === 0} onClick={() => handleSubmit('DRAFT')} className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
              Save draft
            </button>
          )}
          <button type="button" disabled={readOnly || rubricScores.length === 0} onClick={() => handleSubmit('SUBMITTED')} className="rounded-lg bg-primary px-3 py-2 text-xs font-semibold text-white hover:bg-primary/90 disabled:opacity-50">
            {readOnly ? 'Saving...' : isSubmitted ? 'Save changes' : 'Submit evaluation'}
          </button>
        </div>
      )}
    </div>
  );
}
