import type MockAdapter from 'axios-mock-adapter';
import type { AxiosRequestConfig } from 'axios';
import type { MockDirectionReview, MockMentor, MockProposal, MockProposalMember, MockTeam } from '../mockState.ts';
import type { TeamFormation } from '../../types/teamFormation.ts';
import { TEAM_MAJOR_GROUPS } from '../../constants/majors.ts';
import {
  allocateId,
  allocateRowVersion,
  asString,
  asStringArray,
  classMutationGuard,
  created,
  failure,
  findClass,
  getMockState,
  memberFromStudent,
  ok,
  parseBody,
  persistMockState,
  refreshClassCounts,
  requestParams,
  routeId,
} from '../mockHelpers.ts';

function teamById(teamId: string): MockTeam | undefined {
  return getMockState().teams.find((team) => team.id === teamId);
}

function proposalMembers(classId: string, memberIds: string[], leaderId: string): MockProposalMember[] {
  const roster = getMockState().rosters[classId] || [];
  return memberIds.map((studentId) => roster.find((student) => student.studentId === studentId)).filter(Boolean).map((student) => ({
    studentId: student!.studentId,
    rollNumber: student!.rollNumber,
    fullName: student!.fullName,
    majorCode: student!.majorCode || '',
    isLeader: student!.studentId === leaderId,
  }));
}

function updateRosterTeamLinks(classId: string, team: MockTeam, oldMemberIds: string[] = []): void {
  const roster = getMockState().rosters[classId] || [];
  const nextMemberIds = new Set(team.members.map((member) => member.studentId));
  for (const student of roster) {
    if (oldMemberIds.includes(student.studentId) && !nextMemberIds.has(student.studentId)) {
      student.teamId = null;
      student.teamName = null;
      student.isTeamLeader = false;
    }
    if (nextMemberIds.has(student.studentId)) {
      student.teamId = team.id;
      student.teamName = team.teamName;
      student.isTeamLeader = student.studentId === team.leaderId;
    }
  }
}

function hasMemberConflict(classId: string, memberIds: string[], excludedTeamId = ''): boolean {
  return memberIds.some((studentId) => getMockState().teams.some((team) => (
    team.classId === classId
    && team.id !== excludedTeamId
    && team.members.some((member) => member.studentId === studentId)
  )));
}

function hasDuplicateTeamName(classId: string, teamName: string, excludedTeamId = ''): boolean {
  return getMockState().teams.some((team) => (
    team.classId === classId
    && team.id !== excludedTeamId
    && team.teamName.trim().toLowerCase() === teamName.trim().toLowerCase()
  ));
}

function isActiveSemesterMentor(classId: string, userId: string): boolean {
  const cls = findClass(classId);
  if (!cls) return false;
  return getMockState().semesterStaffAssignments.some((assignment) => (
    assignment.semesterId === cls.semesterId
    && assignment.userId === userId
    && assignment.role === 'MENTOR'
    && assignment.status === 'ACTIVE'
  ));
}

function activeMentorTeamCount(userId: string): number {
  return getMockState().teams.filter((team) =>
    team.currentMentorAssignments.some((assignment) => assignment.mentor.userId === userId && assignment.status === 'Active'))
    .length;
}

function registerTeamQueries(mock: MockAdapter): void {
  mock.onGet('/teams').reply((config) => {
    const classId = asString(requestParams(config).classId);
    const teams = getMockState().teams.filter((team) => !classId || team.classId === classId);
    return ok(teams, 'Teams retrieved successfully.');
  });

  mock.onGet(/^\/classes\/[^/]+\/teams$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/teams$/);
    return findClass(classId)
      ? ok(getMockState().teams.filter((team) => team.classId === classId), 'Class teams retrieved successfully.')
      : failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
  });

  mock.onGet(/^\/teams\/[^/]+$/).reply((config) => {
    const team = teamById(routeId(config, /^\/teams\/([^/]+)$/));
    return team ? ok(team, 'Team retrieved successfully.') : failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
  });

  mock.onGet(/^\/classes\/[^/]+\/mentors$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/mentors$/);
    if (!findClass(classId)) return failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
    const mentors = getMockState().teams.filter((team) => team.classId === classId)
      .flatMap((team) => team.currentMentorAssignments)
      .filter((assignment) => assignment.status === 'Active')
      .map((assignment) => assignment.mentor);
    return ok([...new Map(mentors.map((mentor) => [mentor.mentorProfileId, mentor])).values()], 'Class mentors retrieved successfully.');
  });

  mock.onGet(/^\/classes\/[^/]+\/mentor-candidates$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/mentor-candidates$/);
    if (!findClass(classId)) return failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
    const candidates = getMockState().users.filter((user) => (
      user.role === 'MENTOR'
      && user.status === 'APPROVED'
      && isActiveSemesterMentor(classId, user.id)
    )).map((user) => {
      const mentor: MockMentor = { mentorProfileId: user.id, userId: user.id, fullName: user.name, email: user.email, organization: 'E-HUB Partner Network', mentorType: 'Enterprise' };
      const activeTeamCount = activeMentorTeamCount(user.id);
      return { mentor, activeTeamCount };
    });
    return ok(candidates, 'Mentor candidates retrieved successfully.');
  });

  mock.onGet(/^\/teams\/[^/]+\/mentor-assignments$/).reply((config) => {
    const team = teamById(routeId(config, /^\/teams\/([^/]+)\/mentor-assignments$/));
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    return ok(team.currentMentorAssignments, 'Mentor assignments retrieved successfully.');
  });

  mock.onGet(/^\/classes\/[^/]+\/team-proposals$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/team-proposals$/);
    if (!findClass(classId)) return failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
    return ok(getMockState().proposals.filter((proposal) => proposal.classId === classId), 'Team proposals retrieved successfully.');
  });

  mock.onGet(/^\/team-proposals\/[^/]+\/history$/).reply((config) => {
    const proposal = getMockState().proposals.find((item) => item.id === routeId(config, /^\/team-proposals\/([^/]+)\/history$/));
    return proposal ? ok(proposal.history, 'Proposal history retrieved successfully.') : failure(404, 'TEAM_PROPOSAL_NOT_FOUND', 'Team proposal not found.');
  });

  mock.onGet(/^\/teams\/[^/]+\/project-direction$/).reply((config) => {
    const teamId = routeId(config, /^\/teams\/([^/]+)\/project-direction$/);
    if (!teamById(teamId)) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const direction = getMockState().directions.find((item) => item.teamId === teamId);
    return direction ? ok(direction, 'Project direction retrieved successfully.') : failure(404, 'PROJECT_DIRECTION_NOT_FOUND', 'Project direction has not been created.');
  });
}

