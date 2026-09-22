import type MockAdapter from 'axios-mock-adapter';
import type { MockDetailedProposal, MockDetailedProposalContent, MockDetailedProposalVersion } from '../mockState.ts';
import { allocateId, allocateRowVersion, created, failure, getMockState, ok, parseBody, persistMockState, routeId } from '../mockHelpers.ts';

const uuid = (value: number) => `00000000-0000-4000-8000-${String(value).padStart(12, '0')}`;

type MockWeeklyTask = {
  _id: string;
  title: string;
  taskType: string;
  weekNumber: number;
  teamId?: unknown;
  classId?: unknown;
  [key: string]: unknown;
};

type MockShortcut = {
  _id: string;
  url: string;
  [key: string]: unknown;
};

const mockWeeklyTasks: MockWeeklyTask[] = [];
const mockShortcuts = new Map<string, MockShortcut[]>();

const proposalContentKeys = [
  'title', 'startupName', 'tagline', 'problem', 'solution', 'targetCustomers', 'valueProposition',
  'marketSize', 'competitors', 'businessModel', 'revenueModel', 'marketingStrategy', 'technology',
  'financialPlan', 'roadmap', 'teamIntroduction',
] as const;

const proposalMaximums: Record<(typeof proposalContentKeys)[number], number> = {
  title: 200, startupName: 150, tagline: 200, problem: 3000, solution: 3000,
  targetCustomers: 2000, valueProposition: 2000, marketSize: 2500, competitors: 3000,
  businessModel: 3000, revenueModel: 2000, marketingStrategy: 3000, technology: 3000,
  financialPlan: 3000, roadmap: 3000, teamIntroduction: 2000,
};

function proposalContent(body: Partial<Record<(typeof proposalContentKeys)[number], unknown>>): MockDetailedProposalContent {
  return Object.fromEntries(proposalContentKeys.map((key) => [key, String(body[key] || '').trim()])) as unknown as MockDetailedProposalContent;
}

function proposalContentError(content: MockDetailedProposalContent): string | null {
  for (const key of proposalContentKeys) {
    if (content[key].length > proposalMaximums[key]) return `${key} exceeds its maximum length.`;
  }
  return proposalContentKeys.reduce((total, key) => total + content[key].length, 0) > 30_000
    ? 'Proposal content must not exceed 30000 characters in total.'
    : null;
}

function proposalSubmissionError(content: MockDetailedProposalContent): string | null {
  const minimums: Partial<Record<(typeof proposalContentKeys)[number], number>> = {
    title: 5, startupName: 2, problem: 100, solution: 100, targetCustomers: 50,
    valueProposition: 50, businessModel: 100, roadmap: 100,
  };
  for (const [key, minimum] of Object.entries(minimums) as Array<[(typeof proposalContentKeys)[number], number]>) {
    if (content[key].length < minimum) return `${key} must contain at least ${minimum} characters.`;
  }
  return null;
}

function proposalResponse(proposal: MockDetailedProposal) {
  const { versions: _versions, ...response } = proposal;
  return response;
}

function versionSummary(version: MockDetailedProposalVersion) {
  const { snapshot: _snapshot, projectProposalId: _projectProposalId, ...summary } = version;
  return summary;
}

function addProposalVersion(proposal: MockDetailedProposal, purpose: 'DraftSave' | 'Submission', changeNote: string, changedByUserId: string) {
  const version: MockDetailedProposalVersion = {
    id: allocateId(),
    projectProposalId: proposal.id,
    versionNumber: proposal.versions.length + 1,
    purpose,
    snapshotSchemaVersion: 'project-proposal-snapshot-v1',
    changeNote,
    changedByUserId,
    createdAtUtc: new Date().toISOString(),
    snapshot: proposalContent(proposal),
  };
  proposal.versions.push(version);
  return version;
}

function currentMockUser() {
  const state = getMockState();
  return state.users.find((user) => user.id === state.sessionUserId);
}

function canMutateProposal(teamId: string) {
  const user = currentMockUser();
  return Boolean(user?.role === 'STUDENT' && teamById(teamId)?.members.some((member) => member.studentId === user.id));
}

const checkpointConfig = [
  {
    number: 1,
    title: 'Startup Idea & Team Formation',
    shortDescription: 'Define your startup concept, choose your field, and establish clear member roles.',
    icon: 'Users',
    requirements: ['Team name', 'Startup idea', 'Member roles'],
    rubrics: [
      { key: 'idea-clarity', label: 'Startup Idea Clarity', description: 'How clear, focused, and understandable the idea is.', weight: 30, maxScore: 10, levels: [] },
      { key: 'problem-fit', label: 'Problem & Customer Fit', description: 'Evidence that the idea addresses a meaningful customer problem.', weight: 40, maxScore: 10, levels: [] },
      { key: 'team-readiness', label: 'Team Readiness', description: 'Roles and responsibilities are practical and well distributed.', weight: 30, maxScore: 10, levels: [] },
    ],
  },
  {
    number: 2,
    title: 'Market Validation',
    shortDescription: 'Conduct surveys and interviews, analyze the market, and validate product-market fit.',
    icon: 'BarChart2',
    requirements: ['Target customer', 'Interview findings', 'Market evidence'],
    rubrics: [{ key: 'validation', label: 'Validation Quality', description: 'Strength and relevance of collected market evidence.', weight: 100, maxScore: 10, levels: [] }],
  },
  {
    number: 3,
    title: 'Product & Business Model',
    shortDescription: 'Develop an MVP or prototype and outline your Business Model Canvas.',
    icon: 'Layers',
    requirements: ['Value proposition', 'MVP scope', 'Business model'],
    rubrics: [{ key: 'business-model', label: 'Business Model', description: 'Coherence of the value proposition and operating model.', weight: 100, maxScore: 10, levels: [] }],
  },
  {
    number: 4,
    title: 'Final Pitch',
    shortDescription: 'Prepare your final pitch deck and rehearse the presentation delivery.',
    icon: 'TrendingUp',
    requirements: ['Pitch narrative', 'Traction and evidence', 'Next steps'],
    rubrics: [{ key: 'pitch', label: 'Pitch Quality', description: 'Clarity, evidence, and persuasiveness of the final pitch.', weight: 100, maxScore: 10, levels: [] }],
  },
];

