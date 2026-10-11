import type { MentorTagSet } from './mentorProfile';
export type SemesterCode = 'SP' | 'SU' | 'FA';
export type SemesterStatus = 'Planned' | 'Active' | 'Closing' | 'Completed' | 'Archived';
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
  blockingClasses: SemesterCompletionClassBlocker[];
  rowVersion: string;
}

export interface SemesterCompletionClassBlocker {
  classId: string;
  classCode: string;
  slug: string;
  status: 'Draft' | 'Active' | 'Inactive' | 'Completed' | 'Archived';
  activeEnrollmentCount: number;
}

export interface SemesterLifecyclePayload {
  rowVersion: string;
  reason: string;
}

export interface TransitionSemesterPayload {
  currentSemesterId: string;
  currentRowVersion: string;
  targetSemesterId: string;
  targetRowVersion: string;
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

export interface SubjectRubricLevel {
  key: string;
  label: string;
  range: string;
  description: string;
}

export interface SubjectRubricCriterion {
  key: string;
  label: string;
  description: string | null;
  weight: number | string;
  levels: SubjectRubricLevel[];
}

export interface SubjectCheckpointDraft {
  number: number;
  title: string;
  shortDescription: string | null;
  courseWeight: number | string;
  requirements: string[];
  rubrics: SubjectRubricCriterion[];
}

export interface SubjectOtherAssessmentDraft {
  _id?: string;
  id?: string;
  name: string;
  weight: number | string;
}

export interface TeachingAssignmentDto {
  _id: string;
  classCode: string;
  subjectCode: string;
}

export interface TeachingStaffDto {
  _id: string;
  userId: string | null;
  name: string;
  email: string;
  avatar?: string | null;
  role: 'LECTURER' | 'MENTOR';
  /** Only set for mentors: Enterprise = enterprise mentor, Academic = lecturer mentor. */
  mentorType?: 'Enterprise' | 'Academic' | null;
  status: 'Active' | 'Inactive' | 'Incomplete';
  userStatus: string;
  isIncomplete: boolean;
  /** A mentor without an account yet who takes part in the semester; _id is then the participation id. */
  isTemporary?: boolean;
  /** The incomplete-mentor record behind a temporary row. */
  draftId?: string | null;
  missingFields: string[];
  classCount: number;
  assignments: TeachingAssignmentDto[];
  rowVersion: string;
}

export type MentorKind = 'Enterprise' | 'Academic';

export interface TeachingStaffCandidateDto {
  userId: string;
  name: string;
  email: string;
  avatar?: string | null;
  role: 'LECTURER' | 'MENTOR';
  /** Only for mentors: Enterprise = enterprise mentor, Academic = lecturer mentor. */
  mentorType?: MentorKind | null;
  contractType?: string | null;
  /** A mentor without an account yet; userId is then the id of the incomplete-mentor record. */
  isTemporary?: boolean;
  /** Only for mentors: what lecturers filter on when they pick a mentor. */
  tags?: Partial<MentorTagSet> | null;
}

export type TeachingStaffBatchOutcome = 'Added' | 'AlreadyInList' | 'Rejected';

export interface TeachingStaffBatchItem {
  userId: string;
  outcome: TeachingStaffBatchOutcome;
  message?: string | null;
}

export interface AddTeachingStaffBatchPayload {
  semester: SemesterCode;
  year: number;
  role: 'LECTURER' | 'MENTOR';
  userIds: string[];
  /** True when userIds are incomplete-mentor records (mentors without an account yet). */
  temporary?: boolean;
}

export interface AddTeachingStaffBatchResponse {
  results: TeachingStaffBatchItem[];
  addedCount: number;
  alreadyInListCount: number;
  rejectedCount: number;
}

export interface TeachingStaffSummary {
  lecturers: number;
  mentors: number;
  assigned: number;
  unassigned: number;
  classes: number;
}