function registerTeamMutations(mock: MockAdapter): void {
  mock.onPut(/^\/teams\/[^/]+\/members$/).reply((config) => {
    const teamId = routeId(config, /^\/teams\/([^/]+)\/members$/);
    const team = teamById(teamId);
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const body = parseBody(config);
    const guard = classMutationGuard(team.classId);
    if (guard) return guard;
    if (asString(body.rowVersion) !== team.rowVersion) return failure(409, 'TEAM_CONCURRENCY_CONFLICT', 'Team data is stale. Refresh and try again.');
    const memberIds = asStringArray(body.memberIds);
    const leaderId = asString(body.leaderStudentId);
    const teamName = asString(body.teamName, team.teamName).trim();
    if (teamName.length < 3 || teamName.length > 60) return failure(400, 'TEAM_NAME_INVALID', 'Team name must be between 3 and 60 characters.');
    if (hasDuplicateTeamName(team.classId, teamName, team.id)) return failure(409, 'TEAM_NAME_CONFLICT', 'A team with this name already exists in the class.');
    if (memberIds.length < 4 || memberIds.length > 6 || !memberIds.includes(leaderId)) return failure(400, 'TEAM_VALIDATION_ERROR', 'A team needs 4–6 students and a valid leader.');
    if (hasMemberConflict(team.classId, memberIds, team.id)) return failure(409, 'TEAM_MEMBER_CONFLICT', 'One or more students already belong to another team in this class.');
    const roster = getMockState().rosters[team.classId] || [];
    const members = memberIds.map((studentId) => roster.find((student) => student.studentId === studentId)).filter(Boolean).map((student) => memberFromStudent(student!, leaderId));
    if (members.length !== memberIds.length) return failure(400, 'TEAM_MEMBER_NOT_IN_CLASS', 'Every team member must be enrolled in the class.');
    const hasMajorGroup = (groupKey: string) => {
      const group = TEAM_MAJOR_GROUPS.find((item) => item.key === groupKey);
      return group?.majors.some((major) => members.some((member) => member.majorCode?.toUpperCase() === major.code)) ?? false;
    };
    if (!hasMajorGroup('GROUP_1') || !hasMajorGroup('GROUP_2')) {
      return failure(400, 'TEAM_MAJOR_COMPOSITION_INVALID', 'A team must include at least one GROUP_1 major and one GROUP_2 major.');
    }
    const oldMemberIds = team.members.map((member) => member.studentId);
    team.teamName = teamName;
    team.description = asString(body.description, team.description || '') || null;
    team.leaderId = leaderId;
    team.members = members;
    team.rowVersion = allocateRowVersion();
    updateRosterTeamLinks(team.classId, team, oldMemberIds);
    persistMockState();
    return ok(team, 'Team members updated successfully.');
  });

  mock.onDelete(/^\/teams\/[^/]+$/).reply((config) => {
    const teamId = routeId(config, /^\/teams\/([^/]+)$/);
    const state = getMockState();
    const teamIndex = state.teams.findIndex((team) => team.id === teamId);
    if (teamIndex < 0) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const team = state.teams[teamIndex];
    const user = state.users.find((item) => item.id === state.sessionUserId);
    if (user?.role !== 'LECTURER' || findClass(team.classId)?.primaryLecturerId !== user.id)
      return failure(403, 'CLASS_ACCESS_DENIED', 'Only the assigned lecturer can permanently dissolve this team.');
    const guard = classMutationGuard(team.classId);
    if (guard) return guard;
    const oldMemberIds = team.members.map((member) => member.studentId);
    team.members = [];
    team.leaderId = null;
    updateRosterTeamLinks(team.classId, team, oldMemberIds);
    state.teams.splice(teamIndex, 1);
    state.proposals = state.proposals.filter((proposal) => proposal.approvedTeamId !== teamId);
    state.directions = state.directions.filter((direction) => direction.teamId !== teamId);
    refreshClassCounts(team.classId);
    persistMockState();
    return ok(null, 'Team permanently deleted; student accounts and class enrollments preserved.');
  });

  mock.onPut(/^\/teams\/[^/]+\/leader$/).reply((config) => {
    const team = teamById(routeId(config, /^\/teams\/([^/]+)\/leader$/));
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const body = parseBody(config);
    if (asString(body.rowVersion) !== team.rowVersion) return failure(409, 'TEAM_CONCURRENCY_CONFLICT', 'Team data is stale.');
    const studentId = asString(body.studentId);
    if (!team.members.some((member) => member.studentId === studentId)) return failure(400, 'TEAM_LEADER_NOT_MEMBER', 'The team leader must be a current member.');
    team.leaderId = studentId;
    team.members.forEach((member) => { member.roleInTeam = member.studentId === studentId ? 'LEADER' : 'MEMBER'; });
    team.rowVersion = allocateRowVersion();
    updateRosterTeamLinks(team.classId, team);
    persistMockState();
    return ok(team, 'Team leader updated successfully.');
  });

  mock.onPost(/^\/teams\/[^/]+\/mentor-assignments$/).reply((config) => {
    const team = teamById(routeId(config, /^\/teams\/([^/]+)\/mentor-assignments$/));
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const guard = classMutationGuard(team.classId);
    if (guard) return guard;
    const body = parseBody(config);
    const mentorUser = getMockState().users.find((user) => user.id === asString(body.mentorProfileId) && user.role === 'MENTOR' && user.status === 'APPROVED');
    if (!mentorUser) return failure(400, 'MENTOR_INVALID', 'The selected mentor is unavailable.');
    if (!isActiveSemesterMentor(team.classId, mentorUser.id)) {
      return failure(400, 'MENTOR_NOT_AVAILABLE', "The selected mentor is not active in this semester's teaching staff list.");
    }
    const same = team.currentMentorAssignments.find((assignment) => assignment.mentor.userId === mentorUser.id && assignment.status === 'Active');
    if (same) {
      return ok(same, 'Mentor is already assigned to this team.');
    }
    const mentor: MockMentor = { mentorProfileId: mentorUser.id, userId: mentorUser.id, fullName: mentorUser.name, email: mentorUser.email, organization: 'E-HUB Partner Network', mentorType: 'Enterprise' };
    team.currentMentorAssignments.forEach((assignment) => {
      if (assignment.slot === mentor.mentorType && assignment.status === 'Active') {
        assignment.status = 'Ended';
        assignment.endedAtUtc = new Date().toISOString();
      }
    });
    const assignment = { assignmentId: allocateId(), teamId: team.id, teamName: team.teamName, classId: team.classId, mentor, status: 'Active' as const, assignedAtUtc: new Date().toISOString(), endedAtUtc: null, note: asString(body.note) || null, slot: mentor.mentorType };
    team.currentMentorAssignments.push(assignment);
    team.currentMentorAssignment = assignment;
    refreshClassCounts(team.classId);
    persistMockState();
    return ok(assignment, 'Mentor assigned successfully.');
  });

  mock.onPost(/^\/teams\/[^/]+\/mentor-assignments\/end$/).reply((config) => {
    const team = teamById(routeId(config, /^\/teams\/([^/]+)\/mentor-assignments\/end$/));
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const guard = classMutationGuard(team.classId);
    if (guard) return guard;
    const body = parseBody(config);
    const assignment = team.currentMentorAssignments.find(item => item.assignmentId === asString(body.assignmentId) && item.status === 'Active');
    if (!assignment) return failure(404, 'MENTOR_ASSIGNMENT_NOT_FOUND', 'There is no active mentor assignment.');
    assignment.status = 'Ended';
    assignment.endedAtUtc = new Date().toISOString();
    team.currentMentorAssignments = team.currentMentorAssignments.filter(item => item.status === 'Active');
    team.currentMentorAssignment = team.currentMentorAssignments[0] || null;
    refreshClassCounts(team.classId);
    persistMockState();
    return ok(null, 'Mentor assignment ended successfully.');
  });
}

