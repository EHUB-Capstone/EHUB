import type { EvaluationGradingRecord } from '../types/evaluationGrading';
import type { SubmissionAnalyticsItem } from '../types/submissionAnalytics';
import type { WorkspaceOption } from '../types/workspaceTools';

export const submissionKey = (teamId: string, checkpointNumber: number) => `${teamId}:${checkpointNumber}`;

export function matchesSubmissionStatus(item: SubmissionAnalyticsItem | undefined, status: string): boolean {
  if (!status) return true;
  if (!item) return false;
  return status === 'NotSubmitted' ? item.status !== 'Submitted' : item.status === status;
}

export function includeTeamsWithoutWorkspace(
  records: EvaluationGradingRecord[],
  items: SubmissionAnalyticsItem[],
  teams: WorkspaceOption[],
): EvaluationGradingRecord[] {
  const teamById = new Map(teams.map(team => [team.teamId, team]));
  const existingKeys = new Set(records.map(record => submissionKey(record.team.teamId, record.checkpoint.number)));
  return [...records, ...items.filter(item => !item.hasWorkspace && !existingKeys.has(submissionKey(item.teamId, item.checkpointNumber)))
    .map(item => ({
      key: submissionKey(item.teamId, item.checkpointNumber),
      team: teamById.get(item.teamId) || {
        teamId: item.teamId, teamName: item.teamName, classId: item.classId, classCode: item.classCode,
        courseCode: item.courseCode, semester: item.semesterCode, hasWorkspace: false,
        accessMode: 'READ_ONLY' as const, isArchived: false, isCurrent: false,
      },
      checkpoint: { number: item.checkpointNumber, title: item.checkpointTitle, courseWeight: item.courseWeight },
      evaluation: null,
      status: 'NOT_GRADED' as const,
    }))];
}
