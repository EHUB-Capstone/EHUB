export type ClassStatus = 'Draft' | 'Active' | 'Inactive' | 'Completed' | 'Archived';

export interface ClassScheduleSlot {
  dayOfWeek: number;
  slotNumber: number;
  room: string | null;
}

export interface ClassLecturerSummary {
  _id: string;
  name: string;
  email?: string | null;
}

export interface ClassMentorSummary {
  mentorProfileId: string;
  userId: string;
  fullName: string;
  email: string;
}

export interface ClassDto {
  id: string;
  slug: string;
  classCode: string;
  classIndex: number;
  courseId: string;
  subjectCode: string;
  subjectName: string;
  semesterId: string;
  semesterCode: string;
  year: number;
  primaryLecturerId: string | null;
  primaryLecturerName: string | null;
  primaryLecturerEmail: string | null;
  room: string | null;
  schedules: ClassScheduleSlot[];
  isEnrollmentMajorLocked: boolean;
  status: ClassStatus;
  statusBeforeArchive?: ClassStatus | null;
  studentCount: number;
  teamCount: number;
  mentors: ClassMentorSummary[];
  createdAtUtc: string;
  completedAtUtc?: string | null;
  completionReason?: string | null;
  rowVersion: string;
  // Transitional input only. New backend responses use schedules[].
  scheduleJson?: string | null;
}

export interface ClassViewModel extends ClassDto {
  _id: string;
  semester: string;
  lectureId: ClassLecturerSummary | null;
}

export interface ClassListResponse {
  items: ClassDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface GetClassesParams {
  semesterCode?: string;
  year?: number;
  subjectCode?: string;
  status?: ClassStatus | '';
  assignmentStatus?: 'Assigned' | 'Unassigned' | '';
  search?: string;
  page?: number;
  pageSize?: number;
  sort?: string;
}

export interface ClassRosterStudent {
  studentId: string;
  rollNumber: string;
  fullName: string;
  email: string;
  majorCode: string | null;
  profileMajorCode: string | null;
  majorVerificationStatus: string;
  memberCode: string | null;
  enrollmentStatus: string;
  teamId: string | null;
  teamName: string | null;
  isTeamLeader: boolean;
  joinedAtUtc: string;
}

export interface ClassRosterListResponse {
  items: ClassRosterStudent[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface GetClassRosterParams {
  search?: string;
  majorCode?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface ExportClassRosterParams {
  scope: 'Active' | 'History';
  search?: string;
  majorCode?: string;
  status?: 'Active' | 'Dropped' | 'Completed' | '';
}

export interface CreateBulkClassesRequest {
  courseId?: string;
  semesterId?: string;
  subjectCode?: string;
  semester?: string;
  year?: number;
  startClassIndex?: number;
  quantity?: number;
  classIndices?: number[];
  primaryLecturerId?: string;
  lecturerAssignments?: BulkClassLecturerAssignment[];
}

export interface BulkClassLecturerAssignment {
  lecturerId: string;
  classIndices: number[];
}

export interface BulkClassPreviewItem {
  classCode: string;
  classIndex: number;
  subjectCode: string;
  semesterCode: string;
  primaryLecturerId: string | null;
  primaryLecturerName: string | null;
  isValid: boolean;
  errorMessage: string | null;
}

export interface BulkClassPreviewResponse {
  items: BulkClassPreviewItem[];
  totalCount: number;
  validCount: number;
  invalidCount: number;
}

export interface ApiEnvelope<T> {
  success: boolean;
  message: string;
  code?: string | null;
  data: T;
}

export interface AddStudentToClassPayload {
  studentCode: string;
  fullName: string;
  email: string;
  majorCode: string | null;
}

export interface ClassCompletionPreview {
  classId: string;
  classCode: string;
  status: ClassStatus;
  activeEnrollmentCount: number;
  droppedEnrollmentCount: number;
  activeMentorAssignmentCount: number;
  openTeamProposalCount: number;
  openProjectDirectionCount: number;
  processingImportSessionCount: number;
  scheduledMentoringSessionCount: number;
  blockers: string[];
  warnings: string[];
  rowVersion: string;
}
