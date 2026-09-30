import type {
  CheckpointEvaluation,
  EvaluationGradingFilters,
  EvaluationGradingRecord,
  EvaluationGradingStatus,
} from '../types/evaluationGrading';
import type { WorkspaceOption } from '../types/workspaceTools';
import { parseWorkspaceSemester } from './workspaceHub.ts';

export const evaluationRankingRoles = ['ADMIN', 'LECTURER'] as const;

export interface WeightedCourseComponent {
  weight: number;
  score: number | null | undefined;
}

export function calculateWeightedCourseScore(
  components: WeightedCourseComponent[],
): { score: number; complete: boolean } {
  const result = components.reduce((current, component) => ({
    score: current.score + (component.score === null || component.score === undefined
      ? 0
      : Number(component.score) * Number(component.weight || 0) / 100),
    complete: current.complete && component.score !== null && component.score !== undefined,
  }), { score: 0, complete: true });
  return { ...result, score: Math.round((result.score + Number.EPSILON) * 100) / 100 };
}

export function canAccessEvaluationRankings(role: string | null | undefined): boolean {
  return evaluationRankingRoles.includes(String(role || '').trim().toUpperCase() as typeof evaluationRankingRoles[number]);
}

export function resolveActiveEvaluationSemester(teams: WorkspaceOption[]): {
  semester: string;
  year: string;
} | null {
  const activeTeam = teams.find(team => team.isCurrent);
  return activeTeam ? parseWorkspaceSemester(activeTeam.semester) : null;
}

export function filterTeamsBySemester(
  teams: WorkspaceOption[],
  semester: string,
  year: string,
): WorkspaceOption[] {
  return teams.filter(team => {
    const parsed = parseWorkspaceSemester(team.semester);
    return (
      (semester === 'all' || parsed?.semester === semester.toUpperCase()) &&
      (year === 'all' || parsed?.year === year)
    );
  });
}

export function filterEvaluationRecords(
  records: EvaluationGradingRecord[],
  filters: EvaluationGradingFilters,
): EvaluationGradingRecord[] {
  const search = filters.search?.trim().toLowerCase() || '';

  return records.filter(record => {
    const searchableValues = [
      record.team.teamName,
      record.team.teamCode,
      record.team.projectName,
      record.team.projectDescription,
      record.team.semesterGroupName,
      record.team.classCode,
      record.team.courseCode,
      record.team.semester,
      record.checkpoint.title,
      record.evaluation?.lecturerId?.name,
      record.evaluation?.overallFeedback,
      ...(record.team.members?.flatMap(member => [member.fullName, member.rollNumber]) || []),
      ...(record.evaluation?.rubricScores.flatMap(score => [score.criterionName, score.comment]) || []),
    ];

    return (
      (!search || searchableValues.some(value => String(value || '').toLowerCase().includes(search))) &&
      (!filters.classId || record.team.classId === filters.classId) &&
      (!filters.teamId || record.team.teamId === filters.teamId) &&
      (!filters.checkpoint || String(record.checkpoint.number) === filters.checkpoint) &&
      (!filters.status || record.status === filters.status)
    );
  });
}

export function selectLatestOfficialEvaluation(
  evaluations: CheckpointEvaluation[],
): CheckpointEvaluation | null {
  return evaluations
    .filter(evaluation => evaluation.status === 'SUBMITTED' || evaluation.status === 'PUBLISHED')
    .sort((left, right) => new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime())[0] || null;
}

export function resolveEvaluationMemberScore(
  evaluation: CheckpointEvaluation | null,
  studentId: string,
): { score: number | null; isOverridden: boolean } {
  if (!evaluation) return { score: null, isOverridden: false };
  const memberScore = evaluation.memberScores?.find(item => item.studentId === studentId);
  return {
    score: memberScore?.score ?? null,
    isOverridden: Boolean(memberScore?.isOverridden),
  };
}

export function evaluationStatusLabel(status: EvaluationGradingStatus): string {
  return ({
    DRAFT: 'Draft',
    SUBMITTED: 'Submitted',
    PUBLISHED: 'Published',
    NOT_GRADED: 'Not graded',
  } satisfies Record<EvaluationGradingStatus, string>)[status];
}
