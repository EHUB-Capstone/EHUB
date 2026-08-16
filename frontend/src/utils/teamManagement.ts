import type {
  EntityReference,
  ManagedTeam,
  TeamDraft,
  TeamDraftValidation,
  TeamMember,
  TeamProject,
  TeamStudent,
} from '../types/teamManagement';
import { getTeamGroupFromMajor, TEAM_MAJOR_GROUPS } from '../constants/majors.ts';

export const TEAM_MEMBER_LIMIT = 6;
export const TEAM_MEMBER_MINIMUM = 4;

const MISSING_MAJOR_CODES = new Set(['UNDECLARED', 'MISSING', 'UNKNOWN', 'N/A', 'NA', 'NONE', 'NULL']);

type TeamMajorGroupKey = 'GROUP_1' | 'GROUP_2';

export interface TeamSelectionValidation {
  memberCount: number;
  minMembers: number;
  maxMembers: number;
  majorCount: number;
  minMajors: number;
  majorCodes: string[];
  groupMajorCodes: Record<TeamMajorGroupKey, string[]>;
  missingMajorStudents: TeamStudent[];
  unclassifiedMajorCodes: string[];
  leaderStudent: TeamStudent | null;
  hasGroupOne: boolean;
  hasGroupTwo: boolean;
  isMemberCountValid: boolean;
  isMajorRequirementValid: boolean;
  isTeamLeaderValid: boolean;
  canCreateTeam: boolean;
  warnings: string[];
  errors: string[];
}

export function normalizeTeamMajorCode(major: string | null | undefined): string {
  if (!major || typeof major !== 'string') return '';
  return major.trim().toUpperCase();
}

export function isMissingTeamMajor(major: string | null | undefined): boolean {
  const code = normalizeTeamMajorCode(major);
  return !code || MISSING_MAJOR_CODES.has(code);
}

export function validateTeamSelection(
  selectedStudents: TeamStudent[],
  leaderStudentId = '',
): TeamSelectionValidation {
  const uniqueStudents = Array.from(
    new Map(selectedStudents.map((student) => [student._id, student])).values(),
  );
  const memberCount = uniqueStudents.length;
  const missingMajorStudents = uniqueStudents.filter((student) => isMissingTeamMajor(student.major));
  const majorCodes = [...new Set(
    uniqueStudents
      .map((student) => normalizeTeamMajorCode(student.major))
      .filter(Boolean)
      .filter((code) => !MISSING_MAJOR_CODES.has(code)),
  )].sort();
  const groupMajorCodes: Record<TeamMajorGroupKey, string[]> = {
    GROUP_1: [],
    GROUP_2: [],
  };

  majorCodes.forEach((code) => {
    const group = getTeamGroupFromMajor(code) as TeamMajorGroupKey | null;
    if (group === 'GROUP_1' || group === 'GROUP_2') {
      groupMajorCodes[group].push(code);
    }
  });

  const validTeamMajorCodes = new Set(
    TEAM_MAJOR_GROUPS.flatMap((group) => group.majors.map((major) => major.code)),
  );
  const unclassifiedMajorCodes = majorCodes.filter((code) => !validTeamMajorCodes.has(code));
  const leaderStudent = uniqueStudents.find((student) => student._id === leaderStudentId) || null;
  const hasGroupOne = groupMajorCodes.GROUP_1.length > 0;
  const hasGroupTwo = groupMajorCodes.GROUP_2.length > 0;
  const isMemberCountValid = memberCount >= TEAM_MEMBER_MINIMUM && memberCount <= TEAM_MEMBER_LIMIT;
  const isMajorRequirementValid = hasGroupOne && hasGroupTwo;
  const isTeamLeaderValid = Boolean(leaderStudentId && leaderStudent);
  const errors: string[] = [];
  const warnings: string[] = [];

  if (memberCount < TEAM_MEMBER_MINIMUM) {
    errors.push(`Select at least ${TEAM_MEMBER_MINIMUM} students.`);
  } else if (memberCount > TEAM_MEMBER_LIMIT) {
    errors.push(`Select no more than ${TEAM_MEMBER_LIMIT} students.`);
  }

  if (!hasGroupOne) {
    errors.push('Team must include at least one GROUP_1 major.');
  }

  if (!hasGroupTwo) {
    errors.push('Team must include at least one GROUP_2 major.');
  }

  if (!isTeamLeaderValid) {
    errors.push('Team Leader is required.');
  }

  if (missingMajorStudents.length > 0) {
    warnings.push(`${missingMajorStudents.length} selected student(s) have no declared major.`);
  }

  if (unclassifiedMajorCodes.length > 0) {
    warnings.push(`${unclassifiedMajorCodes.join(', ')} does not satisfy GROUP_1/GROUP_2.`);
  }

  return {
    memberCount,
    minMembers: TEAM_MEMBER_MINIMUM,
    maxMembers: TEAM_MEMBER_LIMIT,
    majorCount: majorCodes.length,
    minMajors: 2,
    majorCodes,
    groupMajorCodes,
    missingMajorStudents,
    unclassifiedMajorCodes,
    leaderStudent,
    hasGroupOne,
    hasGroupTwo,
    isMemberCountValid,
    isMajorRequirementValid,
    isTeamLeaderValid,
    canCreateTeam: isMemberCountValid && isMajorRequirementValid && isTeamLeaderValid,
    warnings,
    errors,
  };
}

