import type MockAdapter from 'axios-mock-adapter';
import { failure, getMockState, ok, parseBody, persistMockState, routeId } from '../mockHelpers.ts';

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

const checkpointConfig = [
  {
    number: 1,
    title: 'Startup Idea & Team Formation',
    shortDescription: 'Define your startup concept, choose your field, and establish clear member roles.',
    icon: 'Users',
    requirements: ['Team name', 'Startup idea', 'Startup field', 'Member roles'],
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
  const projectCreatedAtUtc = team.projectCreatedAtUtc || '2026-08-20T08:00:00.000Z';
  const members = team.members.map((member) => {
    const user = state.users.find((item) => item.id === member.studentId);
    return {
      _id: member.studentId,
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
      startupField: team.startupField || '',
      technologyStack: team.technologyStack || [],
      keywords: team.keywords || [],
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
      changedFields: ['projectName', 'description', 'startupField', 'technologyStack', 'keywords'],
      occurredAtUtc: projectCreatedAtUtc,
    }] : []),
    proposal: { _id: uuid(1101), status: 'SUBMITTED', projectName: 'CampusLink' },
    latestDeck: { _id: uuid(1102), originalName: 'Phoenix-Founders-Pitch.pdf' },
  };
}

function checkpointData(teamId: string) {
  const team = teamById(teamId)!;
  const uploader = getMockState().users.find((user) => user.id === team.leaderId);
  return {
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
    const weekNumber = Number(config.params?.weekNumber || 1);
    const rows = mockWeeklyTasks.filter((task) => task.teamId === teamId && task.weekNumber === weekNumber);
    return ok({ courseTasks: [], classTasks: [], teamTasks: rows }, 'Team task board retrieved.');
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
    const startupField = String(body.startupField || '').trim();
    const technologyStack = Array.isArray(body.technologyStack) ? body.technologyStack.map(String) : [];
    const keywords = Array.isArray(body.keywords) ? body.keywords.map(String) : [];
    if (projectName.length < 3 || description.length < 20 || startupField.length < 2 || technologyStack.length === 0) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Required project workspace information is missing or invalid.');
    }
    const hasDuplicate = (values: string[]) => new Set(values.map((value) => value.trim().replace(/\s+/g, ' ').toUpperCase())).size !== values.length;
    if (hasDuplicate(technologyStack) || hasDuplicate(keywords)) {
      return failure(409, 'WORKSPACE_TAG_DUPLICATED', 'Duplicate workspace tags are not allowed.');
    }
    team.projectName = projectName;
    team.projectDescription = description;
    team.startupField = startupField;
    team.technologyStack = technologyStack;
    team.keywords = keywords;
    const createdAtUtc = new Date().toISOString();
    team.projectCreatedAtUtc = createdAtUtc;
    team.projectUpdatedAtUtc = null;
    team.projectActivities = [{
      id: uuid(1701),
      action: 'WORKSPACE_CREATED',
      summary: 'Created the project workspace.',
      actorUserId: currentUser.id,
      actorName: currentUser.name,
      changedFields: ['projectName', 'description', 'startupField', 'technologyStack', 'keywords'],
      occurredAtUtc: createdAtUtc,
    }];
    persistMockState();
    const project = {
      _id: uuid(1601), teamId, classId: team.classId, subjectId: classByTeam(teamId)!.courseId,
      semesterId: classByTeam(teamId)!.semesterId, projectName, description, startupField,
      technologyStack, keywords, status: 'Draft', createdAtUtc, updatedAtUtc: null,
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
    const startupField = String(body.startupField || '').trim();
    const technologyStack = Array.isArray(body.technologyStack) ? body.technologyStack.map(String) : [];
    const keywords = Array.isArray(body.keywords) ? body.keywords.map(String) : [];
    if (projectName.length < 3 || description.length < 20 || startupField.length < 2 || technologyStack.length === 0) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Required project workspace information is missing or invalid.');
    }
    const changedFields = [
      team.projectName !== projectName && 'projectName',
      (team.projectDescription || '') !== description && 'description',
      (team.startupField || '') !== startupField && 'startupField',
      JSON.stringify(team.technologyStack || []) !== JSON.stringify(technologyStack) && 'technologyStack',
      JSON.stringify(team.keywords || []) !== JSON.stringify(keywords) && 'keywords',
    ].filter(Boolean) as string[];

    team.projectName = projectName;
    team.projectDescription = description;
    team.startupField = startupField;
    team.technologyStack = technologyStack;
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

  mock.onGet('/workspace/checkpoints/config').reply(() => ok(checkpointConfig));

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