function registerProposalHandlers(mock: MockAdapter): void {
  mock.onPost(/^\/classes\/[^/]+\/teams\/student-proposal$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/teams\/student-proposal$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;

    const state = getMockState();
    const currentUser = state.users.find((user) => user.id === state.sessionUserId);
    if (currentUser?.role === 'STUDENT') return failure(409, 'TEAM_FORMATION_REQUIRED', 'Students must use team formation.');
    const body = parseBody(config);
    const memberIds = [...new Set(asStringArray(body.studentIds))];
    const leaderId = asString(body.leaderStudentId);
    const teamName = asString(body.groupName).trim();
    const projectName = body.isProjectNameSameAsGroup === true
      ? teamName
      : asString(body.projectName).trim();
    const description = asString(body.description).trim();
    const roster = state.rosters[classId] || [];
    const canManageClass = currentUser?.role === 'ADMIN'
      || (currentUser?.role === 'LECTURER' && findClass(classId)?.primaryLecturerId === currentUser.id);

    if (!currentUser || !canManageClass) {
      return failure(403, 'CLASS_ACCESS_DENIED', 'Only an administrator or assigned lecturer can submit a team proposal.');
    }
    if (memberIds.length < 4 || memberIds.length > 6 || !memberIds.includes(leaderId)) {
      return failure(400, 'TEAM_PROPOSAL_INVALID', 'A proposal needs 4–6 unique students and a leader selected from its members.');
    }
    if (teamName.length < 3 || teamName.length > 60 || projectName.length < 3 || projectName.length > 60 || description.length < 20 || description.length > 500) {
      return failure(400, 'CLASS_VALIDATION_ERROR', 'Team name, project name, or description is invalid.');
    }

    const selectedStudents = memberIds
      .map((studentId) => roster.find((student) => student.studentId === studentId && student.enrollmentStatus === 'Active'));
    if (selectedStudents.some((student) => !student)) {
      return failure(400, 'TEAM_PROPOSAL_INVALID', 'Every proposed member must be actively enrolled in this class.');
    }
    if (selectedStudents.some((student) => student?.teamId)) {
      return failure(409, 'TEAM_MEMBERSHIP_CONFLICT', 'A proposed member already belongs to an active team.');
    }
    const openStatuses = new Set(['Draft', 'Pending', 'NeedsRevision']);
    if (state.proposals.some((proposal) => proposal.classId === classId
      && openStatuses.has(proposal.status)
      && proposal.members.some((member) => memberIds.includes(member.studentId)))) {
      return failure(409, 'TEAM_PROPOSAL_MEMBERSHIP_CONFLICT', 'A proposed member already belongs to another open proposal.');
    }

    const groupOneMajors = new Set(['BBA_HM', 'BBA_FIN', 'BBA_IB', 'BBA_MC', 'BBA_MKT', 'BEN', 'BBA_TM']);
    const groupTwoMajors = new Set(['BIT_AI', 'BIT_GD', 'BIT_IA', 'BIT_SE']);
    const majors = selectedStudents.map((student) => student?.majorCode?.toUpperCase() || '');
    if (!majors.some((major) => groupOneMajors.has(major)) || !majors.some((major) => groupTwoMajors.has(major))) {
      return failure(400, 'TEAM_MAJOR_COMPOSITION_INVALID', 'A team must include at least one GROUP_1 major and one GROUP_2 major.');
    }
    if (hasDuplicateTeamName(classId, teamName)
      || state.proposals.some((proposal) => proposal.classId === classId
        && proposal.teamName.trim().toLowerCase() === teamName.toLowerCase()
        && !['Rejected', 'Cancelled'].includes(proposal.status))) {
      return failure(409, 'TEAM_NAME_DUPLICATED', 'A team or open proposal with this name already exists in the class.');
    }

    const proposal: MockProposal = {
      id: allocateId(),
      classId,
      proposedByStudentId: leaderId,
      teamName,
      description,
      projectName,
      status: 'Pending',
      latestReviewComment: null,
      approvedTeamId: null,
      members: proposalMembers(classId, memberIds, leaderId),
      rowVersion: allocateRowVersion(),
      history: [{
        id: allocateId(),
        fromStatus: null,
        toStatus: 'Pending',
        action: 'SUBMITTED',
        comment: null,
        performedByUserId: currentUser.id,
        occurredAtUtc: new Date().toISOString(),
      }],
    };
    const teamId = allocateId();
    const team: MockTeam = {
      id: teamId,
      classId,
      teamCode: `${findClass(classId)?.subjectCode || 'TEAM'}-T${state.teams.filter((item) => item.classId === classId).length + 1}`,
      teamName,
      description: null,
      projectName: null,
      projectDescription: null,
      status: 'Active',
      leaderId,
      members: selectedStudents.map((student) => memberFromStudent(student!, leaderId)),
      currentMentorAssignment: null,
      currentMentorAssignments: [],
      rowVersion: allocateRowVersion(),
    };
    proposal.approvedTeamId = teamId;
    state.teams.push(team);
    state.proposals.unshift(proposal);
    updateRosterTeamLinks(classId, team);
    refreshClassCounts(classId);
    persistMockState();
    return ok(proposal, 'Team created and project proposal submitted for review.');
  });

  mock.onPost(/^\/classes\/[^/]+\/team-proposals$/).reply((config) => {
    const currentUser = getMockState().users.find((user) => user.id === getMockState().sessionUserId);
    if (currentUser?.role === 'STUDENT') return failure(409, 'TEAM_FORMATION_REQUIRED', 'Students must use team formation.');
    const classId = routeId(config, /^\/classes\/([^/]+)\/team-proposals$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    const body = parseBody(config);
    const leaderId = asString(body.leaderStudentId);
    const members = proposalMembers(classId, asStringArray(body.memberIds), leaderId);
    if (members.length < 2 || !members.some((member) => member.isLeader)) return failure(400, 'TEAM_PROPOSAL_INVALID', 'The proposal must include its leader and proposed members.');
    const proposal: MockProposal = { id: allocateId(), classId, teamName: asString(body.teamName).trim(), description: asString(body.description) || null, projectName: asString(body.projectName) || null, status: 'Draft', latestReviewComment: null, approvedTeamId: null, members, rowVersion: allocateRowVersion(), history: [] };
    getMockState().proposals.unshift(proposal);
    persistMockState();
    return created(proposal, 'Team proposal created successfully.');
  });

  mock.onPut(/^\/team-proposals\/[^/]+$/).reply((config) => {
    const proposal = getMockState().proposals.find((item) => item.id === routeId(config, /^\/team-proposals\/([^/]+)$/));
    if (!proposal) return failure(404, 'TEAM_PROPOSAL_NOT_FOUND', 'Team proposal not found.');
    const body = parseBody(config);
    if (asString(body.rowVersion) !== proposal.rowVersion) return failure(409, 'TEAM_PROPOSAL_CONCURRENCY_CONFLICT', 'Proposal data is stale.');
    if (!['Draft', 'NeedsRevision'].includes(proposal.status)) return failure(409, 'TEAM_PROPOSAL_STATE_INVALID', 'Only draft or revision-requested proposals can be edited.');
    const leaderId = asString(body.leaderStudentId);
    if (proposal.approvedTeamId) {
      const team = teamById(proposal.approvedTeamId);
      const requestedMembers = asStringArray(body.memberIds);
      if (!team || leaderId !== team.leaderId || requestedMembers.length !== team.members.length
        || !team.members.every((member) => requestedMembers.includes(member.studentId))) {
        return failure(400, 'TEAM_PROPOSAL_INVALID', 'Project proposals must use the current team members and leader. Manage membership separately.');
      }
    }
    proposal.teamName = asString(body.teamName, proposal.teamName).trim();
    proposal.description = asString(body.description, proposal.description || '') || null;
    proposal.projectName = asString(body.projectName, proposal.projectName || '') || null;
    proposal.members = proposalMembers(proposal.classId, asStringArray(body.memberIds), leaderId);
    proposal.rowVersion = allocateRowVersion();
    persistMockState();
    return ok(proposal, 'Team proposal updated successfully.');
  });

  mock.onPost(/^\/team-proposals\/[^/]+\/submit$/).reply((config) => proposalState(config, 'Pending'));
  mock.onPost(/^\/team-proposals\/[^/]+\/cancel$/).reply((config) => proposalState(config, 'Cancelled'));

  mock.onPost(/^\/team-proposals\/[^/]+\/review$/).reply((config) => {
    const proposal = getMockState().proposals.find((item) => item.id === routeId(config, /^\/team-proposals\/([^/]+)\/review$/));
    if (!proposal) return failure(404, 'TEAM_PROPOSAL_NOT_FOUND', 'Team proposal not found.');
    const guard = classMutationGuard(proposal.classId);
    if (guard) return guard;
    const body = parseBody(config);
    if (asString(body.rowVersion) !== proposal.rowVersion) return failure(409, 'TEAM_PROPOSAL_CONCURRENCY_CONFLICT', 'Proposal data is stale.');
    if (proposal.status !== 'Pending') return failure(409, 'TEAM_PROPOSAL_STATE_INVALID', 'Only pending proposals can be reviewed.');
    const decision = asString(body.decision, 'NeedsRevision');
    const previousStatus = proposal.status;
    proposal.status = decision;
    proposal.latestReviewComment = asString(body.comment) || null;
    if (decision === 'Approved') {
      let team = proposal.approvedTeamId ? teamById(proposal.approvedTeamId) : undefined;
      if (!team) {
        const id = allocateId();
        const roster = getMockState().rosters[proposal.classId] || [];
        const leaderId = proposal.members.find((member) => member.isLeader)?.studentId || proposal.members[0]?.studentId || '';
        team = { id, classId: proposal.classId, teamCode: `${findClass(proposal.classId)?.subjectCode || 'TEAM'}-T${getMockState().teams.length + 1}`, teamName: proposal.teamName, description: null, status: 'Active', leaderId, members: proposal.members.map((member) => roster.find((student) => student.studentId === member.studentId)).filter(Boolean).map((student) => memberFromStudent(student!, leaderId)), currentMentorAssignment: null, currentMentorAssignments: [], rowVersion: allocateRowVersion() };
        getMockState().teams.push(team);
        updateRosterTeamLinks(proposal.classId, team);
        proposal.approvedTeamId = id;
      }
      team.projectName = proposal.projectName;
      team.projectDescription = proposal.description;
      refreshClassCounts(proposal.classId);
    }
    proposal.rowVersion = allocateRowVersion();
    proposal.history.unshift({ id: allocateId(), fromStatus: previousStatus, toStatus: proposal.status, action: 'REVIEWED', comment: proposal.latestReviewComment, performedByUserId: getMockState().users.find((user) => user.role === 'LECTURER')?.id || allocateId(), occurredAtUtc: new Date().toISOString() });
    persistMockState();
    return ok(proposal, 'Team proposal reviewed successfully.');
  });
}

