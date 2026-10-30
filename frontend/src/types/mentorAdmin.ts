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
  totalRows: number;
  createCount: number;
  updateCount: number;
  needsCompletionCount: number;
  completeDraftCount: number;
  errorCount: number;
  canCommit: boolean;
  /** Things to know before confirming, such as columns of the file that were not recognised. */
  warnings?: string[];
  rows: MentorImportRowPreview[];
}

/** A mentor kept in the master list without a login account yet. */
export interface IncompleteMentor {
  id: string;
  fullName: string;
  mentorType: MentorType;
  email?: string | null;
  missingFields: string[];
  /** Teams that use this mentor as a temporary mentor while there is no account. */
  activeTeamCount?: number;
  updatedAtUtc: string;
  /** What is known so far, so the edit form can start from it. */
  fptEmail?: string | null;
  phone?: string | null;
  dateOfBirth?: string | null;
  contractType?: string | null;
  educationLevel?: string | null;
  currentAddress?: string | null;
  organization?: string | null;
  department?: string | null;
  jobTitle?: string | null;
  /** Set after an edit that gave the mentor an email: the account now exists. */
  becameAccount?: boolean;
  teamsSwitched?: number;
  semestersMoved?: number;
}

/** Edits an incomplete mentor. The login email is not part of it: it arrives through an import. */
export interface UpdateIncompleteMentorPayload {
  fullName: string;
  /** When given, the mentor gets a login account right away. */
  email?: string | null;
  jobTitle?: string | null;
  department?: string | null;
  fptEmail?: string | null;
  phone?: string | null;
  dateOfBirth?: string | null;
  contractType?: string | null;
  educationLevel?: string | null;
  currentAddress?: string | null;
  organization?: string | null;
}

export interface IncompleteMentorList {
  mentors: IncompleteMentor[];
  pagination: { total: number; page: number; limit: number; pages: number };
}

export interface MentorImportCommitResult {
  createdCount: number;
  updatedCount: number;
  draftSavedCount: number;
  draftCompletedCount: number;
  /** Teams whose temporary mentor became the real mentor. */
  temporaryAssignmentsConverted?: number;
  /** Semesters where the mentor now appears as the account instead of a mentor without one. */
  temporarySemestersConverted?: number;
}

export interface MentorAllocationHistory {
  baseFitScore: number;
  completedSessionCount: number;
  mentoredTeamCount: number;
  achievementCount: number;
  historyBonus: number;
  achievementBonus: number;
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
  /** The mentor has no account yet; the slot is filled as a temporary mentor. */
  isTemporary?: boolean;
  resultingSemesterLoad: number;
  /** "Retained" keeps the previous semester's mentor for a continuing team; "Allocated" is newly chosen; "Manual" is a hand edit. */
  source?: 'Retained' | 'Allocated' | 'Manual';
  /** Set when this row replaces the mentor currently in the slot; that assignment ends when the preview is confirmed. */
  replacesAssignmentId?: string | null;
  replacesMentorProfileId?: string | null;
  replacesMentorName?: string | null;
  replaceReason?: string | null;
  mentorLoadBefore?: number | null;
  /** Relative fit score used to rank AI suggestions; not a probability. */
  fitScore?: number | null;
  history?: MentorAllocationHistory | null;
  /** Evidence from skills, mentoring history, achievements and workload. */
  reasons?: string[];
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

export type MentorAllocationStrategy = 'Balanced' | 'Random' | 'Ai';

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

/** One active class of a semester with how many of its mentor slots are still empty. */
export interface MentorSemesterClass {
  classId: string;
  classCode: string;
  subjectCode: string;
  lecturerName?: string | null;
  teamCount: number;
  missingEnterpriseCount: number;
  missingAcademicCount: number;
  /** Slots held by a mentor who has no account yet. */
  temporarySlotCount: number;
}

export interface MentorSemesterClassList {
  classes: MentorSemesterClass[];
}