export function normalizeManagedTeam(source: any): ManagedTeam {
  const members: TeamMember[] = (Array.isArray(source?.members) ? source.members : []).map((member: any) => ({
    studentId: {
      _id: String(member.studentId || member.id || ''),
      fullName: member.fullName || 'Unknown student',
      rollNumber: member.rollNumber || null,
      email: member.email || null,
      major: member.majorCode || null,
    },
    roleInTeam: member.roleInTeam,
    joinedAt: member.joinedAtUtc || member.joinedAt,
  }));
  return {
    ...source,
    _id: String(source?.id || source?._id || ''),
    classId: source?.classId,
    teamName: source?.teamName || source?.groupName || 'Unnamed team',
    leaderId: source?.leaderId || null,
    projectName: source?.projectName || null,
    projectDescription: source?.projectDescription || null,
    projectStatus: source?.projectStatus || null,
    hasChatGroup: Boolean(source?.hasChatGroup),
    members,
    teamMembers: members,
    memberIds: members.map(teamMemberStudentId),
    rowVersion: source?.rowVersion || '',
    mentorId: source?.currentMentorAssignment?.mentor
      ? {
          _id: source.currentMentorAssignment.mentor.mentorProfileId,
          id: source.currentMentorAssignment.mentor.mentorProfileId,
          name: source.currentMentorAssignment.mentor.fullName,
        }
      : null,
    currentMentorAssignment: source?.currentMentorAssignment || null,
  };
}

export function normalizeTeamProposal(source: any): ManagedTeam {
  const proposal = normalizeManagedTeam(source);
  return {
    ...proposal,
    teamCode: 'TEAM PROPOSAL',
    status: source?.status || 'Draft',
    hasChatGroup: false,
    projectName: source?.projectName || null,
    rejectReason: source?.latestReviewComment || null,
    isProposal: true,
  };
}

export function entityId(reference: EntityReference): string {
  if (!reference) return '';
  if (typeof reference === 'string') return reference;
  return String(reference._id || reference.id || '');
}

export function teamMemberStudentId(member: TeamMember): string {
  return typeof member.studentId === 'string' ? member.studentId : member.studentId._id;
}

export function getTeamMemberIds(team: ManagedTeam): string[] {
  const members = Array.isArray(team.members)
    ? team.members
    : Array.isArray(team.teamMembers)
      ? team.teamMembers
      : [];
  const ids = members.map(teamMemberStudentId).filter(Boolean);
  if (ids.length > 0) return [...new Set(ids)];
  return [...new Set((team.memberIds || []).map(String).filter(Boolean))];
}

export function getTeamMembers(team: ManagedTeam, students: TeamStudent[] = []): TeamStudent[] {
  const studentMap = new Map(students.map((student) => [student._id, student]));
  const members = Array.isArray(team.members)
    ? team.members
    : Array.isArray(team.teamMembers)
      ? team.teamMembers
      : [];

  if (members.length > 0) {
    return members.map((member) => {
      if (typeof member.studentId === 'object') return member.studentId;
      return studentMap.get(member.studentId) || {
        _id: member.studentId,
        fullName: 'Unknown student',
      };
    });
  }

  return getTeamMemberIds(team).map((studentId) => studentMap.get(studentId) || {
    _id: studentId,
    fullName: 'Unknown student',
  });
}