function teamById(teamId: string) {
  return getMockState().teams.find((team) => team.id === teamId);
}

function classByTeam(teamId: string) {
  const team = teamById(teamId);
  return team ? getMockState().classes.find((item) => item.id === team.classId) : undefined;
}

function workspaceOption(teamId: string) {
  const team = teamById(teamId)!;
  const cls = classByTeam(teamId)!;
  return {
    teamId: team.id,
    teamName: team.teamName,
    classId: cls.id,
    classCode: cls.classCode,
    courseCode: cls.subjectCode,
    semester: cls.semesterCode,
    accessMode: 'READ_WRITE',
    isArchived: cls.status === 'Archived',
    isCurrent: cls.status === 'Active',
    hasWorkspace: Boolean(team.projectName),
  };
}

function accessibleTeams() {
  const state = getMockState();
  const currentUser = state.users.find((user) => user.id === state.sessionUserId);
  if (!currentUser) return [];
  if (currentUser.role === 'ADMIN' || currentUser.role === 'LECTURER') return state.teams;
  if (currentUser.role === 'MENTOR') {
    return state.teams.filter((team) => team.currentMentorAssignment?.mentor.userId === currentUser.id);
  }
  return state.teams.filter((team) => team.members.some((member) => member.studentId === currentUser.id));
}

function canAccessTeam(teamId: string) {
  return accessibleTeams().some((team) => team.id === teamId);
}

function workspaceData(teamId: string) {
  const state = getMockState();
  const team = teamById(teamId)!;
  const cls = classByTeam(teamId)!;
  const lecturer = state.users.find((user) => user.id === cls.primaryLecturerId) || null;
  const mentorUserId = team.currentMentorAssignment?.mentor.userId;
  const mentor = mentorUserId ? state.users.find((user) => user.id === mentorUserId) || null : null;
  const proposal = state.proposals.find((item) => item.approvedTeamId === teamId) || null;
  const projectCreatedAtUtc = team.projectCreatedAtUtc || '2026-08-20T08:00:00.000Z';
  const members = team.members.map((member) => {
    const user = state.users.find((item) => item.id === member.studentId);
    return {
      _id: member.studentId,
      studentId: member.studentId,
      userId: user ? { _id: user.id, name: user.name, email: user.email } : { _id: member.studentId },
      fullName: member.fullName,
      email: member.email,
      rollNumber: member.rollNumber,
      majorCode: member.majorCode,
      roleInTeam: member.roleInTeam === 'LEADER' ? 'Leader' : 'Member',
    };
  });

  return {
    team: {
      _id: team.id,
      teamCode: team.teamCode,
      teamName: team.teamName,
      name: team.teamName,
      description: team.description,
      leaderId: team.leaderId,
      status: team.status,
    },
    class: {
      _id: cls.id,
      classCode: cls.classCode,
      subjectCode: cls.subjectCode,
      subjectName: cls.subjectName,
      semesterCode: cls.semesterCode,
    },
    members,
    lecturer: lecturer ? { _id: lecturer.id, name: lecturer.name, email: lecturer.email } : null,
    mentor: mentor ? { _id: mentor.id, name: mentor.name, email: mentor.email } : null,
    project: team.projectName ? {
      _id: uuid(1000 + Number(team.id.slice(-3))),
      teamId: team.id,
      classId: cls.id,
      subjectId: cls.courseId,
      semesterId: cls.semesterId,
      projectName: team.projectName,
      description: team.projectDescription || team.description || '',
      problem: team.projectProblem || '',
      solution: team.projectSolution || '',
      targetUsers: team.projectTargetUsers || '',
      zaloGroupUrl: team.projectZaloGroupUrl || '',
      keywords: team.keywords || [],
      startupIndustries: team.startupIndustries || [],
      status: 'Draft',
      createdAtUtc: projectCreatedAtUtc,
      updatedAtUtc: team.projectUpdatedAtUtc || null,
    } : null,
    activities: team.projectActivities || (team.projectName ? [{
      id: uuid(1700 + Number(team.id.slice(-3))),
      action: 'WORKSPACE_CREATED',
      summary: 'Created the project workspace.',
      actorUserId: team.leaderId,
      actorName: members.find((member) => member._id === team.leaderId)?.fullName || 'Team leader',
      changedFields: ['projectName', 'description', 'startupIndustries'],
      occurredAtUtc: projectCreatedAtUtc,
    }] : []),
    proposal: proposal ? {
      id: proposal.id,
      _id: proposal.id,
      teamName: proposal.teamName,
      projectName: proposal.projectName || proposal.teamName,
      projectDescription: proposal.description || '',
      status: proposal.status,
    } : null,
    latestDeck: { _id: uuid(1102), originalName: 'Phoenix-Founders-Pitch.pdf' },
  };
}

