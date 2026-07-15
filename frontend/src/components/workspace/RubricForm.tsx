// @ts-nocheck
import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import PerformanceLevelBadge from '../evaluation/PerformanceLevelBadge';

const DEFAULT_CRITERIA = [
  { criterionKey: 'problem', criterionName: 'Problem Clarity', maxScore: 10, weight: 1 },
  { criterionKey: 'solution', criterionName: 'Solution Feasibility', maxScore: 10, weight: 1 },
  { criterionKey: 'market', criterionName: 'Market Potential', maxScore: 10, weight: 1 },
  { criterionKey: 'businessModel', criterionName: 'Business Model', maxScore: 10, weight: 1 },
  { criterionKey: 'pitchDeck', criterionName: 'Pitch Deck Quality', maxScore: 10, weight: 1 },
  { criterionKey: 'team', criterionName: 'Team Capability', maxScore: 10, weight: 1 },
];

const LEVEL_OPTIONS = [
  { value: 'EXCELLENT', label: 'Excellent', score: 9.2, range: '8.5–10' },
  { value: 'GOOD', label: 'Good', score: 7.7, range: '7.0–8.4' },
  { value: 'FAIR', label: 'Fair', score: 6.0, range: '5.0–6.9' },
  { value: 'POOR', label: 'Poor', score: 4.0, range: '< 5.0' },
];

const getLevelColor = (level) => {
  switch (level) {
    case 'EXCELLENT': return 'bg-emerald-50/60 border-emerald-200 text-emerald-700 hover:bg-emerald-100/60';
    case 'GOOD': return 'bg-blue-50/60 border-blue-200 text-blue-700 hover:bg-blue-100/60';
    case 'FAIR': return 'bg-amber-50/60 border-amber-200 text-amber-700 hover:bg-amber-100/60';
    case 'POOR': return 'bg-rose-50/60 border-rose-200 text-rose-700 hover:bg-rose-100/60';
    default: return 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100';
  }
};

const getActiveLevelColor = (level) => {
  switch (level) {
    case 'EXCELLENT': return 'bg-emerald-600 border-emerald-600 text-white ring-2 ring-emerald-500/20';
    case 'GOOD': return 'bg-blue-600 border-blue-600 text-white ring-2 ring-blue-500/20';
    case 'FAIR': return 'bg-amber-500 border-amber-500 text-white ring-2 ring-amber-500/20';
    case 'POOR': return 'bg-rose-600 border-rose-600 text-white ring-2 ring-rose-500/20';
    default: return '';
  }
};

const normalizeCriteria = (criteria = []) => {
  const source = Array.isArray(criteria) && criteria.length > 0 ? criteria : DEFAULT_CRITERIA;
  return source.map((item) => ({
    criterionKey: item.key || item.criterionKey,
    criterionName: item.label || item.criterionName,
    description: item.description || '',
    weight: Number(item.weight ?? 1),
    maxScore: Number(item.maxScore ?? 10),
    levels: item.levels || LEVEL_OPTIONS,
  }));
};