export function getTeamProject(team: ManagedTeam): TeamProject | null {
  if (team.project?.name?.trim()) return team.project;
  const projectName = team.projectName?.trim();
  if (!projectName) return null;

  return {
    name: projectName,
    description: team.projectDescription || team.description || null,
    status: team.projectStatus || 'DRAFT',
  };
}
export function buildStudentTeamAssignments(
  teams: ManagedTeam[],
  students: TeamStudent[] = [],
): Map<string, { teamId: string; teamName: string }> {
  const assignments = new Map<string, { teamId: string; teamName: string }>();

  teams.forEach((team) => {
    getTeamMemberIds(team).forEach((studentId) => {
      assignments.set(studentId, { teamId: team._id, teamName: team.teamName || team.teamCode || 'Another team' });
    });
  });

  students.forEach((student) => {
    const teamId = entityId(student.teamId);
    if (!teamId || assignments.has(student._id)) return;
    const team = teams.find((candidate) => candidate._id === teamId);
    assignments.set(student._id, {
      teamId,
      teamName: team?.teamName || team?.teamCode || 'Another team',
    });
  });

  return assignments;
}

export function validateTeamDraft(
  draft: TeamDraft,
  teams: ManagedTeam[],
  students: TeamStudent[],
  currentTeamId = '',
): TeamDraftValidation {
  const errors: TeamDraftValidation['errors'] = {};
  const conflicts = new Map<string, string>();
  const teamName = draft.teamName.trim();
  const uniqueMemberIds = [...new Set(draft.memberIds)];

  if (teamName.length === 0 && !currentTeamId) {
    // The backend generates the next sequential name when managers leave this blank.
  } else if (teamName.length < 3 || teamName.length > 60) {
    errors.teamName = 'Team name must be between 3 and 60 characters.';
  } else if (teamName.length > 0) {
    const duplicateName = teams.some((team) => (
      team._id !== currentTeamId
      && (!entityId(team.classId) || entityId(team.classId) === draft.classId)
      && team.teamName.trim().toLowerCase() === teamName.toLowerCase()
    ));
    if (duplicateName) errors.teamName = 'A team with this name already exists in the class.';
  }

  if (!draft.classId) errors.classId = 'A class is required.';

  const hasAllowedSize = uniqueMemberIds.length >= TEAM_MEMBER_MINIMUM && uniqueMemberIds.length <= TEAM_MEMBER_LIMIT;
  if (!hasAllowedSize) {
    errors.memberIds = 'A team must have 4-6 students.';
  }

  const knownStudentIds = new Set(students.map((student) => student._id));
  if (uniqueMemberIds.some((studentId) => !knownStudentIds.has(studentId))) {
    errors.memberIds = 'One or more selected students are not in this class.';
  }

  const selectedStudents = uniqueMemberIds
    .map((studentId) => students.find((student) => student._id === studentId))
    .filter((student): student is TeamStudent => Boolean(student));
  const hasBusinessMajor = selectedStudents.some((student) => getTeamGroupFromMajor(student.major) === 'GROUP_1');
  const hasTechnologyMajor = selectedStudents.some((student) => getTeamGroupFromMajor(student.major) === 'GROUP_2');
  if (hasAllowedSize && (!hasBusinessMajor || !hasTechnologyMajor)) {
    errors.memberIds = 'A team must include at least one GROUP_1 major and one GROUP_2 major.';
  }

  const teamsInClass = teams.filter((team) => !entityId(team.classId) || entityId(team.classId) === draft.classId);
  const assignments = buildStudentTeamAssignments(teamsInClass, students);
  uniqueMemberIds.forEach((studentId) => {
    const assignment = assignments.get(studentId);
    if (assignment && assignment.teamId !== currentTeamId) {
      conflicts.set(studentId, assignment.teamName);
    }
  });
  if (conflicts.size > 0) {
    errors.memberIds = `${conflicts.size} selected student${conflicts.size > 1 ? 's are' : ' is'} already assigned to another team.`;
  }

  if (!draft.leaderId) {
    errors.leaderId = 'Select a team leader.';
  } else if (!uniqueMemberIds.includes(draft.leaderId)) {
    errors.leaderId = 'The team leader must be one of the selected members.';
  }

  if (draft.projectName.trim() && (draft.projectName.trim().length < 3 || draft.projectName.trim().length > 100)) {
    errors.projectName = 'Project name must be between 3 and 100 characters.';
  }

  if (draft.projectDescription.trim().length > 500) {
    errors.projectDescription = 'Project summary cannot exceed 500 characters.';
  }

  return { isValid: Object.keys(errors).length === 0, errors, conflicts };
}
