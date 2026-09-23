import type MockAdapter from 'axios-mock-adapter';
import { allocateId, allocateRowVersion, failure, getMockState, ok, parseBody, persistMockState, routeId } from '../mockHelpers.ts';
import type { MockClass, MockCheckpointFile } from '../mockState.ts';

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

function teamById(teamId: string) {
  return getMockState().teams.find((team) => team.id === teamId);
}

function classByTeam(teamId: string) {
  const team = teamById(teamId);
  return team ? getMockState().classes.find((item) => item.id === team.classId) : undefined;
}

function checkpointDefinitionsForClass(cls: MockClass) {
  return (getMockState().curricula[cls.subjectCode]?.checkpoints || []).map((checkpoint) => ({
    id: uuid(3000 + Number(cls.courseId.slice(-3)) * 10 + checkpoint.number),
    courseId: cls.courseId,
    number: checkpoint.number,
    title: checkpoint.title,
    shortDescription: checkpoint.shortDescription,
    requirements: checkpoint.requirements,
    rubrics: checkpoint.rubrics,
  }));
}

function managedCheckpointClasses(lecturerId: string, params: Record<string, unknown>): MockClass[] {
  const search = String(params.search || '').trim().toLowerCase();
  return getMockState().classes.filter((cls) =>
    cls.primaryLecturerId === lecturerId &&
    (params.status ? cls.status === params.status : ['Active', 'Draft'].includes(cls.status)) &&
    (!params.semester || cls.semesterCode.startsWith(String(params.semester).toUpperCase())) &&
    (!params.year || cls.year === Number(params.year)) &&
    (!params.subjectCode || cls.subjectCode === String(params.subjectCode).toUpperCase()) &&
    (!search || `${cls.classCode} ${cls.subjectCode} ${cls.subjectName}`.toLowerCase().includes(search)));
}

function scheduleFor(classId: string, checkpointId: string) {
  return getMockState().checkpointSchedules[`${classId}:${checkpointId}`];
}

function scheduleStatus(schedule: { startDateUtc: string; endDateUtc: string } | undefined) {
  if (!schedule) return 'NotScheduled';
  const now = Date.now();
  if (now < Date.parse(schedule.startDateUtc)) return 'Upcoming';
  return now <= Date.parse(schedule.endDateUtc) ? 'Open' : 'Closed';
}

