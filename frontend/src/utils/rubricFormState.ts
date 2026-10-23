export interface RubricCriterionConfig {
  key?: string;
  label?: string;
  criterionKey?: string;
  criterionName?: string;
  description?: string;
  weight?: number | string;
  maxScore?: number | string;
}

export interface RubricCriterion {
  criterionKey: string | undefined;
  criterionName: string | undefined;
  description: string;
  weight: number;
  maxScore: number;
}

export interface RubricRow extends RubricCriterion {
  score: number | string;
  comment: string;
}

interface SavedRubricScore {
  criterionKey: string;
  score?: number | string | null;
  manualScore?: number | string | null;
  comment?: string | null;
}

interface SavedMemberScore {
  studentId: string;
  score: number | string;
  isOverridden?: boolean;
}

export interface RubricEvaluationData {
  _id?: string;
  status?: string;
  updatedAt?: string | null;
  rubricScores?: SavedRubricScore[];
  overallFeedback?: string | null;
  memberScores?: SavedMemberScore[];
}

export const normalizeCriteria = (criteria: RubricCriterionConfig[] | undefined = []): RubricCriterion[] => {
  const source = Array.isArray(criteria) ? criteria : [];
  return source.map((item) => ({
    criterionKey: item.key || item.criterionKey,
    criterionName: item.label || item.criterionName,
    description: item.description || '',
    weight: Number(item.weight ?? 1),
    maxScore: Number(item.maxScore ?? 10),
  }));
};

export const initialRows = (criteria: RubricCriterion[], initialData?: RubricEvaluationData | null): RubricRow[] => {
  const byKey = new Map((initialData?.rubricScores || []).map((item) => [item.criterionKey, item]));
  return criteria.map((criterion) => {
    const existing = byKey.get(criterion.criterionKey as string);
    return {
      ...criterion,
      score: existing?.score ?? existing?.manualScore ?? '',
      comment: existing?.comment || '',
    };
  });
};

// Comparable form of everything the lecturer can edit, so opening the form and saving is not an update.
const toNumberOrNull = (value: unknown): number | null =>
  (value === '' || value == null || !Number.isFinite(Number(value)) ? null : Number(value));

export const editableSnapshot = (
  rows: RubricRow[],
  feedback: string | null | undefined,
  overrides: Record<string, string>,
): string => JSON.stringify({
  scores: rows.map((row) => [row.criterionKey, toNumberOrNull(row.score), row.comment || '']),
  feedback: feedback || '',
  members: Object.entries(overrides)
    .map(([studentId, score]) => [studentId, toNumberOrNull(score)])
    .sort(([left], [right]) => String(left).localeCompare(String(right))),
});

export const savedOverrides = (initialData?: RubricEvaluationData | null): Record<string, string> => Object.fromEntries(
  (initialData?.memberScores || [])
    .filter((item) => item.isOverridden)
    .map((item) => [item.studentId, String(item.score)]));

// Identifies which server version (and rubric configuration) the form was last synced to.
// A refetch that returns identical data produces the same key and must not touch what the lecturer is typing.
export const evaluationSyncKey = (
  initialData: RubricEvaluationData | null | undefined,
  criteria: RubricCriterion[],
): string => JSON.stringify([
  initialData?._id ?? null,
  initialData?.updatedAt ?? null,
  initialData?.status ?? null,
  criteria.map((item) => [item.criterionKey, item.weight, item.maxScore]),
]);

export type EvaluationSyncAction = 'keep' | 'apply' | 'notify';

export interface EvaluationSyncInput {
  baselineKey: string;
  incomingKey: string;
  baselineSnapshot: string;
  incomingSnapshot: string;
  currentSnapshot: string;
}

// What the form should do when the evaluation prop changes:
// keep   - same server version, leave the form alone;
// apply  - safe to replace the form (nothing unsaved, or the form already matches the new version, e.g. after saving);
// notify - the lecturer has unsaved edits and a different version arrived: keep the edits and offer a reload.
export const decideEvaluationSync = ({
  baselineKey, incomingKey, baselineSnapshot, incomingSnapshot, currentSnapshot,
}: EvaluationSyncInput): EvaluationSyncAction => {
  if (incomingKey === baselineKey) return 'keep';
  if (currentSnapshot === incomingSnapshot || currentSnapshot === baselineSnapshot) return 'apply';
  return 'notify';
};