function registerFormationHandlers(mock: MockAdapter): void {
  const currentStudent = () => {
    const state = getMockState();
    const user = state.users.find(item => item.id === state.sessionUserId);
    return user?.role === 'STUDENT' ? user.id : null;
  };
  const ownFormation = (formationId: string) => getMockState().formations.find(item => item.id === formationId);

  mock.onGet('/team-formations/mine').reply((config) => {
    const studentId = currentStudent();
    if (!studentId) return failure(403, 'CLASS_ACCESS_DENIED', 'Student account required.');
    const classId = asString(config.params?.classId);
    return ok(getMockState().formations.filter(item =>
      (!classId || item.classId === classId) && item.invitations.some(invitation => invitation.studentId === studentId))
      .map(item => ({ ...item, myStudentId: studentId })));
  });
  mock.onGet('/team-formations/invitations/pending').reply(() => {
    const studentId = currentStudent();
    if (!studentId) return failure(403, 'CLASS_ACCESS_DENIED', 'Student account required.');
    return ok(getMockState().formations.filter(item => item.status === 'Pending' &&
      item.invitations.some(invitation => invitation.studentId === studentId && invitation.status === 'Pending'))
      .map(item => ({ ...item, myStudentId: studentId })));
  });
  mock.onGet(/^\/team-formations\/[^/]+$/).reply((config) => {
    const studentId = currentStudent();
    const formation = ownFormation(routeId(config, /^\/team-formations\/([^/]+)$/));
    if (!formation) return failure(404, 'TEAM_FORMATION_NOT_FOUND', 'Formation not found.');
    if (!studentId || !formation.invitations.some(item => item.studentId === studentId))
      return failure(403, 'CLASS_ACCESS_DENIED', 'This formation is not yours.');
    return ok({ ...formation, myStudentId: studentId });
  });
  mock.onPost(/^\/classes\/[^/]+\/team-formations$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/team-formations$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    const state = getMockState();
    const creatorId = currentStudent();
    if (!creatorId) return failure(403, 'CLASS_ACCESS_DENIED', 'Student account required.');
    const body = parseBody(config);
    const teamName = asString(body.teamName).trim();
    const ids = asStringArray(body.memberStudentIds);
    const leaderId = asString(body.leaderStudentId);
    if (ids.length < 4 || ids.length > 6 || new Set(ids).size !== ids.length ||
      !ids.includes(creatorId) || !ids.includes(leaderId) || teamName.length < 3 || teamName.length > 60)
      return failure(400, 'VALIDATION_ERROR', 'Formation requires 4–6 unique members, creator, leader, and a valid team name.');
    const roster = state.rosters[classId] || [];
    const selected = ids.map(id => roster.find(student => student.studentId === id && student.enrollmentStatus === 'Active'));
    if (selected.some(student => !student)) return failure(400, 'VALIDATION_ERROR', 'Every member must be enrolled.');
    const groupOne = new Set(['BBA_HM', 'BBA_FIN', 'BBA_IB', 'BBA_MC', 'BBA_MKT', 'BEN', 'BBA_TM']);
    const groupTwo = new Set(['BIT_AI', 'BIT_GD', 'BIT_IA', 'BIT_SE']);
    if (!selected.some(student => groupOne.has(student?.majorCode || '')) ||
      !selected.some(student => groupTwo.has(student?.majorCode || '')))
      return failure(400, 'TEAM_MAJOR_COMPOSITION_INVALID', 'Both major groups are required.');
    if (selected.some(student => student?.teamId) || state.formations.some(item => item.classId === classId &&
      item.status === 'Pending' && item.invitations.some(invitation => ids.includes(invitation.studentId))))
      return failure(409, 'TEAM_FORMATION_RESERVATION_CONFLICT', 'A member is already in a team or pending formation.');
    if (hasDuplicateTeamName(classId, teamName) || state.formations.some(item => item.classId === classId &&
      item.status === 'Pending' && item.teamName.toLowerCase() === teamName.toLowerCase()))
      return failure(409, 'TEAM_NAME_DUPLICATED', 'Team name is already in use.');
    const now = new Date().toISOString();
    const formation: TeamFormation = {
      id: allocateId(), classId, classCode: findClass(classId)?.classCode || '', teamName,
      creatorStudentId: creatorId, myStudentId: creatorId, proposedLeaderStudentId: leaderId,
      status: 'Pending', completedTeamId: null, createdAtUtc: now,
      invitations: selected.map(student => ({
        studentId: student!.studentId, fullName: student!.fullName, rollNumber: student!.rollNumber,
        status: student!.studentId === creatorId ? 'Accepted' : 'Pending',
        isCreator: student!.studentId === creatorId, isProposedLeader: student!.studentId === leaderId,
      })),
    };
    state.formations.unshift(formation);
    persistMockState();
    return created(formation, 'Invitations sent.');
  });
  mock.onPost(/^\/team-formations\/[^/]+\/(accept|decline|cancel)$/).reply((config) => {
    const formation = ownFormation(routeId(config, /^\/team-formations\/([^/]+)\/(accept|decline|cancel)$/));
    const action = routeId(config, /^\/team-formations\/([^/]+)\/(accept|decline|cancel)$/, 2);
    const studentId = currentStudent();
    if (!formation) return failure(404, 'TEAM_FORMATION_NOT_FOUND', 'Formation not found.');
    if (!studentId) return failure(403, 'CLASS_ACCESS_DENIED', 'Student account required.');
    const invitation = formation.invitations.find(item => item.studentId === studentId);
    if (!invitation || (action === 'cancel' && formation.creatorStudentId !== studentId))
      return failure(403, 'CLASS_ACCESS_DENIED', 'You cannot act on this formation.');
    if (formation.status !== 'Pending' || (action !== 'cancel' && invitation.status !== 'Pending'))
      return failure(409, 'TEAM_FORMATION_STATE_INVALID', 'Formation is no longer pending.');
    if (action === 'accept') invitation.status = 'Accepted';
    if (action === 'decline') invitation.status = 'Declined';
    if (action !== 'accept') formation.status = 'Cancelled';
    if (formation.invitations.every(item => item.status === 'Accepted')) {
      const state = getMockState();
      const leaderId = formation.proposedLeaderStudentId;
      const roster = state.rosters[formation.classId] || [];
      const selected = formation.invitations.map(item => roster.find(student => student.studentId === item.studentId));
      if (selected.some(item => !item || item.teamId)) return failure(409, 'TEAM_MEMBERSHIP_CONFLICT', 'A member joined another team.');
      const team: MockTeam = {
        id: allocateId(), classId: formation.classId,
        teamCode: `${findClass(formation.classId)?.classCode || 'TEAM'}_TEAM_${state.teams.filter(item => item.classId === formation.classId).length + 1}`,
        teamName: formation.teamName, description: null, projectName: null, projectDescription: null,
        status: 'Active', leaderId, members: selected.map(item => memberFromStudent(item!, leaderId)),
        currentMentorAssignment: null, currentMentorAssignments: [], rowVersion: allocateRowVersion(),
      };
      state.teams.push(team);
      updateRosterTeamLinks(formation.classId, team);
      formation.status = 'Completed';
      formation.completedTeamId = team.id;
      refreshClassCounts(formation.classId);
    }
    persistMockState();
    return ok({ ...formation, myStudentId: studentId });
  });
}

