export type SubmissionAnalyticsStatus = 'Submitted' | 'NotSubmitted' | 'Missing';

export interface SubmissionAnalyticsFilters {
  semester?: string;
  year?: number;
  classId?: string;
  teamId?: string;
  checkpointNumber?: number;
}

export interface SubmissionAnalyticsItem {
  teamId: string;
  teamName: string;
  classId: string;
  classCode: string;
  courseCode: string;
  semesterCode: string;
  hasWorkspace: boolean;
  checkpointId: string;
  checkpointNumber: number;
  checkpointTitle: string;
  courseWeight: number;
  deadlineUtc: string | null;
  submittedAtUtc: string | null;
  status: SubmissionAnalyticsStatus;
}

export interface SubmissionAnalyticsResponse {
  serverTimeUtc: string;
  expectedCount: number;
  submittedCount: number;
  notSubmittedCount: number;
  missingCount: number;
  items: SubmissionAnalyticsItem[];
}