const initialRows = (criteria, initialData) => {
  const byKey = new Map((initialData?.rubricScores || []).map((item) => [item.criterionKey, item]));

  return criteria.map((criterion) => {
    const existing = byKey.get(criterion.criterionKey) || {};
    const fallbackLevel = existing.selectedLevel || existing.level || '';
    const scoreMode = existing.scoreMode || (existing.manualScore != null ? 'MANUAL' : 'LEVEL');

    let currentScore = existing.score ?? existing.manualScore;
    if (currentScore == null) {
      if (scoreMode === 'LEVEL') {
        // Only default to level score if an explicit level was provided in existing data
        if (existing.selectedLevel || existing.level) {
          const meta = LEVEL_OPTIONS.find((opt) => opt.value === fallbackLevel) || LEVEL_OPTIONS[1];
          currentScore = meta.score;
        } else {
          // prefer empty / zero when no prior level exists
          currentScore = '';
        }
      } else {
        currentScore = '';
      }
    }

    return {
      ...criterion,
      selectedLevel: fallbackLevel,
      scoreMode: scoreMode,
      score: currentScore,
      comment: existing.comment || '',
      weightedScore: Number(existing.weightedScore ?? 0),
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
}) {
  const criteria = useMemo(() => normalizeCriteria(customCriteria), [customCriteria]);
  const [rubricScores, setRubricScores] = useState(() => initialRows(criteria, initialData));
  const [overallFeedback, setOverallFeedback] = useState('');
  const isSubmitted = initialData?.status === 'SUBMITTED' || initialData?.status === 'PUBLISHED';
  const [showConfirmModal, setShowConfirmModal] = useState(false);
  const [pendingStatus, setPendingStatus] = useState(null);

  useEffect(() => {
    // Reset the form when switching checkpoint or loading a newer evaluation version.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setRubricScores(initialRows(criteria, initialData));
    setOverallFeedback(initialData?.overallFeedback || '');
  }, [criteria, initialData]);

  const selectedLevelMeta = (value) => LEVEL_OPTIONS.find((opt) => opt.value === value) || LEVEL_OPTIONS[1];

  const guessLevelFromScore = (score) => {
    if (score === '' || score == null) return '';
    const s = Number(score);
    if (!Number.isFinite(s)) return '';
    if (s >= 8.5) return 'EXCELLENT';
    if (s >= 7.0) return 'GOOD';
    if (s >= 5.0) return 'FAIR';
    return 'POOR';
  };

  const updateRow = (index, patch) => {
    if (readOnly) return;
    setRubricScores((prev) => {
      const next = [...prev];
      const current = { ...next[index], ...patch };
      const score = Number(current.score || 0);
      const weight = Number(current.weight || 0);
      current.weightedScore = Number(((score * weight) / 100).toFixed(2));
      next[index] = current;
      return next;
    });
  };

  const handleLevelChange = (index, level) => {
    if (!level) {
      updateRow(index, {
        selectedLevel: '',
        scoreMode: 'LEVEL',
        score: '',
      });
      return;
    }
    const meta = selectedLevelMeta(level);
    updateRow(index, {
      selectedLevel: level,
      scoreMode: 'LEVEL',
      score: meta.score,
    });
  };

  const handleScoreChange = (index, value) => {
    let raw = value.replace(/,/g, '.');
    let cleaned = raw.replace(/[^0-9.]/g, '');
    
    const parts = cleaned.split('.');
    if (parts.length > 2) {
      cleaned = parts[0] + '.' + parts.slice(1).join('');
    }

    if (cleaned.startsWith('0') && cleaned.length > 1 && cleaned[1] !== '.') {
      cleaned = cleaned.replace(/^0+/, '');
      if (cleaned === '') cleaned = '0';
    }

    let numeric = parseFloat(cleaned);
    if (!isNaN(numeric)) {
      if (numeric > 10) {
        cleaned = '10';
        numeric = 10;
      } else if (numeric < 0) {
        cleaned = '0';
        numeric = 0;
      }
    }

    const level = isNaN(numeric) ? '' : guessLevelFromScore(numeric);

    updateRow(index, {
      scoreMode: 'MANUAL',
      score: cleaned,
      selectedLevel: level,
    });
  };

  const handleCommentChange = (index, value) => updateRow(index, { comment: value });

  const checkpointTotal = useMemo(
    () => rubricScores.reduce((sum, item) => sum + (Number(item.weightedScore) || 0), 0),
    [rubricScores]
  );
  const totalWeight = useMemo(
    () => rubricScores.reduce((sum, item) => sum + (Number(item.weight) || 0), 0),
    [rubricScores]
  );
  const totalRawScore = useMemo(
    () => rubricScores.reduce((sum, item) => sum + (Number(item.score) || 0), 0),
    [rubricScores]
  );
  const maxTotal = useMemo(
    () => rubricScores.reduce((sum, item) => sum + (Number(item.maxScore) || 10), 0),
    [rubricScores]
  );

  const executeSubmit = (status) => {
    onSubmit({
      checkpointNumber,
      checkpointTitle,
      rubricScores,
      overallFeedback,
      status: isSubmitted ? initialData.status : status,
    });
  };

  const handleConfirmSaveAnyway = () => {
    setShowConfirmModal(false);
    if (pendingStatus) {
      executeSubmit(pendingStatus);
      setPendingStatus(null);
    }
  };

  const handleCancelSaveAnyway = () => {
    setShowConfirmModal(false);
    setPendingStatus(null);
  };

  const handleSubmit = (e, status) => {
    e.preventDefault();
    if (readOnly || !onSubmit) return;

    const hasUncompleted = rubricScores.some(
      (item) => !item.selectedLevel || item.score === '' || item.score == null
    );

    if (hasUncompleted) {
      setPendingStatus(status);
      setShowConfirmModal(true);
      return;
    }

    executeSubmit(status);
  };

  return (
    <div className="bg-white p-5 sm:p-6 rounded-2xl shadow-sm border border-slate-200/70">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between mb-5">
        <div>
          <h3 className="text-lg font-black text-slate-900">Evaluation Rubric</h3>
          <p className="text-sm text-slate-500">
            {checkpointTitle ? `Checkpoint ${checkpointNumber} · ${checkpointTitle}` : 'Manual and level-based scoring with weighted totals.'}
          </p>
        </div>
      {/* Top summary stats — hidden for Mentor */}
      {!hideSensitiveScores && (
        <div className="grid grid-cols-3 gap-2 text-xs">
          <div className="rounded-xl bg-slate-50 border border-slate-200 px-3 py-2 text-center">
            <p className="text-slate-400 uppercase tracking-wider font-semibold">Raw</p>
            <p className="font-black text-slate-900">{totalRawScore.toFixed(1)} / {maxTotal.toFixed(0)}</p>
          </div>
          <div className="rounded-xl bg-blue-50 border border-blue-100 px-3 py-2 text-center">
            <p className="text-blue-500 uppercase tracking-wider font-semibold">Weighted</p>
            <p className="font-black text-blue-700">{checkpointTotal.toFixed(2)} / 10</p>
          </div>
          <div className={`rounded-xl border px-3 py-2 text-center ${totalWeight === 100 ? 'bg-emerald-50 border-emerald-100' : 'bg-amber-50 border-amber-100'}`}>
            <p className={`uppercase tracking-wider font-semibold ${totalWeight === 100 ? 'text-emerald-500' : 'text-amber-600'}`}>Weight</p>
            <p className={`font-black ${totalWeight === 100 ? 'text-emerald-700' : 'text-amber-700'}`}>{totalWeight}%</p>
          </div>
        </div>
      )}
      </div>

      <div className="space-y-6 mb-8">
        {rubricScores.map((item, index) => (
          <div
            key={`${item.criterionKey}-${index}`}
            className="bg-slate-50/40 rounded-2xl border border-slate-200/80 p-5 sm:p-6 space-y-5 transition-all hover:shadow-md hover:border-slate-300/60"
          >
            {/* Card Header */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-200/60">
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <h4 className="text-base font-bold text-slate-800">{item.criterionName}</h4>
                  <span className="inline-flex items-center rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-600">
                    Weight: {item.weight}%
                  </span>
                </div>
                {item.description && <p className="text-xs text-slate-500 mt-1">{item.description}</p>}
              </div>
              {!hideSensitiveScores && (
                <div className="flex items-center gap-2 self-start sm:self-center">
                  <span className="text-xs text-slate-400 font-medium uppercase tracking-wider">Weighted:</span>
                  <span className="text-lg font-black text-blue-600 bg-blue-50/50 border border-blue-100 px-3 py-1 rounded-xl">
                    {Number(item.weightedScore || 0).toFixed(2)}
                  </span>
                </div>
              )}
            </div>

            {/* Grid Inputs */}
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
              {/* Level Selection Section */}
              <div className="lg:col-span-7 space-y-2">
                <label className="block text-xs font-bold uppercase tracking-wider text-slate-500">
                  Performance Level
                </label>
                {hideSensitiveScores ? (
                  <div className="py-2">
                    <PerformanceLevelBadge
                      level={item.level || 'Unscored'}
                      label={item.label || ''}
                      size="md"
                    />
                  </div>
                ) : (
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
                    {item.levels.map((level) => {
                      const val = level.value || level.key;
                      const isActive = item.selectedLevel === val;
                      return (
                        <button
                          key={val}
                          type="button"
                          disabled={readOnly}
                          onClick={() => handleLevelChange(index, val)}
                          className={`px-3 py-2.5 rounded-xl border text-center transition-all ${
                            isActive ? getActiveLevelColor(val) : getLevelColor(val)
                          } text-xs font-bold shadow-sm active:scale-95 disabled:opacity-50 disabled:pointer-events-none`}
                        >
                          <div>{level.label}</div>
                          <div className={`text-[10px] mt-0.5 ${isActive ? 'text-white/80' : 'text-slate-400'}`}>
                            {level.range}
                          </div>
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* Manual Score Section */}
              {!hideSensitiveScores && (
                <div className="lg:col-span-5 space-y-2">
                  <label className="block text-xs font-bold uppercase tracking-wider text-slate-500">
                    Manual Score Override
                  </label>
                  <div className="relative flex items-center">
                    <input
                      type="text"
                      inputMode="decimal"
                      value={item.score ?? ''}
                      onChange={(e) => handleScoreChange(index, e.target.value)}
                      disabled={readOnly}
                      placeholder="Enter score (0-10)"
                      className="w-full rounded-xl border border-slate-200 bg-white pl-4 pr-12 py-2.5 text-sm font-bold focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 disabled:bg-slate-50 transition-all text-slate-900"
                    />
                    <span className="absolute right-4 text-xs font-semibold text-slate-400">
                      / 10
                    </span>
                  </div>
                </div>
              )}

              {/* Comment Section */}
              <div className="lg:col-span-12 space-y-2">
                <label className="block text-xs font-bold uppercase tracking-wider text-slate-500">
                  Criterion Feedback & Comments
                </label>
                <textarea
                  rows={3}
                  placeholder="Provide specific feedback or observations for this criterion..."
                  value={item.comment}
                  onChange={(e) => handleCommentChange(index, e.target.value)}
                  disabled={readOnly || hideSensitiveScores}
                  className="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 disabled:bg-slate-50 resize-y transition-all text-slate-700"
                />
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Bottom score summary cards — hidden for Mentor */}
      {!hideSensitiveScores && (
        <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div className="rounded-2xl bg-slate-50 border border-slate-200 p-4">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-400">Total Raw Score</p>
            <p className="mt-1 text-2xl font-black text-slate-900">{totalRawScore.toFixed(2)} <span className="text-sm text-slate-400">/ {maxTotal.toFixed(0)}</span></p>
          </div>
          <div className="rounded-2xl bg-blue-50 border border-blue-100 p-4">
            <p className="text-xs font-semibold uppercase tracking-wider text-blue-500">Checkpoint Total</p>
            <p className="mt-1 text-2xl font-black text-blue-700">{checkpointTotal.toFixed(2)} <span className="text-sm text-blue-400">/ 10</span></p>
          </div>
          <div className="rounded-2xl bg-emerald-50 border border-emerald-100 p-4">
            <p className="text-xs font-semibold uppercase tracking-wider text-emerald-500">Weight Validity</p>
            <p className="mt-1 text-2xl font-black text-emerald-700">{totalWeight}%</p>
          </div>
        </div>
      )}

      <div className="mt-8 space-y-4">
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">Overall Feedback</label>
          <textarea
            rows={5}
            value={overallFeedback}
            onChange={(e) => setOverallFeedback(e.target.value)}
            disabled={readOnly}
            className="block w-full rounded-xl border-slate-200 shadow-sm focus:border-primary focus:ring-primary/10 sm:text-sm disabled:bg-slate-50"
            placeholder="General feedback..."
          />
        </div>

      </div>

      {!readOnly && onSubmit && (
        <div className="mt-8 flex flex-col-reverse sm:flex-row justify-end gap-3">
          {!isSubmitted && (
            <button
              type="button"
              onClick={(e) => handleSubmit(e, 'DRAFT')}
              className="inline-flex justify-center py-2.5 px-4 border border-slate-200 shadow-sm text-sm font-semibold rounded-xl text-slate-700 bg-white hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary"
            >
              Save Draft
            </button>
          )}
          <button
            type="button"
            onClick={(e) => handleSubmit(e, 'SUBMITTED')}
            className="inline-flex justify-center py-2.5 px-4 border border-transparent shadow-sm text-sm font-semibold rounded-xl text-white bg-primary hover:bg-primary/90 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary"
          >
            {isSubmitted ? 'Save Changes' : 'Submit Evaluation'}
          </button>
        </div>
      )}

      {/* Custom Confirmation Modal */}
      {showConfirmModal && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center">
          {/* Overlay */}
          <div
            className="absolute inset-0 bg-black/60 backdrop-blur-sm transition-opacity"
            onClick={handleCancelSaveAnyway}
          />

          {/* Modal Container */}
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-md mx-4 overflow-hidden border border-slate-100 transform transition-all p-6">
            <div className="text-center">
              {/* Alert Icon */}
              <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-amber-50 border border-amber-200 text-amber-500 mb-4">
                <svg
                  className="h-6 w-6 animate-bounce"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="2"
                    d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                  />
                </svg>
              </div>
              
              <h3 className="text-lg font-bold text-slate-900 mb-2">
                Incomplete Grades
              </h3>
              
              <p className="text-sm text-slate-500 mb-6">
                You have not finished entering all grades for this checkpoint. Do you want to save anyway?
              </p>
            </div>

            {/* Actions */}
            <div className="flex flex-col sm:flex-row gap-2 justify-center">
              <button
                type="button"
                onClick={handleCancelSaveAnyway}
                className="w-full sm:w-auto px-5 py-2.5 text-sm font-semibold text-slate-700 bg-slate-50 border border-slate-200/60 rounded-xl hover:bg-slate-100 hover:text-slate-800 transition-colors"
              >
                Continue Entering
              </button>
              <button
                type="button"
                onClick={handleConfirmSaveAnyway}
                className="w-full sm:w-auto px-5 py-2.5 text-sm font-bold text-white bg-primary rounded-xl hover:bg-primary/95 transition-colors"
              >
                Save Anyway
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
