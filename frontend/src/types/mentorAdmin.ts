export type MentorType = 'Enterprise' | 'Academic';

export interface MentorImportRowPreview {
  rowNumber: number;
  sheetName: string;
  mentorType: MentorType;
  fullName: string;
  email: string;
  fptEmail?: string | null;
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
  errorCount: number;
  canCommit: boolean;
  rows: MentorImportRowPreview[];
}

export interface MentorImportCommitResult {
  createdCount: number;
  updatedCount: number;
  semesterAssignmentCount: number;
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
}

export interface MentorAllocationPreview {
  sessionId: string;
  semesterId: string;
  seed: number;
  teamCount: number;
  missingEnterpriseCount: number;
  missingAcademicCount: number;
  canCommit: boolean;
  warnings: string[];
  assignments: MentorAllocationRowPreview[];
}

export interface MentorAllocationCommitResult {
  createdCount: number;
  skippedCount: number;
}