function proposalState(config: AxiosRequestConfig, status: 'Pending' | 'Cancelled') {
  const proposal = getMockState().proposals.find((item) => item.id === routeId(config, /^\/team-proposals\/([^/]+)\/(submit|cancel)$/));
  if (!proposal) return failure(404, 'TEAM_PROPOSAL_NOT_FOUND', 'Team proposal not found.');
  const body = parseBody(config);
  if (asString(body.rowVersion) !== proposal.rowVersion) return failure(409, 'TEAM_PROPOSAL_CONCURRENCY_CONFLICT', 'Proposal data is stale.');
  const fromStatus = proposal.status;
  proposal.status = status;
  proposal.rowVersion = allocateRowVersion();
  proposal.history.unshift({ id: allocateId(), fromStatus, toStatus: status, action: status.toUpperCase(), comment: status === 'Cancelled' ? asString(body.reason) || null : null, performedByUserId: getMockState().users.find((user) => user.role === 'STUDENT')?.id || allocateId(), occurredAtUtc: new Date().toISOString() });
  persistMockState();
  return ok(proposal, `Team proposal ${status === 'Pending' ? 'submitted' : 'cancelled'} successfully.`);
}

function registerDirectionHandlers(mock: MockAdapter): void {
  mock.onPut(/^\/teams\/[^/]+\/project-direction$/).reply((config) => {
    const teamId = routeId(config, /^\/teams\/([^/]+)\/project-direction$/);
    const team = teamById(teamId);
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const guard = classMutationGuard(team.classId);
    if (guard) return guard;
    const body = parseBody(config);
    let direction = getMockState().directions.find((item) => item.teamId === teamId);
    if (direction && asString(body.rowVersion) && body.rowVersion !== direction.rowVersion) return failure(409, 'PROJECT_DIRECTION_CONCURRENCY_CONFLICT', 'Project direction data is stale.');
    if (direction && !['Draft', 'NeedsRevision'].includes(direction.status)) return failure(409, 'PROJECT_DIRECTION_STATE_INVALID', 'Only Draft or NeedsRevision directions can be edited.');
    const revisedTitle = asString(body.title, direction?.title).trim();
    const revisedSummary = asString(body.summary, direction?.summary).trim();
    let revisedIndustries = team.startupIndustries || [];
    if (Array.isArray(body.startupIndustryIds)) {
      const startupIndustryIds = asStringArray(body.startupIndustryIds);
      if (startupIndustryIds.length < 1 || startupIndustryIds.length > 3 || new Set(startupIndustryIds).size !== startupIndustryIds.length) {
        return failure(400, 'VALIDATION_ERROR', 'Select between 1 and 3 distinct startup industries.');
      }
      const activeIndustries = getMockState().startupIndustries.filter((industry) => industry.status === 'active');
      const selectedIndustries = startupIndustryIds.map((id) => activeIndustries.find((industry) => industry.id === id));
      if (selectedIndustries.some((industry) => !industry)) {
        return failure(400, 'VALIDATION_ERROR', 'Every selected startup industry must exist and be active.');
      }
      revisedIndustries = selectedIndustries.map((industry) => industry!.name);
    }
    const industriesChanged = [...revisedIndustries].sort().join('|') !== [...(direction?.startupIndustries || [])].sort().join('|');
    if (direction?.status === 'NeedsRevision' && revisedTitle === direction.title && revisedSummary === direction.summary && !industriesChanged) {
      return failure(400, 'VALIDATION_ERROR', 'Change the project direction before saving the requested revision.');
    }
    if (!direction) {
      direction = { id: allocateId(), teamId, title: '', summary: '', startupIndustries: team.startupIndustries || [], status: 'Draft', submittedAtUtc: null, reviewedAtUtc: null, rowVersion: allocateRowVersion(), reviews: [] };
      getMockState().directions.push(direction);
    }
    direction.title = revisedTitle;
    direction.summary = revisedSummary;
    direction.startupIndustries = revisedIndustries;
    direction.status = 'Draft';
    direction.rowVersion = allocateRowVersion();
    if (team.projectName) {
      team.projectName = revisedTitle;
      team.projectDescription = revisedSummary;
      team.startupIndustries = revisedIndustries;
    }
    persistMockState();
    return ok(direction, 'Project direction saved successfully.');
  });

  mock.onPost(/^\/teams\/[^/]+\/project-direction\/submit$/).reply((config) => directionState(config, 'Submitted'));
  mock.onPost(/^\/teams\/[^/]+\/project-direction\/review$/).reply((config) => {
    const teamId = routeId(config, /^\/teams\/([^/]+)\/project-direction\/review$/);
    const direction = getMockState().directions.find((item) => item.teamId === teamId);
    if (!direction) return failure(404, 'PROJECT_DIRECTION_NOT_FOUND', 'Project direction has not been created.');
    const body = parseBody(config);
    if (asString(body.rowVersion) !== direction.rowVersion) return failure(409, 'PROJECT_DIRECTION_CONCURRENCY_CONFLICT', 'Project direction data is stale.');
    if (direction.status !== 'Submitted') return failure(409, 'PROJECT_DIRECTION_STATE_INVALID', 'Only submitted directions can be reviewed.');
    const fromStatus = direction.status;
    const toStatus = asString(body.decision, 'NeedsRevision');
    direction.status = toStatus;
    direction.reviewedAtUtc = new Date().toISOString();
    direction.rowVersion = allocateRowVersion();
    const review: MockDirectionReview = { id: allocateId(), fromStatus, toStatus, comment: asString(body.comment), reviewedByUserId: getMockState().users.find((user) => user.role === 'LECTURER')?.id || allocateId(), occurredAtUtc: new Date().toISOString() };
    direction.reviews.unshift(review);
    persistMockState();
    return ok(direction, 'Project direction reviewed successfully.');
  });
}

function directionState(config: AxiosRequestConfig, status: 'Submitted') {
  const teamId = routeId(config, /^\/teams\/([^/]+)\/project-direction\/submit$/);
  const direction = getMockState().directions.find((item) => item.teamId === teamId);
  if (!direction) return failure(404, 'PROJECT_DIRECTION_NOT_FOUND', 'Project direction has not been created.');
  if (asString(parseBody(config).rowVersion) !== direction.rowVersion) return failure(409, 'PROJECT_DIRECTION_CONCURRENCY_CONFLICT', 'Project direction data is stale.');
  if (direction.status !== 'Draft') return failure(409, 'PROJECT_DIRECTION_STATE_INVALID', 'Save requested revisions as a draft before submitting.');
  direction.status = status;
  direction.submittedAtUtc = new Date().toISOString();
  direction.rowVersion = allocateRowVersion();
  persistMockState();
  return ok(direction, 'Project direction submitted successfully.');
}

export function registerTeamMockHandlers(mock: MockAdapter): void {
  registerFormationHandlers(mock);
  registerTeamQueries(mock);
  registerTeamMutations(mock);
  registerProposalHandlers(mock);
  registerDirectionHandlers(mock);
}
