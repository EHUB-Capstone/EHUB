import type { AcademicOverviewMetrics } from '../types/dashboard';

export type AcademicOverviewState = 'loading' | 'error' | 'unassigned' | 'filtered-empty' | 'ready';

interface ResolveAcademicOverviewStateInput {
  loading: boolean;
  hasError: boolean;
  hasAssignedClasses?: boolean;
  hasMatchingClasses?: boolean;
}

export const resolveAcademicOverviewState = ({
  loading,
  hasError,
  hasAssignedClasses,
  hasMatchingClasses,
}: ResolveAcademicOverviewStateInput): AcademicOverviewState => {
  if (loading) return 'loading';
  if (hasError) return 'error';
  if (!hasAssignedClasses) return 'unassigned';
  return hasMatchingClasses ? 'ready' : 'filtered-empty';
};

export const academicOverviewMetricEntries = (metrics: AcademicOverviewMetrics) => [
  { key: 'classes', title: 'Total Classes', value: metrics.totalClasses },
  { key: 'teams', title: 'Total Teams', value: metrics.totalTeams },
  { key: 'projects', title: 'Total Projects', value: metrics.totalProjects },
  { key: 'submissions', title: 'Total Submissions', value: metrics.totalSubmissions },
  { key: 'evaluations', title: 'Total Evaluations', value: metrics.totalEvaluations },
  { key: 'potential-projects', title: 'Potential Projects', value: metrics.totalPotentialProjects },
] as const;

export const percentage = (value: number, total: number): number => {
  if (total <= 0) return 0;
  return Math.min(100, Math.max(0, Math.round((value / total) * 100)));
};
