import type { ManagedTeam, TeamStudent } from './teamManagement';

export type StudentAssignmentMode = 'CLASS' | 'TEAM';

export interface AssignableStudent extends TeamStudent {
  classCode?: string | null;
  source?: 'CLASS_ROSTER' | 'USER_DIRECTORY' | 'IMPORTED';
}

export interface StudentAssignmentDraft {
  mode: StudentAssignmentMode;
  classId: string;
  teamId: string;
  studentIds: string[];
}

export type StudentAssignmentField = 'classId' | 'teamId' | 'studentIds';

export interface StudentAssignmentValidation {
  isValid: boolean;
  errors: Partial<Record<StudentAssignmentField, string>>;
  studentsOutsideClass: string[];
  teamConflicts: Map<string, string>;
}

export interface StudentAssignmentResult {
  mode: StudentAssignmentMode;
  classId: string;
  teamId: string | null;
  assignedStudentIds: string[];
  students: AssignableStudent[];
  teams: ManagedTeam[];
}

export interface StudentDirectoryRecord {
  _id?: string;
  id?: string;
  studentProfileId?: string | { _id?: string } | null;
  student?: string | { _id?: string } | null;
  name?: string | null;
  fullName?: string | null;
  email?: string | null;
  studentId?: string | null;
  rollNumber?: string | null;
  major?: string | null;
  majorCode?: string | null;
  classId?: TeamStudent['classId'];
  classCode?: string | null;
  teamId?: TeamStudent['teamId'];
}
