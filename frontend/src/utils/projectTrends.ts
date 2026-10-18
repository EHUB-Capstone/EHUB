import type { ProjectDataTrendRow, ProjectDataTrends } from '../types/projectData';

export const TREND_LEVELS = ['semester', 'subject', 'class'] as const;
export type TrendLevel = (typeof TREND_LEVELS)[number];

export const TREND_LEVEL_LABELS: Record<TrendLevel, string> = {
  semester: 'Semester',
  subject: 'Subject',
  class: 'Class',
};

export const TREND_SCORE_SCALE = 10;

export function trendRows(trends: ProjectDataTrends, level: TrendLevel): ProjectDataTrendRow[] {
  return level === 'semester' ? trends.semesters : level === 'subject' ? trends.subjects : trends.classes;
}

export const trendRowKey = (row: ProjectDataTrendRow): string =>
  [row.semesterCode, row.subjectCode ?? '', row.classId ?? ''].join('|');

export const formatScore = (value: number | null): string => (value === null ? '-' : value.toFixed(2));

export type TrendDirection = 'up' | 'down' | 'flat' | 'none';

export function trendDirection(change: number | null): TrendDirection {
  if (change === null) return 'none';
  if (change > 0) return 'up';
  if (change < 0) return 'down';
  return 'flat';
}

/** "+1.50", "-0.30", "0.00"; the sign is always written so the change never relies on color alone. */
export function formatScoreChange(change: number | null): string {
  if (change === null) return '-';
  const text = Math.abs(change).toFixed(2);
  return change > 0 ? `+${text}` : change < 0 ? `-${text}` : text;
}

/** "+2", "-1", "0" for a count change; "-" when there is no earlier semester to compare with. */
export function formatCountChange(change: number | null): string {
  if (change === null) return '-';
  return change > 0 ? `+${change}` : String(change);
}

/** Bar length of an average out of 10, so two semesters can be compared at a glance. */
export function scoreBarWidth(score: number | null): number {
  if (score === null || score <= 0) return 0;
  return Math.min(100, Math.max(2, Math.round((score / TREND_SCORE_SCALE) * 100)));
}

// --- Chart models -----------------------------------------------------------------------------------------------

/** Categorical colors a chart can use. A longer list is cut to this many series, never given generated hues. */
export const MAX_CHART_SERIES = 6;

export interface ScoreSeries {
  key: string;
  label: string;
  /** Index into CHART_SERIES; null for the single brand-colored line. */
  slot: number | null;
}

export type ScoreChartRow = Record<string, string | number | null>;

export interface ScoreChartModel {
  data: ScoreChartRow[];
  series: ScoreSeries[];
  /** Subjects left out of the chart because only MAX_CHART_SERIES fit; they stay in the table view. */
  hiddenSubjects: number;
}

/**
 * One color slot per subject. The subject list of the user's whole scope (not the filtered response) decides the
 * order, so filtering a subject out never repaints the others. Subjects outside the first slots take the free ones.
 */
export function subjectColorSlots(subjects: readonly string[], scopeOrder: readonly string[] = []): Map<string, number> {
  const order = [...new Set([...scopeOrder, ...[...subjects].sort((a, b) => a.localeCompare(b))])];
  const slots = new Map<string, number>();
  const wanted = new Set(subjects);
  const taken = new Set<number>();

  order.forEach((code, index) => {
    if (index < MAX_CHART_SERIES && wanted.has(code)) {
      slots.set(code, index);
      taken.add(index);
    }
  });
  let free = 0;
  for (const code of order) {
    if (!wanted.has(code) || slots.has(code)) continue;
    while (taken.has(free)) free += 1;
    if (free >= MAX_CHART_SERIES) break;
    slots.set(code, free);
    taken.add(free);
  }
  return slots;
}

