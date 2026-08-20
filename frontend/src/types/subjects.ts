export type SemesterCode = 'SP' | 'SU' | 'FA';
export type SemesterStatus = 'Planned' | 'Active' | 'Completed' | 'Archived';
export type SubjectStatus = 'active' | 'disabled';

export interface SubjectDto {
  _id: string;
  subjectCode: string;
  subjectName: string;
  status: SubjectStatus;
}

export interface SemesterDto {
  id: string;
  semester: SemesterCode;
  year: number;
  status: SemesterStatus;
  startDate: string | null;
  endDate: string | null;
  completedAtUtc: string | null;
  completionReason: string | null;
  rowVersion: string;
}

export interface CurrentSemesterResponse {
  currentSemester: SemesterDto | null;
  availableYears: number[];
  isDecember: boolean;
}

export interface SemesterListResponse {
  semesters: SemesterDto[];
}

export interface ClassCreationSemesterOption {
  id: string;
  semester: SemesterCode;
  year: number;
  status: 'Active' | 'Planned';
  availability: 'Current' | 'Next';
  startDate: string | null;
  endDate: string | null;
}

export interface ClassCreationSemesterOptionsResponse {
  semesters: ClassCreationSemesterOption[];
}

export interface SemesterCompletionPreview {
  semesterId: string;
  semester: SemesterCode;
  year: number;
  status: SemesterStatus;
  draftClassCount: number;
  activeClassCount: number;
  inactiveClassCount: number;
  completedClassCount: number;
  archivedClassCount: number;
  activeEnrollmentCount: number;
  processingImportSessionCount: number;
  blockers: string[];
  rowVersion: string;
}

export interface SemesterLifecyclePayload {
  rowVersion: string;
  reason: string;
}

export interface PlanSemesterPayload {
  semester: SemesterCode;
  year: number;
  startDate: string;
  endDate: string;
}

export interface UpdateSemesterDatesPayload {
  startDate: string;
  endDate: string;
  rowVersion: string;
  reason: string;
}

export interface TeachingAssignmentDto {
  _id: string;
  classCode: string;
  subjectCode: string;
}

export interface TeachingStaffDto {
  _id: string;
  name: string;
  email: string;
  avatar?: string | null;
  role: 'LECTURER' | 'MENTOR';
  status: string;
  classCount: number;
  assignments: TeachingAssignmentDto[];
}

export interface TeachingStaffSummary {
  lecturers: number;
  mentors: number;
  assigned: number;
  unassigned: number;
  classes: number;
}
