export type MentorType = 'Enterprise' | 'Academic';

export interface MentorImportRowPreview {
  rowNumber: number;
  sheetName: string;
  mentorType: MentorType;
  fullName: string;
  email: string;
  fptEmail?: string | null;
  missingFields: string[];
  status: string;
  isValid: boolean;
  message?: string | null;
}

export interface MentorImportPreview {
  sessionId: string;
  semesterId: string;
  totalRows: number;
  createCount: number;
  updateCount: number;
  addToSemesterCount: number;
  needsCompletionCount: number;
  completeDraftCount: number;
  errorCount: number;
  canCommit: boolean;
  rows: MentorImportRowPreview[];
}

export interface MentorImportCommitResult {
  createdCount: number;
  updatedCount: number;
  semesterAssignmentCount: number;
  draftSavedCount: number;
  draftCompletedCount: number;
}

export interface MentorAllocationRowPreview {
  teamId: string;
  teamCode: string;
  teamName: string;
  classId: string;
  classCode: string;
  mentorType: MentorType;
  mentorProfileId: string;
  mentorName: string;
  mentorEmail: string;
  resultingSemesterLoad: number;
  /** "Retained" keeps the previous semester's mentor for a continuing team; "Allocated" is newly chosen. */
  source?: 'Retained' | 'Allocated';
}

export type MentorAllocationSkipReason =
  | 'MentorNotActiveInSemester'
  | 'MentorUnavailable'
  | 'SlotAlreadyFilled'
  | 'NoContinuedTeam';

export interface MentorAllocationSkipped {
  teamId: string | null;
  teamCode: string;
  teamName: string;
  classCode: string;
  sourceTeamCode: string;
  mentorType: MentorType;
  mentorProfileId: string;
  mentorName: string;
  mentorEmail: string;
  reason: MentorAllocationSkipReason;
  message: string;
}

export interface MentorAllocationPreview {
  sessionId: string;
  semesterId: string;
  seed: number;
  teamCount: number;
  missingEnterpriseCount: number;
  missingAcademicCount: number;
  retainedCount?: number;
  canCommit: boolean;
  warnings: string[];
  assignments: MentorAllocationRowPreview[];
  skipped?: MentorAllocationSkipped[];
}

export interface MentorAllocationCommitResult {
  createdCount: number;
  skippedCount: number;
}
