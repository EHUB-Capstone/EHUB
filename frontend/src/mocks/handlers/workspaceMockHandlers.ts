import type MockAdapter from 'axios-mock-adapter';
import { allocateId, allocateRowVersion, failure, getMockState, ok, parseBody, persistMockState, routeId } from '../mockHelpers.ts';
import type { MockClass, MockCheckpointFile, MockCheckpointLink } from '../mockState.ts';
import { normalizeCheckpointLinkUrl, validateCheckpointLinkUrl } from '../../utils/checkpointLink.ts';
import type { SubmissionAnalyticsItem } from '../../types/submissionAnalytics';

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
    courseWeight: checkpoint.courseWeight,
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

function linksForCheckpoint(teamId: string, number: number): MockCheckpointLink[] {
  return getMockState().checkpointLinks[`${teamId}:${number}`] || [];
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
    return state.teams.filter((team) => team.currentMentorAssignments.some(assignment => assignment.mentor.userId === currentUser.id));
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
  if (user.role === 'MENTOR') return team.currentMentorAssignments.some(assignment => assignment.mentor.userId === user.id);
  return team.members.some((member) => member.studentId === user.id);
}

function workspaceData(teamId: string) {
  const state = getMockState();
  const viewer = state.users.find(item => item.id === state.sessionUserId);
  const isStaff = viewer && ['ADMIN', 'LECTURER'].includes(viewer.role);
  const team = teamById(teamId)!;
  const cls = classByTeam(teamId)!;
  const lecturer = state.users.find((user) => user.id === cls.primaryLecturerId) || null;
  const mentors = team.currentMentorAssignments.map(assignment => ({
    assignment,
    user: state.users.find(user => user.id === assignment.mentor.userId),
  })).filter(item => item.user);
  const proposal = state.proposals.find((item) => item.approvedTeamId === teamId) || null;
  const projectCreatedAtUtc = team.projectCreatedAtUtc || '2026-08-20T08:00:00.000Z';
  const members = team.members.map((member) => {
    const user = state.users.find((item) => item.id === member.studentId);
    return {
      _id: member.studentId,
      studentId: member.studentId,
      userId: isStaff || member.studentId === viewer?.id
        ? user ? { _id: user.id, name: user.name, email: user.email } : { _id: member.studentId } : null,
      fullName: member.fullName,
      email: isStaff || member.studentId === viewer?.id ? member.email : null,
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
    mentor: mentors[0]?.user ? { _id: mentors[0].user.id, name: mentors[0].user.name, email: mentors[0].user.email, label: mentors[0].assignment.slot } : null,
    mentors: mentors.map(item => ({ _id: item.user!.id, name: item.user!.name, email: item.user!.email, label: item.assignment.slot })),
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
      status: filesForCheckpoint(teamId, checkpoint.number).length > 0 || linksForCheckpoint(teamId, checkpoint.number).length > 0 ? 'Submitted' : 'NotSubmitted',
      files: filesForCheckpoint(teamId, checkpoint.number),
      links: linksForCheckpoint(teamId, checkpoint.number),
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
  const state = getMockState();
  const cls = classByTeam(teamId)!;
  const checkpoint = checkpointDefinitionsForClass(cls).find((item) => item.number === checkpointNumber)!;
  const rubrics = checkpoint.rubrics as Array<{ key: string; label: string; weight: number }>;
  const hasEvaluation = checkpointNumber === 1;
  const members = teamById(teamId)?.members || [];
  const evaluationId = uuid(140000 + Number(teamId.slice(-3)) * 10 + checkpointNumber);
  const status = state.evaluationPublicationStatuses[evaluationId] || 'SUBMITTED';
  const currentUser = state.users.find(user => user.id === state.sessionUserId);
  const isInternal = currentUser?.role === 'ADMIN' || currentUser?.role === 'LECTURER';
  const scoresPublished = status === 'PUBLISHED';
  const canViewTeamScore = isInternal;
  const currentStudentId = Object.values(state.rosters).flat()
    .find(student => student.userId === currentUser?.id)?.studentId;
  const evaluation = {
    _id: evaluationId,
    lecturerId: { _id: uuid(2), name: 'Trần Thu Giang' },
    evaluatorRole: 'LECTURER',
    status,
    checkpointNumber,
    ...(canViewTeamScore ? { checkpointTotal: 7.85 } : {}),
    overallFeedback: 'Good early direction. Strengthen customer evidence and clarify the validation plan.',
    updatedAt: new Date(Date.now() - 21_600_000).toISOString(),
    rubricScores: rubrics.map((criterion, index) => ({
      criterionKey: criterion.key,
      criterionName: criterion.label,
      ...(isInternal
        ? { score: index === 1 ? 7.5 : 8.5 }
        : {}),
      comment: index === 1 ? 'Include more direct customer quotes and quantified findings.' : 'Clear and focused.',
    })),
    ...(isInternal ? {
      memberScores: members.map((member, index) => ({
        studentId: member.studentId,
        score: index === 0 ? 7.5 : 7.85,
        isOverridden: index === 0,
      })),
    } : currentUser?.role === 'STUDENT' && scoresPublished ? {
      memberScores: members.map((member, index) => ({
        studentId: member.studentId,
        score: index === 0 ? 7.5 : 7.85,
        isOverridden: index === 0,
      })).filter(member => member.studentId === currentStudentId),
    } : {}),
  };

  return {
    checkpoint: { ...checkpoint, members: members.filter(member => isInternal ||
      (currentUser?.role === 'STUDENT' && member.studentId === currentStudentId))
      .map(member => ({ studentId: member.studentId, fullName: member.fullName, rollNumber: member.rollNumber })) },
    evaluations: hasEvaluation ? [evaluation] : [],
    summary: {
      evaluationCount: hasEvaluation ? 1 : 0,
      submittedCount: hasEvaluation ? 1 : 0,
      ...(hasEvaluation && canViewTeamScore ? { averageScore: 7.85 } : {}),
    },
    history: hasEvaluation && isInternal ? [{
      _id: uuid(1501),
      action: 'SUBMITTED',
      version: 1,
      changedBy: { _id: uuid(2), name: 'Trần Thu Giang' },
      createdAt: evaluation.updatedAt,
      changes: isInternal ? [
        { category: 'STATUS', field: 'status', label: 'Status', previousValue: null, currentValue: 'SUBMITTED' },
        { category: 'SCORE', field: 'totalScore', label: 'Team score', previousValue: null, currentValue: '7.85' },
        { category: 'FEEDBACK', field: 'overallFeedback', label: 'Overall feedback', previousValue: null, currentValue: 'Good early direction. Strengthen customer evidence and clarify the validation plan.' },
        ...rubrics.flatMap((criterion, index) => [
          { category: 'SCORE', field: `rubricScore:${criterion.key}`, label: `${criterion.label} score`, previousValue: null, currentValue: index === 1 ? '7.5' : '8.5' },
          { category: 'FEEDBACK', field: `rubricComment:${criterion.key}`, label: `${criterion.label} comment`, previousValue: null, currentValue: index === 1 ? 'Include more direct customer quotes and quantified findings.' : 'Clear and focused.' },
        ]),
        ...members.map((member, index) => ({ category: 'SCORE', field: `memberScore:${member.studentId}`, label: `${member.fullName} (${member.rollNumber}) score`, previousValue: null, currentValue: index === 0 ? '7.5' : '7.85' })),
      ] : [],
    }] : [],
  };
}

function courseAssessmentData(teamId: string) {
  const state = getMockState();
  const cls = classByTeam(teamId)!;
  const currentUser = state.users.find(user => user.id === state.sessionUserId);
  const isInternal = currentUser?.role === 'ADMIN' || currentUser?.role === 'LECTURER';
  const team = teamById(teamId)!;
  const assessments = (state.curricula[cls.subjectCode]?.otherAssessments || []).map(item => {
    const score = state.courseAssessmentScores[`${teamId}:${item._id}`];
    const evaluationId = `${teamId}--${item._id}`;
    const status = score === undefined ? 'NOT_GRADED' : state.evaluationPublicationStatuses[evaluationId] || 'SUBMITTED';
    const visibleMembers = isInternal
      ? team.members
      : currentUser?.role === 'STUDENT' && status === 'PUBLISHED'
        ? team.members.filter(member => member.studentId === currentUser.id)
        : [];
    return {
      assessmentId: item._id,
      name: item.name,
      weight: item.weight,
      evaluationId: score === undefined ? null : evaluationId,
      evaluatorId: score === undefined ? null : cls.primaryLecturerId,
      ...(isInternal && score !== undefined ? { score } : {}),
      ...(score !== undefined && visibleMembers.length > 0 ? {
        memberScores: visibleMembers.map(member => {
          const key = `${teamId}:${item._id}:${member.studentId}`;
          const hasOverride = Object.prototype.hasOwnProperty.call(state.courseAssessmentMemberScores, key);
          return { studentId: member.studentId, score: hasOverride ? state.courseAssessmentMemberScores[key] : score, isOverridden: hasOverride };
        }),
      } : {}),
      status,
      updatedAt: score === undefined ? null : new Date().toISOString(),
    };
  });
  return { assessments };
}

const MOCK_MAX_UPLOAD_BYTES = 100 * 1024 * 1024;
const MOCK_DIRECT_DOWNLOAD_THRESHOLD_BYTES = 10 * 1024 * 1024;
const MOCK_STORAGE_PREFIX = '/__mock_r2__/';

interface MockUploadSession {
  teamId: string;
  number: number;
  fileName: string;
  extension: string;
  size: number;
  userId: string;
  putDone: boolean;
  file?: MockCheckpointFile;
}

// Not persisted: an upload session only matters within one page load.
const mockUploadSessions = new Map<string, MockUploadSession>();

/** Stands in for the presigned R2 URL: the browser PUTs here through storageClient. */
export function registerStorageMockHandlers(mock: MockAdapter): void {
  mock.onPut(new RegExp(`^${MOCK_STORAGE_PREFIX}[^/]+$`)).reply((config) => {
    const uploadId = config.url?.slice(MOCK_STORAGE_PREFIX.length) || '';
    const session = mockUploadSessions.get(uploadId);
    if (!session) return [403, '<Error><Code>AccessDenied</Code></Error>'];
    const bytes = config.data instanceof Blob ? config.data.size : session.size;
    if (bytes !== session.size) return [403, '<Error><Code>SignatureDoesNotMatch</Code></Error>'];
    session.putDone = true;
    return [200];
  });
}

export function registerWorkspaceMockHandlers(mock: MockAdapter): void {
  mock.onGet(/^\/workspace\/checkpoints\/classes\/[^/]+\/students\/[^/]+\/previous-scores$/).reply((config) => {
    const [, classId, studentId] = config.url!.match(/^\/workspace\/checkpoints\/classes\/([^/]+)\/students\/([^/]+)\/previous-scores$/)!;
    const state = getMockState();
    const user = state.users.find(item => item.id === state.sessionUserId);
    if (!user) return failure(401, 'UNAUTHORIZED', 'Authentication is required.');
    const cls = state.classes.find(item => item.id === classId);
    const enrollment = state.rosters[classId]?.find(item => item.studentId === studentId && item.enrollmentStatus === 'Active');
    if (!['ADMIN', 'LECTURER'].includes(user.role) || !cls || cls.status !== 'Active' || !enrollment ||
        !state.semesters.some(item => item.id === cls.semesterId && item.status === 'Active') ||
        (user.role !== 'ADMIN' && cls.primaryLecturerId !== user.id))
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this student\'s previous scores.');
    const termOrder: Record<string, number> = { SP: 0, SU: 1, FA: 2 };
    const previous = state.semesters.filter(item => ['Completed', 'Archived'].includes(item.status) &&
      (item.year < cls.year || item.year === cls.year && termOrder[item.semester] < termOrder[cls.semesterCode.slice(0, 2)]))
      .sort((left, right) => right.year - left.year || termOrder[right.semester] - termOrder[left.semester])[0];
    const oldClass = previous && state.classes.find(item => item.semesterId === previous.id && item.courseId === cls.courseId &&
      state.rosters[item.id]?.some(student => student.studentId === studentId && student.enrollmentStatus === 'Completed'));
    const oldTeam = oldClass && state.teams.find(team => team.classId === oldClass.id && team.members.some(member => member.studentId === studentId));
    const assessments = oldTeam ? (state.curricula[cls.subjectCode]?.otherAssessments || []).flatMap(assessment => {
      const evaluationId = `${oldTeam.id}--${assessment._id}`;
      const teamScore = state.courseAssessmentScores[`${oldTeam.id}:${assessment._id}`];
      if (teamScore === undefined || state.evaluationPublicationStatuses[evaluationId] !== 'PUBLISHED') return [];
      return [{ assessmentId: assessment._id, checkpointNumber: null, name: assessment.name, weight: assessment.weight,
        score: state.courseAssessmentMemberScores[`${oldTeam.id}:${assessment._id}:${studentId}`] ?? teamScore }];
    }) : [];
    const checkpoints = oldTeam ? checkpointDefinitionsForClass(oldClass!).flatMap(checkpoint => {
      const evaluationId = uuid(140000 + Number(oldTeam.id.slice(-3)) * 10 + checkpoint.number);
      if (checkpoint.number !== 1 || state.evaluationPublicationStatuses[evaluationId] !== 'PUBLISHED') return [];
      return [{ assessmentId: checkpoint.id, checkpointNumber: checkpoint.number, name: checkpoint.title,
        weight: checkpoint.courseWeight, score: oldTeam.members[0]?.studentId === studentId ? 7.5 : 7.85 }];
    }) : [];
    return ok({ studentId, semesterCode: previous ? `${previous.semester}${previous.year}` : null, components: [...checkpoints, ...assessments] });
  });
  mock.onGet('/rankings').reply((config) => {
    const state = getMockState();
    const currentUser = state.users.find(user => user.id === state.sessionUserId);
    if (!currentUser) return failure(401, 'UNAUTHORIZED', 'Authentication is required.');
    if (config.params?.checkpointNumber !== undefined && (!Number.isInteger(Number(config.params.checkpointNumber)) || Number(config.params.checkpointNumber) < 1))
      return failure(400, 'VALIDATION_ERROR', 'Checkpoint number must be positive.');
    if (!['ADMIN', 'LECTURER'].includes(currentUser.role)) {
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only administrators and lecturers can view team rankings.');
    }

    const scopedClasses = state.classes.filter(cls => currentUser.role === 'ADMIN' || cls.primaryLecturerId === currentUser.id);
    const requestedClass = String(config.params?.classId || '');
    if (requestedClass && !scopedClasses.some(cls => cls.id === requestedClass))
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'The selected class is outside your ranking scope.');
    const availableSemesterIds = new Set(scopedClasses.map(cls => cls.semesterId));
    const availableSemesters = state.semesters
      .filter(item => availableSemesterIds.has(item.id))
      .sort((left, right) => Number(right.status === 'Active') - Number(left.status === 'Active') || right.year - left.year)
      .map(item => ({ id: item.id, semester: item.semester, year: item.year, code: `${item.semester}${item.year}`, isActive: item.status === 'Active' }));
    const requestedSemester = String(config.params?.semester || '').toUpperCase();
    const requestedYear = Number(config.params?.year || 0);
    const activeSemester = availableSemesters.find(item => item.isActive) || null;
    const selectedSemester = availableSemesters.find(item =>
      (!requestedSemester || item.semester === requestedSemester || item.code === requestedSemester) &&
      (!requestedYear || item.year === requestedYear)) || activeSemester || availableSemesters[0] || null;
    const selectedClasses = selectedSemester
      ? scopedClasses.filter(cls => cls.semesterId === selectedSemester.id && (!requestedClass || cls.id === requestedClass))
      : [];
    const selectedClassIds = new Set(selectedClasses.map(cls => cls.id));
    const items = state.teams.filter(team => team.status === 'Active' && selectedClassIds.has(team.classId)).map(team => {
      const cls = state.classes.find(item => item.id === team.classId)!;
      const curriculum = state.curricula[cls.subjectCode];
      const checkpoints = (curriculum?.checkpoints || []).map(checkpoint => {
        const evaluationId = uuid(140000 + Number(team.id.slice(-3)) * 10 + checkpoint.number);
        const status = state.evaluationPublicationStatuses[evaluationId] || 'SUBMITTED';
        return {
          checkpointId: uuid(3000 + Number(cls.courseId.slice(-3)) * 10 + checkpoint.number),
          number: checkpoint.number,
          title: checkpoint.title,
          weight: checkpoint.courseWeight,
          score: checkpoint.number === 1 ? 7.85 : null,
          status: checkpoint.number === 1 ? status : 'NOT_GRADED',
        };
      });
      const assessments = (curriculum?.otherAssessments || []).map(assessment => {
        const score = state.courseAssessmentScores[`${team.id}:${assessment._id}`];
        const evaluationId = `${team.id}--${assessment._id}`;
        return {
          assessmentId: assessment._id,
          name: assessment.name,
          weight: assessment.weight,
          score: score ?? null,
          status: score === undefined ? 'NOT_GRADED' : state.evaluationPublicationStatuses[evaluationId] || 'SUBMITTED',
        };
      });
      const components = config.params?.checkpointNumber
        ? checkpoints.filter(item => item.number === Number(config.params.checkpointNumber)) : checkpoints;
      const complete = components.length > 0 && components.every(item => item.score !== null);
      const published = complete && components.every(item => item.status === 'PUBLISHED');
      const courseTotal = published
        ? config.params?.checkpointNumber ? components[0].score
          : Math.round(components.reduce((sum, item) => sum + Number(item.score) * item.weight / 100, 0) * 100) / 100
        : null;
      return {
        teamId: team.id,
        teamName: team.teamName,
        teamCode: team.teamCode,
        projectName: team.projectName || '',
        projectDescription: team.projectDescription || team.description || '',
        semesterGroupName: [...new Set((state.rosters[cls.id] || [])
          .filter(student => student.teamId === team.id && student.semesterGroupName?.trim())
          .map(student => student.semesterGroupName!.trim()))]
          .sort((left, right) => left.localeCompare(right, undefined, { numeric: true }))
          .join(', '),
        classId: cls.id,
        classCode: cls.classCode,
        courseCode: cls.subjectCode,
        semester: cls.semesterCode,
        year: cls.year,
        checkpoints,
        assessments,
        courseTotal,
        status: published ? 'PUBLISHED' : 'INCOMPLETE',
        completedComponentCount: components.filter(item => item.score !== null).length,
        publishedComponentCount: components.filter(item => item.score !== null && item.status === 'PUBLISHED').length,
        totalComponentCount: components.length,
        lastUpdatedAt: components.some(item => item.score !== null) ? new Date().toISOString() : null,
      };
    });
    const rankedItems = items.map(item => {
      const scope = items.filter(other => other.courseCode === item.courseCode && other.courseTotal !== null);
      const rank = item.courseTotal === null ? null : 1 + scope.filter(other => Number(other.courseTotal) > Number(item.courseTotal)).length;
      const { courseTotal: _total, ...safeItem } = item;
      return { ...safeItem, rank,
        checkpoints: item.checkpoints.map(({ score: _score, ...checkpoint }) => checkpoint),
        assessments: item.assessments.map(({ score: _score, ...assessment }) => assessment) };
    });
    return ok({ activeSemester, selectedSemester, availableSemesters, items: rankedItems }, 'Team rankings retrieved.');
  });

  mock.onGet('/dashboard/submission-analytics').reply((config) => {
    const state = getMockState();
    const user = state.users.find(item => item.id === state.sessionUserId);
    if (!user) return failure(401, 'UNAUTHORIZED', 'Authentication is required.');
    if (!['ADMIN', 'LECTURER'].includes(user.role)) return failure(403, 'CLASS_ACCESS_DENIED', 'Staff access is required.');
    const params = config.params || {};
    if ((params.semester && !['SP', 'SU', 'FA'].includes(String(params.semester).trim().toUpperCase())) ||
        (params.year !== undefined && (!Number.isInteger(Number(params.year)) || Number(params.year) < 2000 || Number(params.year) > 9999)) ||
        (params.checkpointNumber !== undefined && (!Number.isInteger(Number(params.checkpointNumber)) || Number(params.checkpointNumber) < 1))) {
      return failure(400, 'CLASS_VALIDATION_ERROR', 'Invalid submission analytics filters.');
    }
    const accessibleClasses = state.classes.filter(cls => cls.status !== 'Archived' &&
      state.semesters.find(semester => semester.id === cls.semesterId)?.status !== 'Archived' &&
      (user.role === 'ADMIN' || cls.primaryLecturerId === user.id));
    if ((params.classId && !accessibleClasses.some(cls => cls.id === params.classId)) ||
        (params.teamId && !state.teams.some(team => team.id === params.teamId && accessibleClasses.some(cls => cls.id === team.classId)))) {
      return failure(403, 'CLASS_ACCESS_DENIED', 'You do not have access to this submission analytics scope.');
    }
    const classes = accessibleClasses.filter(cls => (!params.semester || cls.semesterCode.startsWith(String(params.semester).trim().toUpperCase())) &&
      (!params.year || cls.year === Number(params.year)) && (!params.classId || cls.id === params.classId));
    const now = new Date().toISOString();
    const items: SubmissionAnalyticsItem[] = classes.flatMap(cls => state.teams.filter(team => team.classId === cls.id &&
      team.status === 'Active' && (!params.teamId || team.id === params.teamId)).flatMap(team =>
      checkpointDefinitionsForClass(cls).filter(checkpoint => !params.checkpointNumber || checkpoint.number === Number(params.checkpointNumber))
        .map(checkpoint => {
          const times = [...filesForCheckpoint(team.id, checkpoint.number).map(file => file.uploadedAt),
            ...linksForCheckpoint(team.id, checkpoint.number).map(link => link.submittedAt)].sort((a, b) => Date.parse(a) - Date.parse(b));
          const deadlineUtc = scheduleFor(cls.id, checkpoint.id)?.endDateUtc || null;
          return {
            teamId: team.id, teamName: team.teamName, classId: cls.id, classCode: cls.classCode,
            courseCode: cls.subjectCode, semesterCode: cls.semesterCode, hasWorkspace: Boolean(team.projectName),
            checkpointId: checkpoint.id, checkpointNumber: checkpoint.number, checkpointTitle: checkpoint.title,
            courseWeight: checkpoint.courseWeight, deadlineUtc, submittedAtUtc: times[0] || null,
            status: times.length ? 'Submitted' : deadlineUtc && Date.parse(now) > Date.parse(deadlineUtc) ? 'Missing' : 'NotSubmitted',
          };
        })));
    const submittedCount = items.filter(item => item.status === 'Submitted').length;
    return ok({ serverTimeUtc: now, expectedCount: items.length, submittedCount,
      notSubmittedCount: items.length - submittedCount, missingCount: items.filter(item => item.status === 'Missing').length, items });
  });

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
          const links = [...linksForCheckpoint(team.id, checkpoint.number)]
            .sort((left, right) => Date.parse(right.submittedAt) - Date.parse(left.submittedAt));
          const checkpointSchedule = scheduleFor(cls.id, checkpoint.id);
          const status = scheduleStatus(checkpointSchedule);
          const latestActivity = [...files.map(file => file.uploadedAt), ...links.map(link => link.submittedAt)]
            .sort((left, right) => Date.parse(right) - Date.parse(left))[0] || null;
          return {
            classId: cls.id,
            classCode: cls.classCode,
            teamId: team.id,
            teamName: team.teamName,
            checkpointId: checkpoint.id,
            checkpointNumber: checkpoint.number,
            checkpointTitle: checkpoint.title,
            status: status === 'Upcoming' ? 'Upcoming' : files.length > 0 || links.length > 0 ? 'Submitted' : status === 'Open' ? 'Pending' : status,
            latestSubmissionAtUtc: latestActivity,
            earliestSubmittedFile: files[0] ? {
              id: files[0]._id,
              originalName: files[0].originalName,
              uploadedAtUtc: files[0].uploadedAt,
            } : null,
            submittedFiles: files.map(file => ({
              id: file._id,
              originalName: file.originalName,
              uploadedAtUtc: file.uploadedAt,
            })),
            submittedLinks: links.map(link => ({
              id: link._id,
              name: link.name,
              url: link.url,
              versionNumber: link.versionNumber,
              submittedAtUtc: link.submittedAt,
            })),
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

  mock.onGet('/workspace/active-semester').reply(() => {
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    if (!user) return failure(401, 'COMMON_UNAUTHORIZED', 'Unauthorized access.');
    if (!['ADMIN', 'LECTURER', 'MENTOR'].includes(user.role))
      return failure(403, 'COMMON_FORBIDDEN', 'Forbidden access.');
    const currentSemester = state.semesters.find((item) => item.status === 'Active') || null;
    return ok({ currentSemester, availableYears: [], isDecember: false }, 'Current semester retrieved successfully.');
  });

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
    const teamName = String(body.teamName ?? team.teamName).trim();
    const projectName = String(body.projectName || '').trim();
    const description = String(body.description || '').trim();
    const zaloGroupUrl = String(body.zaloGroupUrl || '').trim();
    const startupIndustryIds = Array.isArray(body.startupIndustryIds) ? body.startupIndustryIds.map(String) : [];
    let isValidZaloGroupUrl = false;
    if (zaloGroupUrl.length > 0 && zaloGroupUrl.length <= 500) {
      try {
        const url = new URL(zaloGroupUrl);
        isValidZaloGroupUrl = url.protocol === 'https:' && (url.hostname === 'zalo.me' || url.hostname.endsWith('.zalo.me'));
      } catch {
        isValidZaloGroupUrl = false;
      }
    }
    if (teamName.length < 3 || teamName.length > 100 || projectName.length < 3 || description.length < 20 || !isValidZaloGroupUrl) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Required project workspace information is missing or invalid.');
    }
    if (state.teams.some((item) => item.classId === team.classId && item.id !== team.id &&
      item.teamName.toLowerCase() === teamName.toLowerCase())) {
      return failure(409, 'TEAM_NAME_DUPLICATED', 'A team with this name already exists in the class.');
    }
    const activeIndustries = state.startupIndustries.filter((industry) => (
      industry.status === 'active' && startupIndustryIds.includes(industry.id)
    ));
    if (startupIndustryIds.length < 1 || startupIndustryIds.length > 3
      || new Set(startupIndustryIds).size !== startupIndustryIds.length
      || activeIndustries.length !== startupIndustryIds.length) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Select between 1 and 3 active startup industries.');
    }
    const teamNameChanged = team.teamName !== teamName;
    team.teamName = teamName;
    team.projectName = projectName;
    team.projectDescription = description;
    team.projectZaloGroupUrl = zaloGroupUrl;
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
      changedFields: teamNameChanged ? ['teamName', 'projectName', 'description', 'zaloGroupUrl', 'startupIndustries'] : ['projectName', 'description', 'zaloGroupUrl', 'startupIndustries'],
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
      problem: '', solution: '', targetUsers: '', zaloGroupUrl, keywords: [], startupIndustries: team.startupIndustries,
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
    const direction = state.directions.find((item) => item.teamId === teamId);
    if (direction?.status !== 'Approved') {
      return failure(400, 'PROJECT_DIRECTION_STATE_INVALID', 'The project profile can only be updated after its project direction is approved.');
    }

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
    const proposedFields = [
      team.projectName !== projectName && 'projectName',
      (team.projectDescription || '') !== description && 'description',
    ].filter(Boolean) as string[];
    const immediateFields = [
      (team.projectProblem || '') !== problem && 'problem',
      (team.projectSolution || '') !== solution && 'solution',
      (team.projectTargetUsers || '') !== targetUsers && 'targetUsers',
      (team.projectZaloGroupUrl || '') !== zaloGroupUrl && 'zaloGroupUrl',
      JSON.stringify(team.keywords || []) !== JSON.stringify(keywords) && 'keywords',
    ].filter(Boolean) as string[];

    team.projectProblem = problem;
    team.projectSolution = solution;
    team.projectTargetUsers = targetUsers;
    team.projectZaloGroupUrl = zaloGroupUrl;
    team.keywords = keywords;
    if (immediateFields.length > 0) {
      const occurredAtUtc = new Date().toISOString();
      team.projectUpdatedAtUtc = occurredAtUtc;
      team.projectActivities = [{
        id: uuid(1701 + (team.projectActivities?.length || 0)),
        action: 'PROJECT_PROFILE_UPDATED',
        summary: 'Updated ' + immediateFields.join(', ') + '.',
        actorUserId: currentUser.id,
        actorName: currentUser.name,
        changedFields: immediateFields,
        occurredAtUtc,
      }, ...(team.projectActivities || [])];
    }
    if (proposedFields.length > 0) {
      const occurredAtUtc = new Date().toISOString();
      direction.title = projectName;
      direction.summary = description;
      direction.status = 'Submitted';
      direction.submittedAtUtc = occurredAtUtc;
      direction.reviewedAtUtc = null;
      direction.isProjectProfileChangeProposal = true;
      direction.currentTitle = team.projectName;
      direction.currentSummary = team.projectDescription || '';
      direction.rowVersion = allocateRowVersion();
      team.projectActivities = [{
        id: uuid(1801 + (team.projectActivities?.length || 0)),
        action: 'PROJECT_PROFILE_CHANGE_PROPOSED',
        summary: 'Submitted proposed Project Profile changes for lecturer review.',
        actorUserId: currentUser.id,
        actorName: currentUser.name,
        changedFields: proposedFields,
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

  mock.onPost(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/uploads$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/uploads$/);
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
    const body = parseBody(config) as { fileName?: string; contentType?: string; size?: number };
    const fileName = String(body.fileName ?? '').trim();
    const size = Number(body.size);
    const extension = fileName.split('.').at(-1)?.toLowerCase() || '';
    if (!fileName || !['pdf', 'docx', 'pptx'].includes(extension))
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Only PDF, DOCX, and PPTX files are accepted.');
    if (!Number.isFinite(size) || size <= 0)
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'The file is empty.');
    if (size > MOCK_MAX_UPLOAD_BYTES)
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Files must not exceed 100 MB.');
    const uploadId = allocateId();
    const now = Date.now();
    mockUploadSessions.set(uploadId, { teamId, number, fileName, extension, size, userId: user.id, putDone: false });
    return ok({
      uploadId,
      uploadUrl: `${MOCK_STORAGE_PREFIX}${uploadId}`,
      method: 'PUT',
      headers: { 'Content-Type': body.contentType || 'application/octet-stream' },
      urlExpiresAt: new Date(now + 10 * 60_000).toISOString(),
      sessionExpiresAt: new Date(now + 60 * 60_000).toISOString(),
      maxFileSize: MOCK_MAX_UPLOAD_BYTES,
    }, 'Upload session created.');
  });

  mock.onPost(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/uploads\/[^/]+\/complete$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/uploads\/([^/]+)\/complete$/);
    const teamId = match?.[1] || '';
    const number = Number(match?.[2]);
    const session = mockUploadSessions.get(match?.[3] || '');
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    if (user?.role !== 'STUDENT' || !canAccessCheckpointTeam(teamId))
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only active team students can upload documents.');
    if (!session || session.userId !== user.id || session.teamId !== teamId || session.number !== number)
      return failure(404, 'COMMON_NOT_FOUND', 'Upload session was not found.');
    // Retrying complete returns the file created by the first call.
    if (session.file) return ok(session.file, 'File uploaded.');
    if (!session.putDone)
      return failure(409, 'WORKSPACE_UPLOAD_OBJECT_MISSING', 'The file has not finished uploading. Please retry.');
    const key = `${teamId}:${number}`;
    const existingFiles = filesForCheckpoint(teamId, number);
    const existingLinks = linksForCheckpoint(teamId, number);
    const uploaded = {
      _id: allocateId(),
      versionNumber: Math.max(0, ...existingFiles.map((item) => item.versionNumber ?? 0), ...existingLinks.map((item) => item.versionNumber ?? 0)) + 1,
      originalName: session.fileName,
      fileType: session.extension,
      fileSize: session.size,
      canDirectDownload: session.size > MOCK_DIRECT_DOWNLOAD_THRESHOLD_BYTES,
      uploadedAt: new Date().toISOString(),
      uploadedBy: { _id: user.id, name: user.name },
    };
    state.checkpointFiles[key] ??= existingFiles;
    state.checkpointFiles[key].push(uploaded);
    session.file = uploaded;
    persistMockState();
    return ok(uploaded, 'File uploaded.');
  });

  mock.onGet(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/files\/[^/]+\/download-url$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/files\/([^/]+)\/download-url$/);
    const teamId = match?.[1] || '';
    const number = Number(match?.[2]);
    const fileId = match?.[3] || '';
    if (!canAccessCheckpointTeam(teamId)) return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this team workspace.');
    const file = filesForCheckpoint(teamId, number).find((item) => item._id === fileId);
    if (!file) return failure(404, 'COMMON_NOT_FOUND', 'Submitted file was not found.');
    if (!file.canDirectDownload) return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Direct download is not available for this file.');
    return ok({
      url: `data:text/plain;charset=utf-8,${encodeURIComponent(`Mock submitted file: ${file.originalName}`)}`,
      expiresAt: new Date(Date.now() + 5 * 60_000).toISOString(),
    }, 'Download link created.');
  });

  mock.onPost(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/links$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/links$/);
    const teamId = match?.[1] || '';
    const number = Number(match?.[2]);
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    const cls = classByTeam(teamId);
    if (user?.role !== 'STUDENT' || !canAccessCheckpointTeam(teamId) || !cls)
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only active team students can submit links.');
    const checkpoint = checkpointDefinitionsForClass(cls).find((item) => item.number === number);
    if (!checkpoint) return failure(404, 'WORKSPACE_NOT_FOUND', 'The checkpoint workspace was not found.');
    if (scheduleStatus(scheduleFor(cls.id, checkpoint.id)) !== 'Open')
      return failure(400, 'WORKSPACE_CHECKPOINT_NOT_OPEN', 'This checkpoint is not open for submissions.');
    const body = parseBody(config);
    const name = String(body.name || '').trim();
    const rawUrl = String(body.url || '');
    const urlError = validateCheckpointLinkUrl(rawUrl);
    if (!name || name.length > 100 || urlError)
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', urlError || 'Link name is required and must not exceed 100 characters.');
    const url = normalizeCheckpointLinkUrl(rawUrl);
    const existingLinks = linksForCheckpoint(teamId, number);
    if (existingLinks.length >= 10)
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'A checkpoint can contain at most 10 active links.');
    if (existingLinks.some(item => item.url.toLowerCase() === url.toLowerCase()))
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'This URL has already been submitted for the checkpoint.');
    const existingFiles = filesForCheckpoint(teamId, number);
    const submitted: MockCheckpointLink = {
      _id: allocateId(),
      versionNumber: Math.max(0, ...existingFiles.map(item => item.versionNumber ?? 0), ...existingLinks.map(item => item.versionNumber ?? 0)) + 1,
      name,
      url,
      submittedAt: new Date().toISOString(),
      submittedBy: { _id: user.id, name: user.name },
    };
    state.checkpointLinks[`${teamId}:${number}`] ??= existingLinks;
    state.checkpointLinks[`${teamId}:${number}`].push(submitted);
    persistMockState();
    return ok(submitted, 'Link submitted.');
  });

  mock.onPut(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/links\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/links\/([^/]+)$/);
    const teamId = match?.[1] || '';
    const number = Number(match?.[2]);
    const linkId = match?.[3] || '';
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    const cls = classByTeam(teamId);
    const checkpoint = cls && checkpointDefinitionsForClass(cls).find((item) => item.number === number);
    if (user?.role !== 'STUDENT' || !canAccessCheckpointTeam(teamId) || !cls || !checkpoint)
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only active team students can update submitted links.');
    if (scheduleStatus(scheduleFor(cls.id, checkpoint.id)) !== 'Open')
      return failure(400, 'WORKSPACE_CHECKPOINT_NOT_OPEN', 'This checkpoint is not open for submissions.');
    const links = linksForCheckpoint(teamId, number);
    const link = links.find(item => item._id === linkId);
    if (!link) return failure(404, 'COMMON_NOT_FOUND', 'Submitted link was not found.');
    if (link.submittedBy._id !== user.id)
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You can only update links you submitted.');
    const body = parseBody(config);
    const name = String(body.name || '').trim();
    const rawUrl = String(body.url || '');
    const urlError = validateCheckpointLinkUrl(rawUrl);
    if (!name || name.length > 100 || urlError)
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', urlError || 'Link name is required and must not exceed 100 characters.');
    const url = normalizeCheckpointLinkUrl(rawUrl);
    if (links.some(item => item._id !== linkId && item.url.toLowerCase() === url.toLowerCase()))
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'This URL has already been submitted for the checkpoint.');
    link.name = name;
    link.url = url;
    persistMockState();
    return ok(link, 'Submitted link updated.');
  });

  mock.onDelete(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/links\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/links\/([^/]+)$/);
    const teamId = match?.[1] || '';
    const number = Number(match?.[2]);
    const linkId = match?.[3] || '';
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    const cls = classByTeam(teamId);
    const checkpoint = cls && checkpointDefinitionsForClass(cls).find((item) => item.number === number);
    if (user?.role !== 'STUDENT' || !canAccessCheckpointTeam(teamId) || !cls || !checkpoint)
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only active team students can delete submitted links.');
    if (scheduleStatus(scheduleFor(cls.id, checkpoint.id)) !== 'Open')
      return failure(400, 'WORKSPACE_CHECKPOINT_NOT_OPEN', 'This checkpoint is not open for submissions.');
    const key = `${teamId}:${number}`;
    const links = linksForCheckpoint(teamId, number);
    const link = links.find(item => item._id === linkId);
    if (!link) return failure(404, 'COMMON_NOT_FOUND', 'Submitted link was not found.');
    if (link.submittedBy._id !== user.id)
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You can only delete links you submitted.');
    state.checkpointLinks[key] = links.filter(item => item._id !== linkId);
    persistMockState();
    return ok({}, 'Submitted link deleted.');
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

  mock.onGet(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/files\/[^/]+\/preview$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/files\/([^/]+)\/preview$/);
    const teamId = match?.[1] || '';
    const number = Number(match?.[2]);
    const fileId = match?.[3] || '';
    if (!canAccessCheckpointTeam(teamId)) return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this team workspace.');
    const file = filesForCheckpoint(teamId, number).find((item) => item._id === fileId);
    if (!file) return failure(404, 'COMMON_NOT_FOUND', 'Submitted file was not found.');
    const extension = file.originalName.split('.').at(-1)?.toLowerCase();
    if (!['pdf', 'docx', 'pptx'].includes(extension || ''))
      return failure(415, 'WORKSPACE_FILE_PREVIEW_UNSUPPORTED', 'This file format cannot be previewed.');
    return [200, new Blob(['%PDF-1.4\n%%EOF'], { type: 'application/pdf' }), { 'content-type': 'application/pdf' }];
  });

  mock.onDelete(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/files\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/files\/([^/]+)$/);
    const teamId = match?.[1] || '';
    const number = Number(match?.[2]);
    const fileId = match?.[3] || '';
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    const cls = classByTeam(teamId);
    const checkpoint = cls && checkpointDefinitionsForClass(cls).find((item) => item.number === number);
    if (user?.role !== 'STUDENT' || !canAccessCheckpointTeam(teamId) || !cls || !checkpoint)
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only active team students can delete submitted files.');
    if (scheduleStatus(scheduleFor(cls.id, checkpoint.id)) !== 'Open')
      return failure(400, 'WORKSPACE_CHECKPOINT_NOT_OPEN', 'This checkpoint is not open for submissions.');
    const key = `${teamId}:${number}`;
    const files = filesForCheckpoint(teamId, number);
    const file = files.find(item => item._id === fileId);
    if (!file) return failure(404, 'COMMON_NOT_FOUND', 'Submitted file was not found.');
    if (file.uploadedBy._id !== user.id)
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You can only delete files you uploaded.');
    state.checkpointFiles[key] = files.filter(item => item._id !== fileId);
    persistMockState();
    return ok({}, 'File deleted.');
  });

  mock.onGet(/^\/workspace\/checkpoints\/teams\/[^/]+\/checkpoints\/\d+\/evaluation-summary$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/checkpoints\/(\d+)\/evaluation-summary$/);
    const teamId = match?.[1] || '';
    const checkpointNumber = Number(match?.[2] || 1);
    return canAccessCheckpointTeam(teamId) && checkpointDefinitionsForClass(classByTeam(teamId)!).some((item) => item.number === checkpointNumber)
      ? ok(evaluationSummary(teamId, checkpointNumber), 'Evaluation summary retrieved successfully.')
      : failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
  });

  mock.onPost('/workspace/checkpoints/evaluation-grading/export').reply((config) => {
    const state = getMockState();
    const currentUser = state.users.find(user => user.id === state.sessionUserId);
    if (!currentUser) return failure(401, 'UNAUTHORIZED', 'Authentication is required.');
    if (currentUser.role !== 'ADMIN' && currentUser.role !== 'LECTURER') {
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Staff access is required.');
    }

    const body = parseBody(config);
    const scopes = Array.isArray(body.teams) ? body.teams as Array<{
      teamId?: unknown;
      checkpointNumbers?: unknown;
    }> : [];
    if (scopes.length === 0 || scopes.length > 500 || scopes.some(scope =>
      !scope.teamId || !Array.isArray(scope.checkpointNumbers) ||
      scope.checkpointNumbers.length === 0 ||
      scope.checkpointNumbers.some(number => !Number.isInteger(Number(number)) || Number(number) <= 0))) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Select a valid evaluation report scope.');
    }
    if (scopes.some(scope => !classByTeam(String(scope.teamId)) || !canAccessCheckpointTeam(String(scope.teamId)))) {
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'One or more requested teams are outside your access scope.');
    }

    return [200, new Blob(['Mock evaluation report'], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    }), {
      'content-type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      'content-disposition': 'attachment; filename="evaluation_report.xlsx"',
    }];
  });

  mock.onPost('/workspace/checkpoints/evaluation-grading').reply((config) => {
    const state = getMockState();
    const body = parseBody(config);
    const requestedTeamIds = Array.isArray(body.teamIds) ? body.teamIds.map(String) : [];
    if (requestedTeamIds.length > 200 || requestedTeamIds.some(teamId => !teamId)) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Select up to 200 valid teams at once.');
    }
    const teamIds = [...new Set(requestedTeamIds)];
    if (teamIds.some(teamId => !classByTeam(teamId) || !canAccessCheckpointTeam(teamId))) {
      return failure(403, 'WORKSPACE_ACCESS_DENIED', 'One or more requested teams are unavailable or outside your access scope.');
    }
    const currentUser = state.users.find(user => user.id === state.sessionUserId);
    const isInternal = currentUser?.role === 'ADMIN' || currentUser?.role === 'LECTURER';
    const teams = teamIds.map(teamId => {
      const detail = workspaceData(teamId);
      const semesterGroupName = [...new Set(Object.values(state.rosters).flat()
        .filter(student => student.teamId === teamId && student.semesterGroupName)
        .map(student => student.semesterGroupName))].join(', ');
      return {
        teamId,
        teamCode: detail?.team.teamCode || '',
        projectName: detail?.project?.projectName || detail?.proposal?.projectName || null,
        projectDescription: detail?.project?.description || detail?.proposal?.projectDescription || null,
        semesterGroupName,
        members: (detail?.members || []).filter(member => isInternal ||
          (currentUser?.role === 'STUDENT' && (typeof member.userId === 'object' && member.userId ? member.userId._id : member.userId) === currentUser.id))
          .map(member => ({
          studentId: member.studentId,
          userId: typeof member.userId === 'object' && member.userId ? member.userId._id : member.userId || null,
          fullName: member.fullName,
          rollNumber: member.rollNumber,
          majorCode: member.majorCode,
          roleInTeam: member.roleInTeam,
        })),
        checkpoints: checkpointDefinitionsForClass(classByTeam(teamId)!).map(checkpoint => {
          const summary = evaluationSummary(teamId, checkpoint.number);
          return { checkpoint: summary.checkpoint, evaluations: summary.evaluations };
        }),
        assessments: courseAssessmentData(teamId).assessments,
      };
    });
    return ok({ teams }, 'Evaluation grading data retrieved.');
  });

  mock.onGet(/^\/workspace\/checkpoints\/teams\/[^/]+\/course-assessments$/).reply((config) => {
    const teamId = routeId(config, /^\/workspace\/checkpoints\/teams\/([^/]+)\/course-assessments$/);
    const cls = classByTeam(teamId);
    if (!cls || !canAccessCheckpointTeam(teamId)) return failure(403, 'WORKSPACE_ACCESS_DENIED', 'You do not have access to this team.');
    return ok(courseAssessmentData(teamId), 'Course assessments retrieved.');
  });

  mock.onPut(/^\/workspace\/checkpoints\/teams\/[^/]+\/course-assessments\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/workspace\/checkpoints\/teams\/([^/]+)\/course-assessments\/([^/]+)$/);
    const teamId = match?.[1] || '';
    const assessmentId = match?.[2] || '';
    const state = getMockState();
    const user = state.users.find(item => item.id === state.sessionUserId);
    const cls = classByTeam(teamId);
    if (!cls || user?.role !== 'LECTURER' || cls.primaryLecturerId !== user.id) return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only an assigned lecturer can grade this assessment.');
    const assessment = state.curricula[cls.subjectCode]?.otherAssessments.find(item => item._id === assessmentId);
    if (!assessment) return failure(404, 'COMMON_NOT_FOUND', 'The course assessment was not found.');
    const body = parseBody(config);
    const score = Number(body.score);
    if (!Number.isFinite(score) || score < 0 || score > 10) return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'The assessment score must be between 0 and 10.');
    const team = teamById(teamId)!;
    const memberScores = Array.isArray(body.memberScores) ? body.memberScores as Array<{ studentId?: unknown; score?: unknown }> : [];
    const studentIds = memberScores.map(item => String(item.studentId || ''));
    if (new Set(studentIds).size !== studentIds.length) return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'A team member can only have one individual assessment score.');
    if (memberScores.some(item => !team.members.some(member => member.studentId === item.studentId))) return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'One or more individual scores belong to a student outside this team.');
    if (memberScores.some(item => !Number.isFinite(Number(item.score)) || Number(item.score) < 0 || Number(item.score) > 10)) return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Each individual member score must be between 0 and 10.');
    const evaluationId = `${teamId}--${assessmentId}`;
    state.courseAssessmentScores[`${teamId}:${assessmentId}`] = score;
    Object.keys(state.courseAssessmentMemberScores)
      .filter(key => key.startsWith(`${teamId}:${assessmentId}:`))
      .forEach(key => delete state.courseAssessmentMemberScores[key]);
    memberScores.forEach(item => {
      const memberScore = Number(item.score);
      if (memberScore !== score) state.courseAssessmentMemberScores[`${teamId}:${assessmentId}:${String(item.studentId)}`] = memberScore;
    });
    state.evaluationPublicationStatuses[evaluationId] = 'SUBMITTED';
    persistMockState();
    return ok({ assessmentId, name: assessment.name, weight: assessment.weight, evaluationId, evaluatorId: user.id, score, memberScores: team.members.map(member => { const key = `${teamId}:${assessmentId}:${member.studentId}`; const hasOverride = Object.prototype.hasOwnProperty.call(state.courseAssessmentMemberScores, key); return { studentId: member.studentId, score: hasOverride ? state.courseAssessmentMemberScores[key] : score, isOverridden: hasOverride }; }), status: 'SUBMITTED', updatedAt: new Date().toISOString() }, 'Course assessment score saved.');
  });

  mock.onPut(/^\/workspace\/checkpoints\/evaluations\/[^/]+\/publish$/).reply((config) => {
    const evaluationId = routeId(config, /^\/workspace\/checkpoints\/evaluations\/([^/]+)\/publish$/);
    const state = getMockState();
    const user = state.users.find(item => item.id === state.sessionUserId);
    if (user?.role !== 'LECTURER') return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only the assigned lecturer can publish this evaluation.');
    state.evaluationPublicationStatuses[evaluationId] = 'PUBLISHED';
    persistMockState();
    return ok({ _id: evaluationId, status: 'PUBLISHED', publishedAt: new Date().toISOString() }, 'Evaluation published.');
  });

  mock.onPut(/^\/workspace\/checkpoints\/evaluations\/[^/]+\/unpublish$/).reply((config) => {
    const evaluationId = routeId(config, /^\/workspace\/checkpoints\/evaluations\/([^/]+)\/unpublish$/);
    const state = getMockState();
    const user = state.users.find(item => item.id === state.sessionUserId);
    if (user?.role !== 'LECTURER') return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only the assigned lecturer can hide published scores.');
    if (state.evaluationPublicationStatuses[evaluationId] !== 'PUBLISHED') return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Only a published evaluation can be hidden.');
    state.evaluationPublicationStatuses[evaluationId] = 'SUBMITTED';
    persistMockState();
    return ok({ _id: evaluationId, status: 'SUBMITTED', updatedAt: new Date().toISOString() }, 'Published scores hidden.');
  });

  mock.onPut('/workspace/checkpoints/evaluations/publication/bulk').reply((config) => {
    const state = getMockState();
    const user = state.users.find(item => item.id === state.sessionUserId);
    if (user?.role !== 'LECTURER') return failure(403, 'WORKSPACE_ACCESS_DENIED', 'Only an assigned lecturer can update evaluation publication.');
    const body = parseBody(config);
    const action = String(body.action || '').toUpperCase();
    const evaluationIds = [...new Set(Array.isArray(body.evaluationIds) ? body.evaluationIds.map(String) : [])];
    if (!['PUBLISH', 'UNPUBLISH'].includes(action) || evaluationIds.length === 0 || evaluationIds.length > 200) {
      return failure(400, 'WORKSPACE_VALIDATION_ERROR', 'Select between 1 and 200 evaluations and a valid publication action.');
    }
    const targetStatus = action === 'PUBLISH' ? 'PUBLISHED' : 'SUBMITTED';
    let changedCount = 0;
    evaluationIds.forEach(evaluationId => {
      if (state.evaluationPublicationStatuses[evaluationId] !== targetStatus) changedCount += 1;
      state.evaluationPublicationStatuses[evaluationId] = targetStatus;
    });
    persistMockState();
    return ok({
      action,
      targetStatus,
      requestedCount: evaluationIds.length,
      changedCount,
      unchangedCount: evaluationIds.length - changedCount,
    }, 'Evaluation publication statuses updated.');
  });
}
