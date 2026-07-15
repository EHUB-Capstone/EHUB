// @ts-nocheck
// frontend/src/components/evaluation/PerformanceLevelBadge.jsx
/**
 * PerformanceLevelBadge
 *
 * A reusable badge that displays the performance level of an evaluation
 * criterion or overall score WITHOUT revealing any numeric value.
 *
 * Props:
 *  - level   : "Poor" | "Fair" | "Good" | "Excellent" | "Unscored"
 *  - label   : full descriptive label, e.g. "Meets Standard"
 *  - color   : "red" | "yellow" | "green" | "blue" | "gray"
 *  - size    : "sm" | "md" | "lg"  (default "md")
 *  - showLabel : boolean — show the label text alongside the level (default true)
 *  - className : extra Tailwind classes
 */

const LEVEL_CONFIG = {
  Excellent: {
    dot: 'bg-blue-500',
    badge: 'bg-blue-100 text-blue-800 border border-blue-300',
    glow: 'shadow-blue-200',
    icon: '⭐',
  },
  Good: {
    dot: 'bg-green-500',
    badge: 'bg-green-100 text-green-800 border border-green-300',
    glow: 'shadow-green-200',
    icon: '✅',
  },
  Fair: {
    dot: 'bg-yellow-500',
    badge: 'bg-yellow-100 text-yellow-800 border border-yellow-300',
    glow: 'shadow-yellow-200',
    icon: '⚠️',
  },
  Poor: {
    dot: 'bg-red-500',
    badge: 'bg-red-100 text-red-800 border border-red-300',
    glow: 'shadow-red-200',
    icon: '🔴',
  },
  Unscored: {
    dot: 'bg-slate-400',
    badge: 'bg-slate-100 text-slate-600 border border-slate-300',
    glow: 'shadow-slate-200',
    icon: '—',
  },
};

const SIZE_CLASSES = {
  sm: {
    badge: 'text-[11px] px-2 py-0.5 gap-1',
    dot: 'w-1.5 h-1.5',
    label: 'text-[10px]',
  },
  md: {
    badge: 'text-xs px-2.5 py-1 gap-1.5',
    dot: 'w-2 h-2',
    label: 'text-[11px]',
  },
  lg: {
    badge: 'text-sm px-3 py-1.5 gap-2',
    dot: 'w-2.5 h-2.5',
    label: 'text-xs',
  },
};

const PerformanceLevelBadge = ({
  level = 'Unscored',
  label = '',
  color,       // optional override — color is derived from level by default
  size = 'md',
  showLabel = true,
  className = '',
}) => {
  const config = LEVEL_CONFIG[level] || LEVEL_CONFIG.Unscored;
  const sizes = SIZE_CLASSES[size] || SIZE_CLASSES.md;

  return (
    <span
      className={[
        'inline-flex items-center font-semibold rounded-full shadow-sm',
        config.badge,
        sizes.badge,
        className,
      ].join(' ')}
    >
      {/* Pulsing dot indicator */}
      <span className="relative flex-shrink-0">
        <span
          className={[
            'rounded-full animate-pulse opacity-70',
            config.dot,
            sizes.dot,
            'block',
          ].join(' ')}
        />
      </span>

      {/* Level name */}
      <span className="font-bold">{level}</span>

      {/* Descriptive label */}
      {showLabel && label && (
        <span className={['opacity-75 font-medium', sizes.label].join(' ')}>
          — {label}
        </span>
      )}
    </span>
  );
};

/**
 * MentorRubricRow — A single criterion row rendered for the Mentor view.
 * Shows: criterion name, weight badge, performance level badge, and comment.
 * Hides: score, weightedScore.
 */
export const MentorRubricRow = ({ criterionName, weight, level, label, comment }) => (
  <div className="flex flex-col sm:flex-row sm:items-start gap-3 rounded-2xl border border-slate-200 bg-white p-4">
    {/* Left — criterion info */}
    <div className="flex-1 min-w-0">
      <p className="font-semibold text-slate-900 text-sm">{criterionName}</p>
      <span className="inline-flex rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-bold text-slate-600 mt-1">
        Weight: {weight}%
      </span>
    </div>

    {/* Centre — level badge */}
    <div className="flex-shrink-0 flex items-start pt-0.5">
      <PerformanceLevelBadge level={level} label={label} size="md" />
    </div>

    {/* Right — comment */}
    {comment && (
      <div className="flex-1 min-w-0">
        <p className="text-xs text-slate-500 font-semibold uppercase tracking-wider mb-1">
          Comment
        </p>
        <p className="text-sm text-slate-700 whitespace-pre-wrap">{comment}</p>
      </div>
    )}
  </div>
);

/**
 * MentorEvaluationCard — Full evaluation card shown to Mentors.
 * Displays overall performance level + per-criterion level badges + feedback.
 * No numeric score is rendered anywhere.
 */
export const MentorEvaluationCard = ({ evaluation }) => {
  const rubricScores = evaluation?.rubricScores || [];
  const overallLevel = evaluation?.overallLevel || 'Unscored';
  const overallLabel = evaluation?.overallLabel || '';

  return (
    <div className="rounded-2xl border border-slate-200 bg-slate-50 overflow-hidden">
      {/* Card header */}
      <div className="flex items-center justify-between gap-4 p-4 bg-white border-b border-slate-200">
        <div>
          <p className="font-semibold text-slate-900">{evaluation?.lecturerId?.name || 'Evaluator'}</p>
          <p className="text-xs text-slate-400 mt-0.5">
            {evaluation?.evaluatorRole} ·{' '}
            {evaluation?.updatedAt ? new Date(evaluation.updatedAt).toLocaleString() : ''}
          </p>
        </div>

        <div className="flex flex-col items-end gap-1.5">
          <span
            className={`inline-flex rounded-full px-2 py-0.5 text-[11px] font-bold ${
              evaluation?.status === 'SUBMITTED' || evaluation?.status === 'PUBLISHED'
                ? 'bg-emerald-50 text-emerald-700'
                : 'bg-amber-50 text-amber-700'
            }`}
          >
            {evaluation?.status || 'DRAFT'}
          </span>
          {/* Overall performance badge — NO numeric score */}
          <PerformanceLevelBadge level={overallLevel} label={overallLabel} size="md" />
        </div>
      </div>

      {/* Per-criterion level badges */}
      {rubricScores.length > 0 && (
        <div className="p-4 space-y-3">
          <p className="text-xs font-semibold uppercase tracking-wider text-slate-400">
            Criteria Performance
          </p>
          <div className="space-y-2">
            {rubricScores.map((item) => (
              <MentorRubricRow
                key={item.criterionKey}
                criterionName={item.criterionName}
                weight={item.weight}
                level={item.level}
                label={item.label}
                comment={item.comment}
              />
            ))}
          </div>
        </div>
      )}

      {/* Feedback text — fully visible to Mentor */}
      {evaluation?.overallFeedback && (
        <div className="p-4 border-t border-slate-200 space-y-2 text-sm">
          <p className="text-slate-600 whitespace-pre-wrap">
            <span className="font-semibold text-slate-700">Overall: </span>
            {evaluation.overallFeedback}
          </p>
        </div>
      )}
    </div>
  );
};

export default PerformanceLevelBadge;
