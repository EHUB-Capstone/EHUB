import type { AcademicOverviewMetrics, AcademicOverviewScope } from '../types/dashboard';
import { parseApiError } from './apiError.ts';

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

export const ACADEMIC_OVERVIEW_ERROR_FALLBACK = "We couldn't load your dashboard. Please try again.";

/**
 * Shows the reason the server gave when it is a business error (it carries an error code), and a generic message for
 * network and unexpected failures so no technical detail reaches the user.
 */
export const resolveOverviewErrorMessage = (error: unknown): string => {
  if (error === null || typeof error !== 'object') return ACADEMIC_OVERVIEW_ERROR_FALLBACK;
  const parsed = parseApiError(error, ACADEMIC_OVERVIEW_ERROR_FALLBACK);
  return parsed.code && parsed.message ? parsed.message : ACADEMIC_OVERVIEW_ERROR_FALLBACK;
};

/** Explains why the overview is not about the active semester, or returns null when there is nothing to explain. */
export const semesterScopeNotice = (
  scope: Pick<AcademicOverviewScope, 'isActiveSemester' | 'semesterName' | 'semesterCode'>,
): string | null => {
  if (scope.isActiveSemester !== false) return null;
  const label = scope.semesterName || scope.semesterCode;
  return `No semester is currently active. Showing ${label || 'the most recent semester'}.`;
};
