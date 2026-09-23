export type CheckpointScheduleStatus = 'NotScheduled' | 'Upcoming' | 'Open' | 'Closed';
export type CheckpointSubmissionStatus = CheckpointScheduleStatus | 'Pending' | 'Draft' | 'Submitted';

export interface LecturerCheckpointClass {
  id: string;
  classCode: string;
  subjectCode: string;
  subjectName: string;
  semesterCode: string;
  year: number;
}

export interface LecturerCheckpointDefinition {
  id: string;
  courseId: string;
  number: number;
  title: string;
  shortDescription?: string | null;
}

export interface ClassCheckpointSchedule {
  id?: string | null;
  classId: string;
  classCode: string;
  checkpointId: string;
  checkpointNumber: number;
  checkpointTitle: string;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
  status: CheckpointScheduleStatus;
  canReopen: boolean;
  reopenCount: number;
}

export interface LecturerCheckpointFile {
  id: string;
  originalName: string;
  uploadedAtUtc: string;
}

export interface LecturerCheckpointSubmission {
  classId: string;
  classCode: string;
  teamId: string;
  teamName: string;
  checkpointId: string;
  checkpointNumber: number;
  checkpointTitle: string;
  status: CheckpointSubmissionStatus;
  latestSubmissionAtUtc?: string | null;
  earliestSubmittedFile?: LecturerCheckpointFile | null;
}

export interface LecturerCheckpointOverview {
  serverTimeUtc: string;
  classes: LecturerCheckpointClass[];
  checkpoints: LecturerCheckpointDefinition[];
  schedules: ClassCheckpointSchedule[];
  submissions: LecturerCheckpointSubmission[];
}

export interface GetLecturerCheckpointsParams {
  semester?: string;
  year?: number;
  subjectCode?: string;
  status?: string;
  search?: string;
  classId?: string;
  checkpointId?: string;
  checkpointNumber?: number;
}

export interface SaveClassCheckpointSchedulePayload {
  startDateUtc: string;
  endDateUtc: string;
}

export interface BulkSaveClassCheckpointSchedulePayload extends SaveClassCheckpointSchedulePayload {
  checkpointNumber: number;
  semester?: string;
  year?: number;
  subjectCode?: string;
  status?: string;
  search?: string;
  expectedClassIds: string[];
}
