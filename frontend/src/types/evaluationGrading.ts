import type { WorkspaceOption } from './workspaceTools';

export type EvaluationGradingStatus = 'DRAFT' | 'SUBMITTED' | 'PUBLISHED' | 'NOT_GRADED';

export interface EvaluationGradingUser {
  _id: string;
  name: string;
  role?: string;
}

export interface EvaluationCriterionScore {
  criterionKey: string;
  criterionName: string;
  score?: number | null;
  comment?: string | null;
}

export interface EvaluationMemberScore {
  studentId: string;
  score: number;
  isOverridden: boolean;
}

export interface EvaluationTeamMember {
  studentId: string;
  userId?: string | null;
  fullName: string;
  rollNumber: string;
  majorCode?: string;
  roleInTeam?: string;
}

export interface EvaluationTeam extends WorkspaceOption {
  teamCode?: string;
  projectName?: string;
  projectDescription?: string;
  semesterGroupName?: string;
  members?: EvaluationTeamMember[];
}

export interface CheckpointEvaluation {
  _id: string;
  lecturerId: EvaluationGradingUser;
  evaluatorRole: string;
  status: Exclude<EvaluationGradingStatus, 'NOT_GRADED'>;
  checkpointTotal?: number | null;
  overallFeedback?: string | null;
  updatedAt: string;
  rubricScores: EvaluationCriterionScore[];
  memberScores?: EvaluationMemberScore[];
}

export interface EvaluationCheckpoint {
  number: number;
  title: string;
  shortDescription?: string | null;
  courseWeight: number;
  rubrics?: Array<{
    key: string;
    label: string;
    description?: string | null;
    weight: number;
    maxScore: number;
  }>;
}

export interface CourseAssessmentEvaluation {
  assessmentId: string;
  name: string;
  weight: number;
  evaluationId?: string | null;
  evaluatorId?: string | null;
  score?: number | null;
  memberScores?: EvaluationMemberScore[];
  status: EvaluationGradingStatus;
  updatedAt?: string | null;
}

export interface CourseAssessmentEvaluationList {
  assessments: CourseAssessmentEvaluation[];
}

export interface CheckpointEvaluationSummary {
  checkpoint: EvaluationCheckpoint;
  evaluations: CheckpointEvaluation[];
  summary: {
    evaluationCount: number;
    submittedCount: number;
    averageScore?: number | null;
  };
}

export interface EvaluationGradingCheckpointSnapshot {
  checkpoint: EvaluationCheckpoint;
  evaluations: CheckpointEvaluation[];
}

export interface EvaluationGradingTeamSnapshot {
  teamId: string;
  teamCode: string;
  projectName?: string | null;
  projectDescription?: string | null;
  semesterGroupName?: string | null;
  members: EvaluationTeamMember[];
  checkpoints: EvaluationGradingCheckpointSnapshot[];
  assessments: CourseAssessmentEvaluation[];
}

export interface EvaluationGradingBatchResponse {
  teams: EvaluationGradingTeamSnapshot[];
}

export interface EvaluationReportTeamScope {
  teamId: string;
  checkpointNumbers: number[];
}

export interface EvaluationReportExportRequest {
  teams: EvaluationReportTeamScope[];
}

export interface EvaluationGradingRecord {
  key: string;
  team: EvaluationTeam;
  checkpoint: EvaluationCheckpoint;
  evaluation: CheckpointEvaluation | null;
  status: EvaluationGradingStatus;
}

export interface EvaluationGradingFilters {
  search?: string;
  classId?: string;
  teamId?: string;
  checkpoint?: string;
  status?: string;
}
