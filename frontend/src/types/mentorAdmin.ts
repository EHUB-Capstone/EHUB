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
  /** Null when the workbook is imported into the master mentor list instead of a semester. */
  semesterId: string | null;
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

/** A mentor kept in the master list without a login account yet. */
export interface IncompleteMentor {
  id: string;
  fullName: string;
  mentorType: MentorType;
  email?: string | null;
  missingFields: string[];
  updatedAtUtc: string;
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
  /** "Retained" keeps the previous semester's mentor for a continuing team; "Allocated" is newly chosen; "Manual" is a hand edit. */
  source?: 'Retained' | 'Allocated' | 'Manual';
  /** Set when this row replaces the mentor currently in the slot; that assignment ends when the preview is confirmed. */
  replacesAssignmentId?: string | null;
  replacesMentorProfileId?: string | null;
  replacesMentorName?: string | null;
  replaceReason?: string | null;
  mentorLoadBefore?: number | null;
}

/** A hand edit of one team slot. A null mentorProfileId leaves the slot empty. */
export interface MentorAllocationEdit {
  teamId: string;
  mentorType: MentorType;
  mentorProfileId: string | null;
  /** Explicitly end the mentor currently in the slot; a reason of 3 to 1000 characters is then required. */
  replace?: boolean;
  reason?: string;
}

/** A mentor already assigned to a team slot. `replaced` is true when the preview ends this assignment. */
export interface MentorAllocationExisting {
  assignmentId: string;
  teamId: string;
  teamCode: string;
  teamName: string;
  classId: string;
  classCode: string;
  subjectCode: string;
  mentorType: MentorType;
  mentorProfileId: string;
  mentorName: string;
  replaced: boolean;
}

export interface MentorAllocationConflict {
  teamId: string | null;
  teamCode: string;
  teamName: string;
  mentorType: MentorType;
  currentAssignmentId: string | null;
  currentMentorProfileId: string | null;
  currentMentorName: string | null;
  proposedMentorProfileId: string | null;
  proposedMentorName: string | null;
  /** "SlotOccupied": resubmit the edit with replace to swap the mentor. "EditRejected": the edit was not applied. */
  kind: 'SlotOccupied' | 'EditRejected';
  message: string;
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

export type MentorAllocationStrategy = 'Balanced' | 'Random';

/** A team slot that still has no mentor after the proposed allocation. */
export interface MentorAllocationUnfilled {
  teamId: string;
  teamCode: string;
  teamName: string;
  classId: string;
  classCode: string;
  subjectCode: string;
  mentorType: MentorType;
}

export interface MentorAllocationSubjectLoad {
  subjectCode: string;
  before: number;
  added: number;
  /** Assignments that end because the mentor of a slot was replaced. */
  removed?: number;
}

/** Teams carried by an active mentor of the semester, before and after the proposed allocation. */
export interface MentorAllocationMentorLoad {
  mentorProfileId: string;
  mentorName: string;
  mentorEmail: string;
  mentorType: MentorType;
  contractType?: string | null;
  subjects: MentorAllocationSubjectLoad[];
  totalBefore: number;
  totalAfter: number;
}

export interface MentorAllocationPreview {
  sessionId: string;
  semesterId: string;
  seed: number;
  teamCount: number;
  missingEnterpriseCount: number;
  missingAcademicCount: number;
  retainedCount?: number;
  strategy?: MentorAllocationStrategy;
  unfilledEnterpriseCount?: number;
  unfilledAcademicCount?: number;
  replacementCount?: number;
  canCommit: boolean;
  warnings: string[];
  assignments: MentorAllocationRowPreview[];
  skipped?: MentorAllocationSkipped[];
  unfilled?: MentorAllocationUnfilled[];
  mentorLoads?: MentorAllocationMentorLoad[];
  conflicts?: MentorAllocationConflict[];
  existingAssignments?: MentorAllocationExisting[];
}

export interface MentorAllocationCommitResult {
  endedCount?: number;
  createdCount: number;
  skippedCount: number;
}
