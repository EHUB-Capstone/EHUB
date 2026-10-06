export interface TeamLineageMember {
  studentId: string;
  fullName: string;
  rollNumber: string | null;
  isLeader: boolean;
}

export interface TeamLineageTerm {
  teamId: string;
  classId: string;
  classCode: string;
  semesterId: string;
  semesterCode: string;
  teamName: string;
  projectName: string | null;
  projectStatus: string | null;
  isCurrent: boolean;
  canViewSubmissions: boolean;
  canViewScores: boolean;
  members: TeamLineageMember[];
}

export interface TeamLineage {
  teamLineageId: string;
  terms: TeamLineageTerm[];
}

export interface TeamLineageEvaluation {
  id: string;
  evaluatorRole: string;
  totalScore: number;
  maxTotalScore: number;
  overallFeedback: string | null;
}

export interface TeamLineageSubmission {
  id: string;
  checkpointId: string;
  checkpointName: string;
  title: string;
  status: string;
  versionNumber: number;
  submittedAt: string | null;
  // null when the caller may not see scores of this term.
  evaluations: TeamLineageEvaluation[] | null;
}

export interface TeamContinuityReportClass {
  classId: string;
  classCode: string;
  continuedTeamCount: number;
  dissolvedCount: number;
}

export interface TeamContinuityReport {
  semesterId: string;
  semesterCode: string;
  continuedTeamCount: number;
  continuedProjectCount: number;
  dissolvedCount: number;
  classes: TeamContinuityReportClass[];
}
