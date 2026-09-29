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
  score: number;
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
  semesterGroupName?: string;
  members?: EvaluationTeamMember[];
}

export interface CheckpointEvaluation {
  _id: string;
  lecturerId: EvaluationGradingUser;
  evaluatorRole: string;
  status: Exclude<EvaluationGradingStatus, 'NOT_GRADED'>;
  checkpointTotal: number;
  overallFeedback?: string | null;
  updatedAt: string;
  rubricScores: EvaluationCriterionScore[];
  memberScores: EvaluationMemberScore[];
}

export interface EvaluationCheckpoint {
  number: number;
  title: string;
  shortDescription?: string | null;
  courseWeight: number;
}

export interface CourseAssessmentEvaluation {
  assessmentId: string;
  name: string;
  weight: number;
  evaluationId?: string | null;
  score?: number | null;
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
    averageScore: number;
  };
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