function checkpointData(teamId: string) {
  const team = teamById(teamId)!;
  const uploader = getMockState().users.find((user) => user.id === team.leaderId);
  return {
    subjectCode: classByTeam(teamId)?.subjectCode || '',
    checkpoints: checkpointConfig,
    submissions: checkpointConfig.map((checkpoint) => ({
      checkpointNumber: checkpoint.number,
      files: checkpoint.number === 1 ? [{
        _id: uuid(1201),
        originalName: 'Startup-Idea-Phoenix-Founders.pdf',
        fileType: 'pdf',
        fileSize: 1_482_752,
        uploadedAt: new Date(Date.now() - 86_400_000).toISOString(),
        uploadedBy: { _id: uploader?.id, name: uploader?.name || 'Team leader' },
      }] : [],
      requirementContents: checkpoint.requirements.map((_, index) => ({
        index,
        content: checkpoint.number === 1
          ? [
              'Phoenix Founders',
              'A trusted marketplace connecting students with verified campus services.',
              'Education technology and campus services.',
              'Product lead, customer research, engineering, and business development.',
            ][index]
          : '',
      })),
    })),
    feedbacks: [{
      _id: uuid(1301),
      checkpointNumber: 1,
      comment: 'The direction is promising. Add stronger interview evidence before moving to the prototype.',
      parentFeedbackId: null,
      createdAt: new Date(Date.now() - 43_200_000).toISOString(),
      user: { _id: uuid(2), name: 'Trần Thu Giang', role: 'LECTURER' },
    }],
  };
}

function evaluationSummary(checkpointNumber: number) {
  const checkpoint = checkpointConfig.find((item) => item.number === checkpointNumber) || checkpointConfig[0];
  const hasEvaluation = checkpointNumber === 1;
  const evaluation = {
    _id: uuid(1401),
    lecturerId: { _id: uuid(2), name: 'Trần Thu Giang' },
    evaluatorRole: 'LECTURER',
    status: 'DRAFT',
    checkpointNumber,
    checkpointTotal: 7.85,
    weightedScore: 7.85,
    overallFeedback: 'Good early direction. Strengthen customer evidence and clarify the validation plan.',
    updatedAt: new Date(Date.now() - 21_600_000).toISOString(),
    rubricScores: checkpoint.rubrics.map((criterion, index) => ({
      criterionKey: criterion.key,
      criterionName: criterion.label,
      selectedLevel: index === 1 ? 'GOOD' : 'EXCELLENT',
      scoreMode: 'LEVEL',
      score: index === 1 ? 7.5 : 8.5,
      weightedScore: Number((((index === 1 ? 7.5 : 8.5) * criterion.weight) / 100).toFixed(2)),
      comment: index === 1 ? 'Include more direct customer quotes and quantified findings.' : 'Clear and focused.',
    })),
  };

  return {
    checkpoint,
    evaluations: hasEvaluation ? [evaluation] : [],
    summary: {
      evaluationCount: hasEvaluation ? 1 : 0,
      submittedCount: 0,
      averageScore: hasEvaluation ? evaluation.checkpointTotal : 0,
      overallPerformance: { level: hasEvaluation ? 'GOOD' : 'Unscored', label: hasEvaluation ? 'Good' : 'Not scored' },
    },
    history: hasEvaluation ? [{
      _id: uuid(1501),
      action: 'DRAFT_SAVED',
      version: 1,
      changedBy: { _id: uuid(2), name: 'Trần Thu Giang' },
      createdAt: evaluation.updatedAt,
    }] : [],
  };
}

