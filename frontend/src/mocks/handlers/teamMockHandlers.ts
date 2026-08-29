import type MockAdapter from 'axios-mock-adapter';
import type { AxiosRequestConfig } from 'axios';
import type { MockDirectionReview, MockMentor, MockProposal, MockProposalMember, MockTeam } from '../mockState.ts';
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

function nextTeamName(classId: string): string {
  return `Team ${getMockState().teams.filter((team) => team.classId === classId).length + 1}`;
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
  return getMockState().teams.filter((team) => (
    team.currentMentorAssignment?.mentor.userId === userId
    && team.currentMentorAssignment.status === 'Active'
  )).length;
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
    const mentors = getMockState().teams.filter((team) => team.classId === classId && team.currentMentorAssignment?.status === 'Active').map((team) => team.currentMentorAssignment!.mentor);
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
      const mentor: MockMentor = { mentorProfileId: user.id, userId: user.id, fullName: user.name, email: user.email, organization: 'E-HUB Partner Network' };
      const activeTeamCount = activeMentorTeamCount(user.id);
      return { mentor, activeTeamCount, maxTeams: 4, hasCapacity: activeTeamCount < 4 };
    });
    return ok(candidates, 'Mentor candidates retrieved successfully.');
  });

  mock.onGet(/^\/teams\/[^/]+\/mentor-assignments$/).reply((config) => {
    const team = teamById(routeId(config, /^\/teams\/([^/]+)\/mentor-assignments$/));
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    return ok(team.currentMentorAssignment ? [team.currentMentorAssignment] : [], 'Mentor assignments retrieved successfully.');
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
  mock.onPost(/^\/classes\/[^/]+\/teams$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/teams$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    const body = parseBody(config);
    const memberIds = asStringArray(body.memberIds);
    const leaderId = asString(body.leaderStudentId);
    const teamName = asString(body.teamName).trim();
    const roster = getMockState().rosters[classId] || [];
    if (teamName.length < 3 || teamName.length > 60) return failure(400, 'TEAM_NAME_INVALID', 'Team name must be between 3 and 60 characters.');
    if (hasDuplicateTeamName(classId, teamName)) return failure(409, 'TEAM_NAME_CONFLICT', 'A team with this name already exists in the class.');
    if (memberIds.length < 4 || memberIds.length > 6 || !memberIds.includes(leaderId)) return failure(400, 'TEAM_VALIDATION_ERROR', 'A team needs 4–6 students and a leader selected from its members.');
    if (hasMemberConflict(classId, memberIds)) return failure(409, 'TEAM_MEMBER_CONFLICT', 'One or more students already belong to a team in this class.');
    const members = memberIds.map((studentId) => roster.find((student) => student.studentId === studentId)).filter(Boolean).map((student) => memberFromStudent(student!, leaderId));
    if (members.length !== memberIds.length) return failure(400, 'TEAM_MEMBER_NOT_IN_CLASS', 'Every team member must be enrolled in the class.');
    const id = allocateId();
    const team: MockTeam = { id, classId, teamCode: `${findClass(classId)!.subjectCode}-T${String(getMockState().teams.filter((item) => item.classId === classId).length + 1).padStart(2, '0')}`, teamName, description: asString(body.description) || null, projectName: null, projectDescription: null, status: 'Active', leaderId, members, currentMentorAssignment: null, rowVersion: allocateRowVersion() };
    getMockState().teams.push(team);
    updateRosterTeamLinks(classId, team);
    refreshClassCounts(classId);
    persistMockState();
    return created(team, 'Team created successfully.');
  });

  mock.onPost(/^\/classes\/[^/]+\/teams\/generate$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/teams\/generate$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    const body = parseBody(config);
    const memberIds = asStringArray(body.studentIds);
    const leaderId = asString(body.leaderStudentId);
    const requestedTeamName = asString(body.teamName).trim();
    const teamName = requestedTeamName || nextTeamName(classId);
    const roster = getMockState().rosters[classId] || [];
    if (teamName.length < 3 || teamName.length > 60) return failure(400, 'TEAM_NAME_INVALID', 'Team name must be between 3 and 60 characters.');
    if (hasDuplicateTeamName(classId, teamName)) return failure(409, 'TEAM_NAME_CONFLICT', 'A team with this name already exists in the class.');
    if (memberIds.length < 4 || memberIds.length > 6 || !memberIds.includes(leaderId)) return failure(400, 'TEAM_VALIDATION_ERROR', 'A team needs 4–6 students and a leader selected from its members.');
    if (hasMemberConflict(classId, memberIds)) return failure(409, 'TEAM_MEMBER_CONFLICT', 'One or more students already belong to a team in this class.');
    const members = memberIds.map((studentId) => roster.find((student) => student.studentId === studentId)).filter(Boolean).map((student) => memberFromStudent(student!, leaderId));
    if (members.length !== memberIds.length) return failure(400, 'TEAM_MEMBER_NOT_IN_CLASS', 'Every team member must be enrolled in the class.');

    const id = allocateId();
    const team: MockTeam = {
      id,
      classId,
      teamCode: `${findClass(classId)!.subjectCode}-T${String(getMockState().teams.filter((item) => item.classId === classId).length + 1).padStart(2, '0')}`,
      teamName,
      description: asString(body.description) || null,
      projectName: null,
      projectDescription: null,
      status: 'Active',
      leaderId,
      members,
      currentMentorAssignment: null,
      rowVersion: allocateRowVersion(),
    };
    const mentorId = asString(body.mentorId);
    if (mentorId) {
      const mentorUser = getMockState().users.find((user) => user.id === mentorId && user.role === 'MENTOR' && user.status === 'APPROVED');
      if (!mentorUser) return failure(400, 'MENTOR_INVALID', 'The selected mentor is unavailable.');
      const mentor: MockMentor = { mentorProfileId: mentorUser.id, userId: mentorUser.id, fullName: mentorUser.name, email: mentorUser.email, organization: 'E-HUB Partner Network' };
      team.currentMentorAssignment = { assignmentId: allocateId(), teamId: id, teamName, classId, mentor, status: 'Active', assignedAtUtc: new Date().toISOString(), endedAtUtc: null, note: null };
    }
    getMockState().teams.push(team);
    updateRosterTeamLinks(classId, team);
    refreshClassCounts(classId);
    persistMockState();
    return created({ team, proposal: null }, 'Team request processed successfully.');
  });

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
    const guard = classMutationGuard(team.classId);
    if (guard) return guard;
    const oldMemberIds = team.members.map((member) => member.studentId);
    team.members = [];
    team.leaderId = null;
    updateRosterTeamLinks(team.classId, team, oldMemberIds);
    state.teams.splice(teamIndex, 1);
    state.directions = state.directions.filter((direction) => direction.teamId !== teamId);
    refreshClassCounts(team.classId);
    persistMockState();
    return ok(null, 'Team archived and members unassigned.');
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
    if (team.currentMentorAssignment?.mentor.userId === mentorUser.id && team.currentMentorAssignment.status === 'Active') {
      return ok(team.currentMentorAssignment, 'Mentor is already assigned to this team.');
    }
    if (activeMentorTeamCount(mentorUser.id) >= 4) {
      return failure(409, 'MENTOR_CAPACITY_REACHED', 'The selected mentor has reached the maximum active team capacity.');
    }
    const mentor: MockMentor = { mentorProfileId: mentorUser.id, userId: mentorUser.id, fullName: mentorUser.name, email: mentorUser.email, organization: 'E-HUB Partner Network' };
    team.currentMentorAssignment = { assignmentId: allocateId(), teamId: team.id, teamName: team.teamName, classId: team.classId, mentor, status: 'Active', assignedAtUtc: new Date().toISOString(), endedAtUtc: null, note: asString(body.note) || null };
    refreshClassCounts(team.classId);
    persistMockState();
    return ok(team.currentMentorAssignment, 'Mentor assigned successfully.');
  });

  mock.onPost(/^\/teams\/[^/]+\/mentor-assignments\/end$/).reply((config) => {
    const team = teamById(routeId(config, /^\/teams\/([^/]+)\/mentor-assignments\/end$/));
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const guard = classMutationGuard(team.classId);
    if (guard) return guard;
    if (!team.currentMentorAssignment) return failure(404, 'MENTOR_ASSIGNMENT_NOT_FOUND', 'There is no active mentor assignment.');
    team.currentMentorAssignment.status = 'Ended';
    team.currentMentorAssignment.endedAtUtc = new Date().toISOString();
    refreshClassCounts(team.classId);
    persistMockState();
    return ok(null, 'Mentor assignment ended successfully.');
  });
}

