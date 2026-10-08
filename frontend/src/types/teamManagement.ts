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
  problem?: string | null;
  solution?: string | null;
}

export interface TeamMember {
  studentId: string | TeamStudent;
  roleInTeam?: string;
  joinedAt?: string;
}

/** Server-evaluated major structure of a team: needs at least one GROUP_1 and one GROUP_2 major. */
export interface TeamMajorComposition {
  isValid: boolean;
  missingGroups: string[];
  membersWithoutValidMajor: string[];
  message: string | null;
}

export interface TeamMajorWarning {
  teamId: string;
  teamCode: string;
  teamName: string;
  majorComposition: TeamMajorComposition;
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
  hasChatGroup?: boolean;
  majorComposition?: TeamMajorComposition | null;
  teamLineageId?: string | null;
  isContinued?: boolean;
  continuedFromSemesterCode?: string | null;
  continuedFromClassCode?: string | null;
  chatGroupId?: EntityReference;
  mentorId?: EntityReference;
  lectureId?: EntityReference;
  rejectReason?: string | null;
  rowVersion?: string;
  isProposal?: boolean;
  approvedTeamId?: EntityReference;
  linkedProposal?: ManagedTeam;
  currentMentorAssignment?: MentorAssignment | null;
  currentMentorAssignments?: MentorAssignment[];
}

export interface MentorAssignment {
  assignmentId: string;
  teamId: string;
  mentor: {
    mentorProfileId: string;
    userId: string;
    fullName: string;
    email: string;
    organization?: string | null;
    mentorType: 'Enterprise' | 'Academic';
    department?: string | null;
    jobTitle?: string | null;
    contractType?: string | null;
  };
  slot: 'Enterprise' | 'Academic';
  status: string;
  assignedAtUtc: string;
  endedAtUtc?: string | null;
  note?: string | null;
}

export interface MentorCandidate {
  mentor: MentorAssignment['mentor'];
  activeTeamCount: number;
}

export interface TeamClassOption {
  id: string;
  code: string;
  name?: string;
}

export interface CreateClassManagerTeamRequest {
  teamName: string;
  memberStudentIds: string[];
  leaderStudentId: string;
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
}

export type TeamDraftField = 'teamName' | 'classId' | 'memberIds' | 'leaderId' | 'projectName' | 'projectDescription';

export interface TeamDraftValidation {
  isValid: boolean;
  errors: Partial<Record<TeamDraftField, string>>;
  conflicts: Map<string, string>;
}
