export type EntityReference = string | { _id?: string; id?: string; name?: string } | null | undefined;

export interface TeamStudent {
  _id: string;
  fullName: string;
  rollNumber?: string | null;
  email?: string | null;
  major?: string | null;
  classId?: EntityReference;
  teamId?: EntityReference;
  userId?: EntityReference;
}

export interface TeamProject {
  _id?: string;
  name: string;
  description?: string | null;
  status?: string | null;
  startupField?: string | null;
  problem?: string | null;
  solution?: string | null;
}

export interface TeamMember {
  studentId: string | TeamStudent;
  roleInTeam?: string;
  joinedAt?: string;
}

export interface ManagedTeam {
  _id: string;
  classId?: EntityReference;
  teamCode?: string | null;
  teamName: string;
  groupName?: string | null;
  description?: string | null;
  status?: string | null;
  leaderId?: EntityReference;
  members?: TeamMember[];
  teamMembers?: TeamMember[];
  memberIds?: string[];
  project?: TeamProject | null;
  projectName?: string | null;
  projectDescription?: string | null;
  projectStatus?: string | null;
  chatGroupId?: EntityReference;
  mentorId?: EntityReference;
  lectureId?: EntityReference;
  rejectReason?: string | null;
}

export interface TeamClassOption {
  id: string;
  code: string;
  name?: string;
}

export interface TeamDraft {
  teamName: string;
  classId: string;
  memberIds: string[];
  leaderId: string;
  description: string;
  projectName: string;
  projectDescription: string;
  projectStatus: string;
  startupField: string;
}

export type TeamDraftField = 'teamName' | 'classId' | 'memberIds' | 'leaderId' | 'projectName' | 'projectDescription';

export interface TeamDraftValidation {
  isValid: boolean;
  errors: Partial<Record<TeamDraftField, string>>;
  conflicts: Map<string, string>;
}