function registerProposalHandlers(mock: MockAdapter): void {
  mock.onPost(/^\/classes\/[^/]+\/team-proposals$/).reply((config) => {
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
      const id = allocateId();
      const roster = getMockState().rosters[proposal.classId] || [];
      const leaderId = proposal.members.find((member) => member.isLeader)?.studentId || proposal.members[0]?.studentId || '';
      const team: MockTeam = { id, classId: proposal.classId, teamCode: `${findClass(proposal.classId)?.subjectCode || 'TEAM'}-T${getMockState().teams.length + 1}`, teamName: proposal.teamName, description: proposal.description, status: 'Active', leaderId, members: proposal.members.map((member) => roster.find((student) => student.studentId === member.studentId)).filter(Boolean).map((student) => memberFromStudent(student!, leaderId)), currentMentorAssignment: null, rowVersion: allocateRowVersion() };
      getMockState().teams.push(team);
      updateRosterTeamLinks(proposal.classId, team);
      proposal.approvedTeamId = id;
      refreshClassCounts(proposal.classId);
    }
    proposal.rowVersion = allocateRowVersion();
    proposal.history.unshift({ id: allocateId(), fromStatus: previousStatus, toStatus: proposal.status, action: 'REVIEWED', comment: proposal.latestReviewComment, performedByUserId: getMockState().users.find((user) => user.role === 'LECTURER')?.id || allocateId(), occurredAtUtc: new Date().toISOString() });
    persistMockState();
    return ok(proposal, 'Team proposal reviewed successfully.');
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
    if (!direction) {
      direction = { id: allocateId(), teamId, title: '', summary: '', status: 'Draft', submittedAtUtc: null, reviewedAtUtc: null, rowVersion: allocateRowVersion(), reviews: [] };
      getMockState().directions.push(direction);
    }
    direction.title = asString(body.title, direction.title).trim();
    direction.summary = asString(body.summary, direction.summary).trim();
    direction.status = 'Draft';
    direction.rowVersion = allocateRowVersion();
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
  direction.status = status;
  direction.submittedAtUtc = new Date().toISOString();
  direction.rowVersion = allocateRowVersion();
  persistMockState();
  return ok(direction, 'Project direction submitted successfully.');
}

export function registerTeamMockHandlers(mock: MockAdapter): void {
  registerTeamQueries(mock);
  registerTeamMutations(mock);
  registerProposalHandlers(mock);
  registerDirectionHandlers(mock);
}
