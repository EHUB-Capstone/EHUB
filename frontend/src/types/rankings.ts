import type { ApiEnvelope } from './workspaceTools';

export type TeamRankingStatus = 'INCOMPLETE' | 'READY_TO_PUBLISH' | 'PUBLISHED';
export type TeamRankingEvaluationStatus = 'NOT_GRADED' | 'SUBMITTED' | 'PUBLISHED';

export interface TeamRankingSemester {
  id: string;
  semester: string;
  year: number;
  code: string;
  isActive: boolean;
}

export interface TeamRankingCheckpoint {
  checkpointId: string;
  number: number;
  title: string;
  weight: number;
  score: number | null;
  status: TeamRankingEvaluationStatus;
}

export interface TeamRankingAssessment {
  assessmentId: string;
  name: string;
  weight: number;
  score: number | null;
  status: TeamRankingEvaluationStatus;
}

export interface TeamRankingItem {
  teamId: string;
  teamName: string;
  teamCode: string;
  projectName: string;
  projectDescription: string;
  semesterGroupName: string;
  classId: string;
  classCode: string;
  courseCode: string;
  semester: string;
  year: number;
  checkpoints: TeamRankingCheckpoint[];
  assessments: TeamRankingAssessment[];
  courseTotal: number | null;
  status: TeamRankingStatus;
  completedComponentCount: number;
  publishedComponentCount: number;
  totalComponentCount: number;
  lastUpdatedAt?: string | null;
}

export interface TeamRankingList {
  activeSemester: TeamRankingSemester | null;
  selectedSemester: TeamRankingSemester | null;
  availableSemesters: TeamRankingSemester[];
  items: TeamRankingItem[];
}

export type TeamRankingResponse = ApiEnvelope<TeamRankingList>;
export type TeamRankingScoreScope = 'course' | `checkpoint:${number}`;

export interface TeamRankingViewItem extends TeamRankingItem {
  rank: number | null;
  rankingScore: number | null;
  rankingStatus: TeamRankingStatus;
}
