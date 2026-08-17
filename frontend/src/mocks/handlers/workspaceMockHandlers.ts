import type MockAdapter from 'axios-mock-adapter';
import { failure, getMockState, ok, routeId } from '../mockHelpers.ts';

const uuid = (value: number) => `00000000-0000-4000-8000-${String(value).padStart(12, '0')}`;

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

function workspaceData(teamId: string) {
  const state = getMockState();
  const team = teamById(teamId)!;
  const cls = classByTeam(teamId)!;
  const lecturer = state.users.find((user) => user.id === cls.primaryLecturerId) || null;
  const mentorUserId = team.currentMentorAssignment?.mentor.userId;
  const mentor = mentorUserId ? state.users.find((user) => user.id === mentorUserId) || null : null;
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
    const selectedWorkspace = workspaceOption(teamId);
    return ok({ selectedWorkspace, availableWorkspaces: accessibleTeams().map((team) => workspaceOption(team.id)), accessMode: selectedWorkspace.accessMode });
  });

  mock.onGet(/^\/workspace\/teams\/[^/]+$/).reply((config) => {
    const teamId = routeId(config, /^\/workspace\/teams\/([^/]+)$/);
    return teamById(teamId)
      ? ok(workspaceData(teamId), 'Workspace retrieved successfully.')
      : failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
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