function filesForCheckpoint(teamId: string, number: number): MockCheckpointFile[] {
  const stored = getMockState().checkpointFiles[`${teamId}:${number}`];
  if (stored) return stored.map((file, index) => ({ ...file, versionNumber: file.versionNumber ?? index + 1 }));
  if (number !== 1 || teamId !== uuid(601)) return [];
  const leader = getMockState().users.find((user) => user.id === teamById(teamId)?.leaderId);
  return [{
    _id: uuid(1201),
    versionNumber: 1,
    originalName: 'Startup-Idea-Phoenix-Founders.pdf',
    fileType: 'pdf',
    fileSize: 1_482_752,
    uploadedAt: new Date(Date.now() - 86_400_000).toISOString(),
    uploadedBy: { _id: leader?.id || '', name: leader?.name || 'Team leader' },
  }];
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

function canAccessCheckpointTeam(teamId: string) {
  const state = getMockState();
  const user = state.users.find((item) => item.id === state.sessionUserId);
  const team = teamById(teamId);
  const cls = classByTeam(teamId);
  if (!user || !team || !cls) return false;
  if (user.role === 'ADMIN') return true;
  if (user.role === 'LECTURER') return cls.primaryLecturerId === user.id;
  if (user.role === 'MENTOR') return team.currentMentorAssignment?.mentor.userId === user.id;
  return team.members.some((member) => member.studentId === user.id);
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
  const cls = classByTeam(teamId)!;
  const checkpoints = checkpointDefinitionsForClass(cls);
  return {
    subjectCode: cls.subjectCode,
    checkpoints: checkpoints.map((checkpoint) => {
      const schedule = scheduleFor(cls.id, checkpoint.id);
      const status = scheduleStatus(schedule);
      return {
        ...checkpoint,
        startDateUtc: schedule?.startDateUtc || null,
        endDateUtc: schedule?.endDateUtc || null,
        scheduleStatus: status,
        canUpload: status === 'Open',
      };
    }),
    submissions: checkpoints.map((checkpoint) => ({
      checkpointNumber: checkpoint.number,
      status: filesForCheckpoint(teamId, checkpoint.number).length > 0 ? 'Submitted' : 'NotSubmitted',
      files: filesForCheckpoint(teamId, checkpoint.number),
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

function evaluationSummary(teamId: string, checkpointNumber: number) {
  const cls = classByTeam(teamId)!;
  const checkpoint = checkpointDefinitionsForClass(cls).find((item) => item.number === checkpointNumber)!;
  const rubrics = checkpoint.rubrics as Array<{ key: string; label: string; weight: number }>;
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
    rubricScores: rubrics.map((criterion, index) => ({
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
  mock.onGet('/lecturer/checkpoints').reply((config) => {
    const state = getMockState();
    const lecturer = state.users.find((user) => user.id === state.sessionUserId);
    if (lecturer?.role !== 'LECTURER') return failure(403, 'CLASS_ACCESS_DENIED', 'Lecturer access is required.');
    const params = config.params || {};
    const classes = managedCheckpointClasses(lecturer.id, params);
    const definitions = Array.from(new Map(classes.flatMap(checkpointDefinitionsForClass)
      .map((checkpoint) => [checkpoint.id, checkpoint])).values());
    const selectedClasses = params.classId
      ? classes.filter((cls) => cls.id === String(params.classId))
      : classes;
    const availableDefinitions = definitions.filter((checkpoint) =>
      selectedClasses.some((cls) => cls.courseId === checkpoint.courseId));
    const selectedDefinitions = availableDefinitions.filter((checkpoint) =>
      (!params.checkpointId || checkpoint.id === String(params.checkpointId)) &&
      (!params.checkpointNumber || checkpoint.number === Number(params.checkpointNumber)));
    const schedules = selectedClasses.flatMap((cls) => selectedDefinitions
      .filter((checkpoint) => checkpoint.courseId === cls.courseId)
      .map((checkpoint) => {
        const schedule = scheduleFor(cls.id, checkpoint.id);
        return {
          id: schedule?.id || null,
          classId: cls.id,
          classCode: cls.classCode,
          checkpointId: checkpoint.id,
          checkpointNumber: checkpoint.number,
          checkpointTitle: checkpoint.title,
          startDateUtc: schedule?.startDateUtc || null,
          endDateUtc: schedule?.endDateUtc || null,
          status: scheduleStatus(schedule),
          canReopen: Boolean(schedule && Date.now() > Date.parse(schedule.endDateUtc)),
          reopenCount: schedule?.reopenCount || 0,
        };
      }));
    const submissions = selectedClasses.flatMap((cls) => state.teams
      .filter((team) => team.classId === cls.id && team.status === 'Active')
      .flatMap((team) => selectedDefinitions.filter((checkpoint) => checkpoint.courseId === cls.courseId)
        .map((checkpoint) => {
          const files = [...filesForCheckpoint(team.id, checkpoint.number)]
            .sort((left, right) => Date.parse(left.uploadedAt) - Date.parse(right.uploadedAt));
          const checkpointSchedule = scheduleFor(cls.id, checkpoint.id);
          const status = scheduleStatus(checkpointSchedule);
          return {
            classId: cls.id,
            classCode: cls.classCode,
            teamId: team.id,
            teamName: team.teamName,
            checkpointId: checkpoint.id,
            checkpointNumber: checkpoint.number,
            checkpointTitle: checkpoint.title,
            status: status === 'Upcoming' ? 'Upcoming' : files.length > 0 ? 'Submitted' : status === 'Open' ? 'Pending' : status,
            latestSubmissionAtUtc: files.at(-1)?.uploadedAt || null,
            earliestSubmittedFile: files[0] ? {
              id: files[0]._id,
              originalName: files[0].originalName,
              uploadedAtUtc: files[0].uploadedAt,
            } : null,
          };
        })));
    return ok({
      serverTimeUtc: new Date().toISOString(),
      classes: classes.map((cls) => ({
        id: cls.id, classCode: cls.classCode, subjectCode: cls.subjectCode,
        subjectName: cls.subjectName, semesterCode: cls.semesterCode, year: cls.year,
      })),
      checkpoints: availableDefinitions.map(({ id, courseId, number, title, shortDescription }) => ({
        id, courseId, number, title, shortDescription,
      })),
      schedules,
      submissions,
    }, 'Lecturer checkpoint overview retrieved.');
  });

  mock.onPut(/^\/lecturer\/checkpoints\/classes\/[^/]+\/definitions\/[^/]+\/schedule$/).reply((config) => {
    const match = config.url?.match(/^\/lecturer\/checkpoints\/classes\/([^/]+)\/definitions\/([^/]+)\/schedule$/);
    const classId = match?.[1] || '';
    const checkpointId = match?.[2] || '';
    const state = getMockState();
    const lecturer = state.users.find((user) => user.id === state.sessionUserId);
    const cls = state.classes.find((item) => item.id === classId);
    if (lecturer?.role !== 'LECTURER' || cls?.primaryLecturerId !== lecturer.id)
      return failure(403, 'CLASS_ACCESS_DENIED', 'You do not have permission to manage this class.');
    const checkpoint = checkpointDefinitionsForClass(cls).find((item) => item.id === checkpointId);
    if (!checkpoint) return failure(404, 'COMMON_NOT_FOUND', 'The checkpoint is not configured for this class subject.');
    const body = parseBody(config);
    const start = Date.parse(String(body.startDateUtc || ''));
    const end = Date.parse(String(body.endDateUtc || ''));
    if (!Number.isFinite(start) || !Number.isFinite(end) || start >= end)
      return failure(400, 'VALIDATION_ERROR', 'Start date must be earlier than end date.');
    const key = `${classId}:${checkpointId}`;
    const previous = state.checkpointSchedules[key];
    const reopened = Boolean(previous && Date.now() > Date.parse(previous.endDateUtc));
    if (reopened && end <= Date.now())
      return failure(400, 'VALIDATION_ERROR', 'A reopened checkpoint must end in the future.');
    const schedule = {
      id: previous?.id || allocateId(),
      classId,
      checkpointId,
      startDateUtc: new Date(start).toISOString(),
      endDateUtc: new Date(end).toISOString(),
      reopenCount: (previous?.reopenCount || 0) + (reopened ? 1 : 0),
    };
    state.checkpointSchedules[key] = schedule;
    state.audits[classId] ??= [];
    state.audits[classId].push({
      id: allocateId(),
      action: reopened ? 'CheckpointReopened' : previous ? 'CheckpointScheduleUpdated' : 'CheckpointScheduled',
      performedByUserId: lecturer.id,
      performedByName: lecturer.name,
      occurredAtUtc: new Date().toISOString(),
      detailsJson: JSON.stringify({
        checkpointId,
        checkpointNumber: checkpoint.number,
        oldStartDateUtc: previous?.startDateUtc || null,
        oldEndDateUtc: previous?.endDateUtc || null,
        newStartDateUtc: schedule.startDateUtc,
        newEndDateUtc: schedule.endDateUtc,
      }),
    });
    persistMockState();
    return ok({
      ...schedule,
      classCode: cls.classCode,
      checkpointNumber: checkpoint.number,
      checkpointTitle: checkpoint.title,
      status: scheduleStatus(schedule),
      canReopen: Date.now() > end,
    }, 'Checkpoint schedule saved.');
  });

  mock.onPut('/lecturer/checkpoints/schedules/bulk').reply((config) => {
    const state = getMockState();
    const lecturer = state.users.find((user) => user.id === state.sessionUserId);
    if (lecturer?.role !== 'LECTURER') return failure(403, 'CLASS_ACCESS_DENIED', 'Lecturer access is required.');
    const body = parseBody(config);
    const checkpointNumber = Number(body.checkpointNumber);
    const expected = Array.isArray(body.expectedClassIds) ? body.expectedClassIds.map(String) : [];
    const classes = managedCheckpointClasses(lecturer.id, body);
    const targets = classes.filter((cls) =>
      checkpointDefinitionsForClass(cls).some((checkpoint) => checkpoint.number === checkpointNumber));
    if (targets.length === 0)
      return failure(404, 'COMMON_NOT_FOUND', 'This checkpoint is not configured for any class in your scope.');
    if (expected.length === 0 || new Set(expected).size !== expected.length ||
      expected.slice().sort().join('|') !== targets.map((cls) => cls.id).sort().join('|'))
      return failure(403, 'CLASS_ACCESS_DENIED', 'The class selection changed or includes a class outside your scope. Refresh and try again.');
    const start = Date.parse(String(body.startDateUtc || ''));
    const end = Date.parse(String(body.endDateUtc || ''));
    if (!Number.isFinite(start) || !Number.isFinite(end) || start >= end)
      return failure(400, 'VALIDATION_ERROR', 'Start date must be earlier than end date.');
    const now = Date.now();
    if (end <= now && targets.some((cls) => {
      const checkpointId = checkpointDefinitionsForClass(cls).find((item) => item.number === checkpointNumber)!.id;
      const prior = scheduleFor(cls.id, checkpointId);
      return prior && Date.parse(prior.endDateUtc) < now;
    })) return failure(400, 'VALIDATION_ERROR', 'A reopened checkpoint must end in the future.');

    const schedules = targets.map((cls) => {
      const checkpoint = checkpointDefinitionsForClass(cls).find((item) => item.number === checkpointNumber)!;
      const checkpointId = checkpoint.id;
      const key = `${cls.id}:${checkpointId}`;
      const previous = state.checkpointSchedules[key];
      const reopened = Boolean(previous && Date.parse(previous.endDateUtc) < now);
      const schedule = {
        id: previous?.id || allocateId(), classId: cls.id, checkpointId,
        startDateUtc: new Date(start).toISOString(), endDateUtc: new Date(end).toISOString(),
        reopenCount: (previous?.reopenCount || 0) + (reopened ? 1 : 0),
      };
      state.checkpointSchedules[key] = schedule;
      state.audits[cls.id] ??= [];
      state.audits[cls.id].push({
        id: allocateId(),
        action: reopened ? 'CheckpointReopened' : previous ? 'CheckpointScheduleUpdated' : 'CheckpointScheduled',
        performedByUserId: lecturer.id,
        performedByName: lecturer.name,
        occurredAtUtc: new Date(now).toISOString(),
        detailsJson: JSON.stringify({
          checkpointId, checkpointNumber: checkpoint.number,
          oldStartDateUtc: previous?.startDateUtc || null, oldEndDateUtc: previous?.endDateUtc || null,
          newStartDateUtc: schedule.startDateUtc, newEndDateUtc: schedule.endDateUtc,
        }),
      });
      return {
        ...schedule, classCode: cls.classCode, checkpointNumber: checkpoint.number,
        checkpointTitle: checkpoint.title, status: scheduleStatus(schedule),
        canReopen: now > end,
      };
    });
    persistMockState();
    return ok({ appliedClassCount: schedules.length, schedules }, 'Checkpoint schedule applied to all selected classes.');
  });

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

  mock.onGet(/^\/workspace\/checkpoints\/teams\/[^/]+$/).reply((config) => {
    const teamId = routeId(config, /^\/workspace\/checkpoints\/teams\/([^/]+)$/);
    return canAccessCheckpointTeam(teamId)
      ? ok(checkpointData(teamId), 'Checkpoint data retrieved successfully.')
      : failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this team workspace.');
  });

  mock.onPost(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/upload$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/upload$/);
    const teamId = match?.[1] || '';
    const number = Number(match?.[2]);
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    const cls = classByTeam(teamId);
    if (user?.role !== 'STUDENT' || !canAccessCheckpointTeam(teamId) || !cls)
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only active team students can upload documents.');
    const checkpoint = checkpointDefinitionsForClass(cls).find((item) => item.number === number);
    if (!checkpoint) return failure(404, 'WORKSPACE_NOT_FOUND', 'The checkpoint workspace was not found.');
    const schedule = scheduleFor(cls.id, checkpoint.id);
    if (scheduleStatus(schedule) !== 'Open')
      return failure(400, 'WORKSPACE_CHECKPOINT_NOT_OPEN', 'This checkpoint is not open for uploads.');
    const file = config.data instanceof FormData ? config.data.get('file') : null;
    if (!(file instanceof File)) return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'A file is required.');
    const extension = file.name.split('.').at(-1)?.toLowerCase() || '';
    if (!['pdf', 'docx', 'pptx'].includes(extension) || file.size <= 0 || file.size > 15 * 1024 * 1024)
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Only PDF, DOCX, and PPTX files up to 15 MB are accepted.');
    const key = `${teamId}:${number}`;
    const existingFiles = filesForCheckpoint(teamId, number);
    const uploaded = {
      _id: allocateId(),
      versionNumber: Math.max(0, ...existingFiles.map((item) => item.versionNumber ?? 0)) + 1,
      originalName: file.name,
      fileType: extension,
      fileSize: file.size,
      uploadedAt: new Date().toISOString(),
      uploadedBy: { _id: user.id, name: user.name },
    };
    state.checkpointFiles[key] ??= existingFiles;
    state.checkpointFiles[key].push(uploaded);
    persistMockState();
    return ok(uploaded, 'File uploaded.');
  });

  mock.onGet(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/files\/[^/]+\/download$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/files\/([^/]+)\/download$/);
    const teamId = match?.[1] || '';
    const number = Number(match?.[2]);
    const fileId = match?.[3] || '';
    if (!canAccessCheckpointTeam(teamId)) return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this team workspace.');
    const file = filesForCheckpoint(teamId, number).find((item) => item._id === fileId);
    if (!file) return failure(404, 'COMMON_NOT_FOUND', 'Submitted file was not found.');
    return [200, new Blob([`Mock submitted file: ${file.originalName}`], { type: 'application/octet-stream' }),
      { 'content-type': 'application/octet-stream' }];
  });

  mock.onGet(/^\/evaluations\/team\/[^/]+\/checkpoints\/\d+\/summary$/).reply((config) => {
    const match = config.url?.match(/^\/evaluations\/team\/([^/]+)\/checkpoints\/(\d+)\/summary$/);
    const teamId = match?.[1] || '';
    const checkpointNumber = Number(match?.[2] || 1);
    return canAccessCheckpointTeam(teamId) && checkpointDefinitionsForClass(classByTeam(teamId)!).some((item) => item.number === checkpointNumber)
      ? ok(evaluationSummary(teamId, checkpointNumber), 'Evaluation summary retrieved successfully.')
      : failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
  });
}