export function registerWorkspaceMockHandlers(mock: MockAdapter): void {
  mock.onGet('/features').reply(() => ok({ aiEnabled: true }, 'Feature availability retrieved.'));

  mock.onGet('/weekly-tasks').reply((config) => {
    const weekNumber = Number(config.params?.weekNumber || 1);
    const teamId = String(config.params?.teamId || '');
    const classId = String(config.params?.classId || '');
    const rows = mockWeeklyTasks.filter((task) => Number(task.weekNumber) === weekNumber);
    return ok({
      courseTasks: rows.filter((task) => task.taskType === 'COURSE_TEMPLATE'),
      classTasks: rows.filter((task) => task.taskType === 'CLASS_TASK' && (!classId || task.classId === classId)),
      teamTasks: rows.filter((task) => task.taskType === 'TEAM_TASK' && (!teamId || task.teamId === teamId)),
    }, 'Weekly roadmap retrieved.');
  });

  mock.onGet(/^\/weekly-tasks\/team\/[^/]+\/board$/).reply((config) => {
    const teamId = routeId(config, /^\/weekly-tasks\/team\/([^/]+)\/board$/);
    if (!canAccessTeam(teamId)) return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this workspace.');
    const teamClass = classByTeam(teamId);
    const params = config.params || {};
    const search = String(params.search || '').trim().toLowerCase();
    const rows = mockWeeklyTasks.filter((task) =>
      task.courseCode === teamClass?.subjectCode &&
      (!params.weekNumber || task.weekNumber === Number(params.weekNumber)) &&
      (!params.priority || task.priority === params.priority) &&
      (!params.assigneeStudentId || task.assigneeStudentId === params.assigneeStudentId) &&
      (!params.status || task.status === params.status) &&
      (!search || `${task.title} ${task.description || ''}`.toLowerCase().includes(search)));
    return ok({
      courseTasks: rows.filter((task) => task.taskType === 'COURSE_TEMPLATE'),
      classTasks: rows.filter((task) => task.taskType === 'CLASS_TASK' && task.classId === teamClass?.id),
      teamTasks: rows.filter((task) => task.taskType === 'TEAM_TASK' && task.teamId === teamId),
    }, 'Team task board retrieved.');
  });

  mock.onPost('/weekly-tasks').reply((config) => {
    const body = parseBody(config);
    const title = String(body.title || '').trim();
    const weekNumber = Number(body.weekNumber || 0);
    if (!title || weekNumber < 1 || weekNumber > 10) return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Weekly task information is invalid.');
    const taskType = String(body.taskType || 'TEAM_TASK');
    const duplicated = mockWeeklyTasks.some((task) => task.taskType === taskType && task.weekNumber === weekNumber && task.teamId === body.teamId && task.classId === body.classId && task.title.toLowerCase() === title.toLowerCase());
    if (duplicated) return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'A task with this title already exists for the selected week.');
    const task: MockWeeklyTask = { ...body, _id: uuid(3000 + mockWeeklyTasks.length), title, taskType, weekNumber, status: body.status || 'TODO', priority: body.priority || 'MEDIUM', checklist: body.checklist || [], attachments: body.attachments || [], createdBy: { _id: getMockState().sessionUserId, name: 'Current user' }, createdAt: new Date().toISOString() };
    mockWeeklyTasks.push(task);
    return [201, { success: true, message: 'Weekly task created.', data: task, errors: null }];
  });

  mock.onPut(/^\/weekly-tasks\/[^/]+$/).reply((config) => {
    const taskId = routeId(config, /^\/weekly-tasks\/([^/]+)$/);
    const index = mockWeeklyTasks.findIndex((task) => task._id === taskId);
    if (index < 0) return failure(404, 'WORKSPACE_NOT_FOUND', 'Weekly task was not found.');
    mockWeeklyTasks[index] = { ...mockWeeklyTasks[index], ...parseBody(config), _id: taskId, updatedAt: new Date().toISOString() };
    return ok(mockWeeklyTasks[index], 'Weekly task updated.');
  });

  mock.onPatch(/^\/weekly-tasks\/[^/]+\/status$/).reply((config) => {
    const taskId = routeId(config, /^\/weekly-tasks\/([^/]+)\/status$/);
    const task = mockWeeklyTasks.find((item) => item._id === taskId);
    if (!task) return failure(404, 'WORKSPACE_NOT_FOUND', 'Weekly task was not found.');
    Object.assign(task, parseBody(config), { updatedAt: new Date().toISOString() });
    return ok(task, 'Weekly task status updated.');
  });

  mock.onDelete(/^\/weekly-tasks\/[^/]+$/).reply((config) => {
    const taskId = routeId(config, /^\/weekly-tasks\/([^/]+)$/);
    const index = mockWeeklyTasks.findIndex((task) => task._id === taskId);
    if (index < 0) return failure(404, 'WORKSPACE_NOT_FOUND', 'Weekly task was not found.');
    mockWeeklyTasks.splice(index, 1);
    return ok(null, 'Weekly task deleted.');
  });

  mock.onGet(/^\/teams\/[^/]+\/shortcuts$/).reply((config) => {
    const teamId = routeId(config, /^\/teams\/([^/]+)\/shortcuts$/);
    if (!canAccessTeam(teamId)) return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this workspace.');
    return ok(mockShortcuts.get(teamId) || [], 'Shortcuts retrieved.');
  });

  mock.onPost(/^\/teams\/[^/]+\/shortcuts$/).reply((config) => {
    const teamId = routeId(config, /^\/teams\/([^/]+)\/shortcuts$/);
    if (!canAccessTeam(teamId)) return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this workspace.');
    const body = parseBody(config);
    const name = String(body.name || '').trim();
    const url = String(body.url || '').trim().replace(/\/$/, '');
    if (!name || !/^https?:\/\//i.test(url)) return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Shortcut name and a valid URL are required.');
    const rows = mockShortcuts.get(teamId) || [];
    if (rows.some((item) => String(item.url).toLowerCase() === url.toLowerCase())) return failure(409, 'WORKSPACE_TAG_DUPLICATED', 'A shortcut with this URL already exists in the team.');
    const shortcut: MockShortcut = { _id: uuid(4000 + rows.length), teamId, name, url, createdBy: { _id: getMockState().sessionUserId, name: 'Current user' }, createdAt: new Date().toISOString() };
    rows.unshift(shortcut); mockShortcuts.set(teamId, rows);
    return [201, { success: true, message: 'Shortcut created.', data: shortcut, errors: null }];
  });

  mock.onPut(/^\/teams\/[^/]+\/shortcuts\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/teams\/([^/]+)\/shortcuts\/([^/]+)$/);
    const rows = mockShortcuts.get(match?.[1] || '') || [];
    const shortcut = rows.find((item) => item._id === match?.[2]);
    if (!shortcut) return failure(404, 'WORKSPACE_NOT_FOUND', 'Shortcut was not found.');
    Object.assign(shortcut, parseBody(config), { _id: shortcut._id, updatedAt: new Date().toISOString() });
    return ok(shortcut, 'Shortcut updated.');
  });

  mock.onDelete(/^\/teams\/[^/]+\/shortcuts\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/teams\/([^/]+)\/shortcuts\/([^/]+)$/);
    const rows = mockShortcuts.get(match?.[1] || '') || [];
    const index = rows.findIndex((item) => item._id === match?.[2]);
    if (index < 0) return failure(404, 'WORKSPACE_NOT_FOUND', 'Shortcut was not found.');
    rows.splice(index, 1);
    return ok(null, 'Shortcut deleted.');
  });

  mock.onGet('/workspace/accessible-teams').reply(() => ok(
    accessibleTeams().map((team) => workspaceOption(team.id)),
    'Accessible workspaces retrieved successfully.',
  ));

  mock.onGet('/team-workspaces/current').reply(() => {
    const first = accessibleTeams()[0];
    if (!first) return ok(null, 'No team workspace is available.');
    const selectedWorkspace = workspaceOption(first.id);
    return ok({ selectedWorkspace, availableWorkspaces: accessibleTeams().map((team) => workspaceOption(team.id)), accessMode: selectedWorkspace.accessMode });
  });

  mock.onGet(/^\/team-workspaces\/team\/[^/]+\/context$/).reply((config) => {
    const teamId = routeId(config, /^\/team-workspaces\/team\/([^/]+)\/context$/);
    if (!teamById(teamId)) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    if (!canAccessTeam(teamId)) return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this team workspace.');
    const selectedWorkspace = workspaceOption(teamId);
    return ok({ selectedWorkspace, availableWorkspaces: accessibleTeams().map((team) => workspaceOption(team.id)), accessMode: selectedWorkspace.accessMode });
  });

  mock.onGet(/^\/workspace\/teams\/[^/]+$/).reply((config) => {
    const teamId = routeId(config, /^\/workspace\/teams\/([^/]+)$/);
    if (!teamById(teamId)) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    return canAccessTeam(teamId)
      ? ok(workspaceData(teamId), 'Workspace retrieved successfully.')
      : failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this team workspace.');
  });

  mock.onPost(/^\/workspace\/teams\/[^/]+$/).reply((config) => {
    const teamId = routeId(config, /^\/workspace\/teams\/([^/]+)$/);
    const team = teamById(teamId);
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const state = getMockState();
    const currentUser = state.users.find((user) => user.id === state.sessionUserId);
    if (!currentUser || currentUser.role !== 'STUDENT' || team.leaderId !== currentUser.id) {
      return failure(403, 'WORKSPACE_LEADER_REQUIRED', 'Only the active team leader can create this project workspace.');
    }
    if (team.projectName) return failure(409, 'WORKSPACE_ALREADY_EXISTS', 'This team already has an active project workspace.');
    const body = parseBody(config);
    const projectName = String(body.projectName || '').trim();
    const description = String(body.description || '').trim();
    const startupIndustryIds = Array.isArray(body.startupIndustryIds) ? body.startupIndustryIds.map(String) : [];
    if (projectName.length < 3 || description.length < 20) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Required project workspace information is missing or invalid.');
    }
    const activeIndustries = state.startupIndustries.filter((industry) => (
      industry.status === 'active' && startupIndustryIds.includes(industry.id)
    ));
    if (startupIndustryIds.length < 1 || startupIndustryIds.length > 3
      || new Set(startupIndustryIds).size !== startupIndustryIds.length
      || activeIndustries.length !== startupIndustryIds.length) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Select between 1 and 3 active startup industries.');
    }
    team.projectName = projectName;
    team.projectDescription = description;
    team.startupIndustryIds = startupIndustryIds;
    team.startupIndustries = startupIndustryIds.map((id) => activeIndustries.find((industry) => industry.id === id)!.name);
    const createdAtUtc = new Date().toISOString();
    team.projectCreatedAtUtc = createdAtUtc;
    team.projectUpdatedAtUtc = null;
    team.projectActivities = [{
      id: uuid(1701),
      action: 'WORKSPACE_CREATED',
      summary: 'Created the project workspace.',
      actorUserId: currentUser.id,
      actorName: currentUser.name,
      changedFields: ['projectName', 'description', 'startupIndustries'],
      occurredAtUtc: createdAtUtc,
    }];
    let direction = state.directions.find((item) => item.teamId === teamId);
    if (!direction) {
      direction = {
        id: allocateId(),
        teamId,
        title: projectName,
        summary: description,
        startupIndustries: team.startupIndustries,
        status: 'Submitted',
        submittedAtUtc: createdAtUtc,
        reviewedAtUtc: null,
        rowVersion: allocateRowVersion(),
        reviews: [],
      };
      state.directions.push(direction);
    } else if (['Draft', 'NeedsRevision'].includes(direction.status)) {
      direction.title = projectName;
      direction.summary = description;
      direction.startupIndustries = team.startupIndustries;
      direction.status = 'Submitted';
      direction.submittedAtUtc = createdAtUtc;
      direction.reviewedAtUtc = null;
      direction.rowVersion = allocateRowVersion();
    }
    persistMockState();
    const project = {
      _id: uuid(1601), teamId, classId: team.classId, subjectId: classByTeam(teamId)!.courseId,
      semesterId: classByTeam(teamId)!.semesterId, projectName, description,
      problem: '', solution: '', targetUsers: '', zaloGroupUrl: '', keywords: [], startupIndustries: team.startupIndustries,
      status: 'Draft', createdAtUtc, updatedAtUtc: null,
    };
    return ok(project, 'Project workspace created.');
  });

  mock.onPut(/^\/workspace\/teams\/[^/]+\/profile$/).reply((config) => {
    const teamId = routeId(config, /^\/workspace\/teams\/([^/]+)\/profile$/);
    const team = teamById(teamId);
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    const state = getMockState();
    const currentUser = state.users.find((user) => user.id === state.sessionUserId);
    if (!currentUser || currentUser.role !== 'STUDENT' || team.leaderId !== currentUser.id) {
      return failure(403, 'WORKSPACE_LEADER_REQUIRED', 'Only the active team leader can update this project profile.');
    }
    if (!team.projectName) return failure(404, 'WORKSPACE_NOT_FOUND', 'This team does not have a project workspace.');

    const body = parseBody(config);
    const projectName = String(body.projectName || '').trim();
    const description = String(body.description || '').trim();
    const problem = String(body.problem || '').trim();
    const solution = String(body.solution || '').trim();
    const targetUsers = String(body.targetUsers || '').trim();
    const zaloGroupUrl = String(body.zaloGroupUrl || '').trim();
    const keywords = Array.isArray(body.keywords) ? body.keywords.map(String) : [];
    let isValidZaloGroupUrl = zaloGroupUrl.length === 0;
    if (zaloGroupUrl.length > 0 && zaloGroupUrl.length <= 500) {
      try {
        const url = new URL(zaloGroupUrl);
        isValidZaloGroupUrl = url.protocol === 'https:' && (url.hostname === 'zalo.me' || url.hostname.endsWith('.zalo.me'));
      } catch {
        isValidZaloGroupUrl = false;
      }
    }
    if (projectName.length < 3 || projectName.length > 200
      || description.length < 20 || description.length > 2000
      || problem.length < 20 || problem.length > 2000
      || solution.length < 20 || solution.length > 2000
      || (targetUsers.length > 0 && (targetUsers.length < 3 || targetUsers.length > 2000))
      || !isValidZaloGroupUrl) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Required project workspace information is missing or invalid.');
    }
    const changedFields = [
      team.projectName !== projectName && 'projectName',
      (team.projectDescription || '') !== description && 'description',
      (team.projectProblem || '') !== problem && 'problem',
      (team.projectSolution || '') !== solution && 'solution',
      (team.projectTargetUsers || '') !== targetUsers && 'targetUsers',
      (team.projectZaloGroupUrl || '') !== zaloGroupUrl && 'zaloGroupUrl',
      JSON.stringify(team.keywords || []) !== JSON.stringify(keywords) && 'keywords',
    ].filter(Boolean) as string[];

    team.projectName = projectName;
    team.projectDescription = description;
    team.projectProblem = problem;
    team.projectSolution = solution;
    team.projectTargetUsers = targetUsers;
    team.projectZaloGroupUrl = zaloGroupUrl;
    team.keywords = keywords;
    if (changedFields.length > 0) {
      const occurredAtUtc = new Date().toISOString();
      team.projectUpdatedAtUtc = occurredAtUtc;
      team.projectActivities = [{
        id: uuid(1701 + (team.projectActivities?.length || 0)),
        action: 'PROJECT_PROFILE_UPDATED',
        summary: 'Updated ' + changedFields.join(', ') + '.',
        actorUserId: currentUser.id,
        actorName: currentUser.name,
        changedFields,
        occurredAtUtc,
      }, ...(team.projectActivities || [])];
    }
    persistMockState();
    return ok(workspaceData(teamId).project, 'Project profile updated.');
  });

  mock.onGet(/^\/workspace\/teams\/[^/]+\/proposal$/).reply((config) => {
    const teamId = routeId(config, /^\/workspace\/teams\/([^/]+)\/proposal$/);
    if (!teamById(teamId)) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    if (!canAccessTeam(teamId)) return failure(403, 'PROJECT_PROPOSAL_ACCESS_DENIED', 'You cannot view this project proposal.');
    const proposal = getMockState().detailedProposals.find((item) => item.teamId === teamId);
    return proposal
      ? ok(proposalResponse(proposal), 'Project proposal retrieved.')
      : failure(404, 'PROJECT_PROPOSAL_NOT_FOUND', 'The team has not created a project proposal.');
  });

  mock.onPost(/^\/workspace\/teams\/[^/]+\/proposal$/).reply((config) => {
    const teamId = routeId(config, /^\/workspace\/teams\/([^/]+)\/proposal$/);
    const team = teamById(teamId);
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    if (!canMutateProposal(teamId)) return failure(403, 'PROJECT_PROPOSAL_ACCESS_DENIED', 'Only active team members can edit the project proposal draft.');
    if (!team.projectName) return failure(404, 'WORKSPACE_NOT_FOUND', 'Create the project workspace before creating a project proposal.');
    const state = getMockState();
    if (state.detailedProposals.some((item) => item.teamId === teamId)) return failure(409, 'PROJECT_PROPOSAL_STATE_INVALID', 'This team already has a project proposal.');
    const body = parseBody(config);
    const content = proposalContent(body);
    const contentError = proposalContentError(content);
    if (contentError) return failure(400, 'PROJECT_PROPOSAL_VALIDATION_ERROR', contentError);
    const user = currentMockUser()!;
    const proposal: MockDetailedProposal = {
      id: allocateId(), projectId: workspaceData(teamId).project._id, teamId, classId: team.classId,
      ...content, status: 'Draft', currentSubmittedVersionId: null, submittedAtUtc: null,
      currentAnalysisJobId: null, currentAnalysisStatus: null,
      approvedAtUtc: null, rejectedAtUtc: null, rowVersion: allocateRowVersion(), reviews: [], versions: [],
    };
    addProposalVersion(proposal, 'DraftSave', String(body.changeNote || ''), user.id);
    state.detailedProposals.push(proposal);
    persistMockState();
    return created(proposalResponse(proposal), 'Project proposal draft created.');
  });

  mock.onPut(/^\/workspace\/proposals\/[^/]+$/).reply((config) => {
    const proposalId = routeId(config, /^\/workspace\/proposals\/([^/]+)$/);
    const proposal = getMockState().detailedProposals.find((item) => item.id === proposalId);
    if (!proposal) return failure(404, 'PROJECT_PROPOSAL_NOT_FOUND', 'The project proposal was not found.');
    if (!canMutateProposal(proposal.teamId)) return failure(403, 'PROJECT_PROPOSAL_ACCESS_DENIED', 'Only active team members can edit the project proposal draft.');
    const body = parseBody(config);
    if (String(body.rowVersion || '') !== proposal.rowVersion) return failure(409, 'PROJECT_PROPOSAL_CONCURRENCY_CONFLICT', 'The supplied rowVersion is stale.');
    if (!['Draft', 'NeedsRevision'].includes(proposal.status)) return failure(409, 'PROJECT_PROPOSAL_STATE_INVALID', 'Only Draft or NeedsRevision proposals can be edited.');
    const content = proposalContent(body);
    const contentError = proposalContentError(content);
    if (contentError) return failure(400, 'PROJECT_PROPOSAL_VALIDATION_ERROR', contentError);
    if (proposalContentKeys.every((key) => proposal[key] === content[key])) return failure(400, 'PROJECT_PROPOSAL_VALIDATION_ERROR', 'Change at least one proposal field before saving a new version.');
    Object.assign(proposal, content, { status: 'Draft', rowVersion: allocateRowVersion() });
    addProposalVersion(proposal, 'DraftSave', String(body.changeNote || ''), currentMockUser()!.id);
    persistMockState();
    return ok(proposalResponse(proposal), 'Project proposal draft version saved.');
  });

  mock.onPost(/^\/workspace\/proposals\/[^/]+\/submit$/).reply((config) => {
    const proposalId = routeId(config, /^\/workspace\/proposals\/([^/]+)\/submit$/);
    const proposal = getMockState().detailedProposals.find((item) => item.id === proposalId);
    if (!proposal) return failure(404, 'PROJECT_PROPOSAL_NOT_FOUND', 'The project proposal was not found.');
    const team = teamById(proposal.teamId)!;
    const body = parseBody(config);
    const user = currentMockUser();
    if (!user || user.role !== 'STUDENT' || team.leaderId !== user.id) return failure(403, 'PROJECT_PROPOSAL_ACCESS_DENIED', 'Only the active team leader can submit the project proposal.');
    if (String(body.rowVersion || '') !== proposal.rowVersion) return failure(409, 'PROJECT_PROPOSAL_CONCURRENCY_CONFLICT', 'The project proposal changed concurrently.');
    if (proposal.status !== 'Draft') return failure(409, 'PROJECT_PROPOSAL_STATE_INVALID', 'Only a saved Draft proposal can be submitted.');
    const direction = getMockState().directions.find((item) => item.teamId === proposal.teamId);
    if (direction?.status !== 'Approved') return failure(409, 'PROJECT_PROPOSAL_DIRECTION_NOT_APPROVED', 'The project direction must be approved before submission.');
    const submissionError = proposalSubmissionError(proposal);
    if (submissionError) return failure(400, 'PROJECT_PROPOSAL_VALIDATION_ERROR', submissionError);
    if (!team.startupIndustries?.length || team.startupIndustries.length > 3) return failure(400, 'PROJECT_PROPOSAL_VALIDATION_ERROR', 'The project must have between 1 and 3 startup industries.');
    const version = addProposalVersion(proposal, 'Submission', String(body.changeNote || ''), user.id);
    proposal.status = 'Submitted';
    proposal.currentSubmittedVersionId = version.id;
    proposal.currentAnalysisJobId = allocateId();
    proposal.currentAnalysisStatus = 'Completed';
    proposal.submittedAtUtc = version.createdAtUtc;
    proposal.approvedAtUtc = null;
    proposal.rejectedAtUtc = null;
    proposal.rowVersion = allocateRowVersion();
    persistMockState();
    return ok(proposalResponse(proposal), 'Project proposal submitted.');
  });

  mock.onGet(/^\/workspace\/proposal-analyses\/[^/]+$/).reply((config) => {
    const jobId = routeId(config, /^\/workspace\/proposal-analyses\/([^/]+)$/);
    const proposal = getMockState().detailedProposals.find((item) => item.currentAnalysisJobId === jobId);
    if (!proposal) return failure(404, 'PROJECT_PROPOSAL_ANALYSIS_NOT_FOUND', 'The proposal analysis job was not found.');
    if (!canAccessTeam(proposal.teamId)) return failure(403, 'PROJECT_PROPOSAL_ANALYSIS_ACCESS_DENIED', 'You cannot view this proposal analysis.');
    const generatedAtUtc = proposal.submittedAtUtc || new Date().toISOString();
    return ok({
      jobId,
      proposalVersionId: proposal.currentSubmittedVersionId,
      status: 'Completed',
      attemptCount: 1,
      candidateScope: 'AllSystem',
      includeCrossSemester: true,
      languageMode: 'VietnameseAndEnglish',
      requestedAtUtc: generatedAtUtc,
      processingStartedAtUtc: generatedAtUtc,
      completedAtUtc: generatedAtUtc,
      failedAtUtc: null,
      failureCode: null,
      canViewDetailedReport: currentMockUser()?.role !== 'STUDENT',
      report: {
        summary: `Báo cáo retrieval đã tiếp nhận đề xuất ${proposal.startupName}. Chưa có proposal lịch sử hợp lệ để đối sánh trong dữ liệu mock.`,
        overlapRisk: 'InsufficientData',
        potentialDifferentiators: [`Đề xuất giá trị đã nêu: ${proposal.valueProposition.slice(0, 220)}`],
        limitations: [
          'Đây là kết quả mô phỏng, không phải kết luận về mức độ trùng lặp hoặc đạo văn.',
          'Đã áp dụng embedding theo từng trường, TF-IDF, Jaccard và hybrid ranking; chưa có diễn giải bằng LLM.',
        ],
        provider: 'Mock',
        model: 'deterministic-v1',
        promptVersion: 'proposal-analysis-mock-v2',
        outputSchemaVersion: 'proposal-analysis-result-v2',
        embeddingProvider: 'DeterministicLocal',
        embeddingModel: 'feature-hashing-384-v1',
        embeddingDimension: 384,
        textSchemaVersion: 'proposal-embedding-text-v1',
        retrievalVersion: 'proposal-overall-top10-hybrid-top3-v3',
        fieldTextSchemaVersion: 'proposal-field-embedding-text-v1',
        fieldScoringVersion: 'proposal-field-weighted-semantic-v1',
        fieldWeights: {
          problem: 0.3,
          solution: 0.3,
          targetCustomers: 0.2,
          valueAndApproach: 0.2,
        },
        lexicalScoringVersion: 'proposal-lexical-unigram-bigram-v1',
        hybridScoringVersion: 'proposal-hybrid-semantic-tfidf-jaccard-v1',
        hybridWeights: {
          semantic: 0.7,
          tfIdf: 0.2,
          jaccard: 0.1,
        },
        retrievalCandidateCount: 0,
        matches: [],
        generatedAtUtc,
      },
    }, 'Project proposal analysis retrieved.');
  });

  mock.onGet(/^\/workspace\/proposals\/[^/]+\/versions$/).reply((config) => {
    const proposalId = routeId(config, /^\/workspace\/proposals\/([^/]+)\/versions$/);
    const proposal = getMockState().detailedProposals.find((item) => item.id === proposalId);
    if (!proposal) return failure(404, 'PROJECT_PROPOSAL_NOT_FOUND', 'The project proposal was not found.');
    if (!canAccessTeam(proposal.teamId)) return failure(403, 'PROJECT_PROPOSAL_ACCESS_DENIED', 'You cannot view this project proposal.');
    return ok([...proposal.versions].reverse().map(versionSummary), 'Project proposal versions retrieved.');
  });

  mock.onGet(/^\/workspace\/proposals\/[^/]+\/versions\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/proposals\/([^/]+)\/versions\/([^/]+)$/);
    const proposal = getMockState().detailedProposals.find((item) => item.id === match?.[1]);
    if (!proposal) return failure(404, 'PROJECT_PROPOSAL_NOT_FOUND', 'The project proposal was not found.');
    if (!canAccessTeam(proposal.teamId)) return failure(403, 'PROJECT_PROPOSAL_ACCESS_DENIED', 'You cannot view this project proposal.');
    const version = proposal.versions.find((item) => item.id === match?.[2]);
    return version ? ok(version, 'Project proposal version retrieved.') : failure(404, 'PROJECT_PROPOSAL_VERSION_NOT_FOUND', 'The project proposal version was not found.');
  });

  mock.onPost(/^\/workspace\/proposals\/[^/]+\/versions\/[^/]+\/restore$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/proposals\/([^/]+)\/versions\/([^/]+)\/restore$/);
    const proposal = getMockState().detailedProposals.find((item) => item.id === match?.[1]);
    if (!proposal) return failure(404, 'PROJECT_PROPOSAL_NOT_FOUND', 'The project proposal was not found.');
    if (!canMutateProposal(proposal.teamId)) return failure(403, 'PROJECT_PROPOSAL_ACCESS_DENIED', 'Only active team members can restore a proposal version.');
    const body = parseBody(config);
    if (String(body.rowVersion || '') !== proposal.rowVersion) return failure(409, 'PROJECT_PROPOSAL_CONCURRENCY_CONFLICT', 'The project proposal changed concurrently.');
    if (!['Draft', 'NeedsRevision'].includes(proposal.status)) return failure(409, 'PROJECT_PROPOSAL_STATE_INVALID', 'Only Draft or NeedsRevision proposals can restore a version.');
    const version = proposal.versions.find((item) => item.id === match?.[2]);
    if (!version) return failure(404, 'PROJECT_PROPOSAL_VERSION_NOT_FOUND', 'The project proposal version was not found.');
    if (proposalContentKeys.every((key) => proposal[key] === version.snapshot[key])) return failure(400, 'PROJECT_PROPOSAL_VALIDATION_ERROR', 'The selected version already matches the current draft.');
    Object.assign(proposal, version.snapshot, { status: 'Draft', rowVersion: allocateRowVersion() });
    addProposalVersion(proposal, 'DraftSave', String(body.changeNote || `Restored version ${version.versionNumber}.`), currentMockUser()!.id);
    persistMockState();
    return ok(proposalResponse(proposal), 'Project proposal version restored as a new draft.');
  });

  mock.onPost(/^\/workspace\/proposals\/[^/]+\/review$/).reply((config) => {
    const proposalId = routeId(config, /^\/workspace\/proposals\/([^/]+)\/review$/);
    const proposal = getMockState().detailedProposals.find((item) => item.id === proposalId);
    if (!proposal) return failure(404, 'PROJECT_PROPOSAL_NOT_FOUND', 'The project proposal was not found.');
    const body = parseBody(config);
    const user = currentMockUser();
    const cls = classByTeam(proposal.teamId);
    if (!user || user.role !== 'LECTURER' || cls?.primaryLecturerId !== user.id) return failure(403, 'PROJECT_PROPOSAL_ACCESS_DENIED', 'Only an assigned lecturer can review this project proposal.');
    if (String(body.rowVersion || '') !== proposal.rowVersion) return failure(409, 'PROJECT_PROPOSAL_CONCURRENCY_CONFLICT', 'The project proposal changed concurrently.');
    if (proposal.status !== 'Submitted') return failure(409, 'PROJECT_PROPOSAL_STATE_INVALID', 'Only a Submitted project proposal can be reviewed.');
    const decision = String(body.decision || '');
    const feedback = String(body.feedback || '').trim();
    if (!['Approved', 'NeedsRevision', 'Rejected'].includes(decision) || feedback.length < 3 || feedback.length > 1000) return failure(400, 'PROJECT_PROPOSAL_VALIDATION_ERROR', 'A valid decision and feedback are required.');
    const occurredAtUtc = new Date().toISOString();
    proposal.reviews.unshift({ id: allocateId(), proposalVersionId: proposal.currentSubmittedVersionId!, fromStatus: proposal.status, toStatus: decision, feedback, reviewedByUserId: user.id, occurredAtUtc });
    proposal.status = decision as MockDetailedProposal['status'];
    proposal.approvedAtUtc = decision === 'Approved' ? occurredAtUtc : null;
    proposal.rejectedAtUtc = decision === 'Rejected' ? occurredAtUtc : null;
    proposal.rowVersion = allocateRowVersion();
    persistMockState();
    return ok(proposalResponse(proposal), 'Project proposal reviewed.');
  });

  mock.onGet(/^\/workspace\/checkpoints\/teams\/[^/]+$/).reply((config) => {
    const teamId = routeId(config, /^\/workspace\/checkpoints\/teams\/([^/]+)$/);
    return teamById(teamId)
      ? ok(checkpointData(teamId), 'Checkpoint data retrieved successfully.')
      : failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
  });

  mock.onGet(/^\/evaluations\/team\/[^/]+\/checkpoints\/\d+\/summary$/).reply((config) => {
    const match = config.url?.match(/^\/evaluations\/team\/([^/]+)\/checkpoints\/(\d+)\/summary$/);
    const teamId = match?.[1] || '';
    const checkpointNumber = Number(match?.[2] || 1);
    return teamById(teamId)
      ? ok(evaluationSummary(checkpointNumber), 'Evaluation summary retrieved successfully.')
      : failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
  });
}