const pivot = (rows: ProjectDataTrendRow[], keyOf: (row: ProjectDataTrendRow) => string | null): ScoreChartRow[] => {
  const bySemester = new Map<string, ScoreChartRow>();
  for (const row of rows) {
    const key = keyOf(row);
    if (key === null) continue;
    const entry = bySemester.get(row.semesterCode) ?? { semester: row.semesterCode };
    entry[key] = row.averageScore;
    entry[`${key}:graded`] = row.gradedGroups;
    entry[`${key}:total`] = row.totalGroups;
    entry[`${key}:change`] = row.averageScoreChange;
    bySemester.set(row.semesterCode, entry);
  }
  return [...bySemester.values()];
};

/** Rows of the average-score line chart, oldest semester first. A semester without a score stays null (a gap). */
export function toScoreChartData(
  trends: ProjectDataTrends,
  level: TrendLevel,
  scopeSubjectOrder: readonly string[] = [],
): ScoreChartModel {
  if (level !== 'subject') {
    return {
      data: pivot(trends.semesters, () => 'score'),
      series: [{ key: 'score', label: 'Average score', slot: null }],
      hiddenSubjects: 0,
    };
  }

  const groups = new Map<string, number>();
  for (const row of trends.subjects) {
    if (row.subjectCode) groups.set(row.subjectCode, (groups.get(row.subjectCode) ?? 0) + row.totalGroups);
  }
  const shown = [...groups.entries()]
    .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
    .slice(0, MAX_CHART_SERIES)
    .map(([code]) => code);
  const slots = subjectColorSlots(shown, scopeSubjectOrder);
  const series = shown
    .filter(code => slots.has(code))
    .sort((a, b) => slots.get(a)! - slots.get(b)!)
    .map(code => ({ key: code, label: code, slot: slots.get(code)! }));
  const keys = new Set(series.map(item => item.key));

  return {
    data: pivot(trends.subjects, row => (row.subjectCode && keys.has(row.subjectCode) ? row.subjectCode : null)),
    series,
    hiddenSubjects: groups.size - series.length,
  };
}

export interface OutcomeChartRow {
  semester: string;
  Potential: number;
  'Internally Funded': number;
  'Externally Funded': number;
  Awarded: number;
}

export function toOutcomeChartData(trends: ProjectDataTrends): OutcomeChartRow[] {
  return trends.semesters.map(row => ({
    semester: row.semesterCode,
    Potential: row.potentialGroups,
    'Internally Funded': row.internallyFundedGroups,
    'Externally Funded': row.externallyFundedGroups,
    Awarded: row.awardedGroups,
  }));
}

export interface LatestSemesterSummary {
  semesterCode: string;
  averageScore: number | null;
  scoreChange: number | null;
  gradedGroups: number;
  totalGroups: number;
  /** Share of groups with an official score, 0-100. */
  coveragePercent: number;
  potentialGroups: number;
  potentialChange: number | null;
  /** Average score of each semester up to the latest, oldest first; null where nothing is graded. */
  scoreHistory: { semester: string; score: number | null }[];
  potentialHistory: { semester: string; count: number }[];
}

/** What the KPI tiles show: the newest semester, even when nothing in it is graded yet. */
export function latestSemesterSummary(trends: ProjectDataTrends): LatestSemesterSummary | null {
  const latest = trends.semesters.at(-1);
  if (!latest) return null;
  return {
    semesterCode: latest.semesterCode,
    averageScore: latest.averageScore,
    scoreChange: latest.averageScoreChange,
    gradedGroups: latest.gradedGroups,
    totalGroups: latest.totalGroups,
    coveragePercent: latest.totalGroups > 0 ? Math.round((latest.gradedGroups / latest.totalGroups) * 100) : 0,
    potentialGroups: latest.potentialGroups,
    potentialChange: latest.potentialChange,
    scoreHistory: trends.semesters.map(row => ({ semester: row.semesterCode, score: row.averageScore })),
    potentialHistory: trends.semesters.map(row => ({ semester: row.semesterCode, count: row.potentialGroups })),
  };
}
