import type {
  EntityReference,
  ManagedTeam,
  TeamDraft,
  TeamDraftValidation,
  TeamMember,
  TeamProject,
  TeamStudent,
} from '../types/teamManagement';

export const TEAM_MEMBER_LIMIT = 6;

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

  if (teamName.length < 3 || teamName.length > 60) {
    errors.teamName = 'Team name must be between 3 and 60 characters.';
  } else {
    const duplicateName = teams.some((team) => (
      team._id !== currentTeamId
      && (!entityId(team.classId) || entityId(team.classId) === draft.classId)
      && team.teamName.trim().toLowerCase() === teamName.toLowerCase()
    ));
    if (duplicateName) errors.teamName = 'A team with this name already exists in the class.';
  }

  if (!draft.classId) errors.classId = 'A class is required.';

  if (uniqueMemberIds.length === 0) {
    errors.memberIds = 'Select at least one student.';
  } else if (uniqueMemberIds.length > TEAM_MEMBER_LIMIT) {
    errors.memberIds = `A team can have up to ${TEAM_MEMBER_LIMIT} students.`;
  }

  const knownStudentIds = new Set(students.map((student) => student._id));
  if (uniqueMemberIds.some((studentId) => !knownStudentIds.has(studentId))) {
    errors.memberIds = 'One or more selected students are not in this class.';
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

  if (draft.leaderId && !uniqueMemberIds.includes(draft.leaderId)) {
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

export function applyTeamDraft(
  draft: TeamDraft,
  students: TeamStudent[],
  existingTeam: ManagedTeam | null,
  teamCode: string,
  teamId: string,
): ManagedTeam {
  const studentMap = new Map(students.map((student) => [student._id, student]));
  const now = new Date().toISOString();
  const members: TeamMember[] = draft.memberIds.map((studentId) => ({
    studentId: studentMap.get(studentId) || { _id: studentId, fullName: 'Unknown student' },
    roleInTeam: studentId === draft.leaderId ? 'LEADER' : 'MEMBER',
    joinedAt: now,
  }));
  const projectName = draft.projectName.trim();
  const project: TeamProject | null = projectName ? {
    ...(existingTeam?.project || {}),
    _id: existingTeam?.project?._id || `frontend-project-${teamId}`,
    name: projectName,
    description: draft.projectDescription.trim() || null,
    status: draft.projectStatus || 'DRAFT',
    startupField: draft.startupField.trim() || null,
  } : null;

  return {
    ...(existingTeam || {}),
    _id: teamId,
    classId: draft.classId,
    teamCode,
    teamName: draft.teamName.trim(),
    groupName: draft.teamName.trim(),
    description: draft.description.trim() || null,
    status: existingTeam?.status || 'ACTIVE',
    leaderId: draft.leaderId || null,
    members,
    teamMembers: members,
    memberIds: [...draft.memberIds],
    project,
    projectName: project?.name || null,
    projectDescription: project?.description || null,
    projectStatus: project?.status || null,
  };
}
