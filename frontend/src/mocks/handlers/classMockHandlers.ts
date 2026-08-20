import type MockAdapter from 'axios-mock-adapter';
import type { AxiosRequestConfig } from 'axios';
import type { ClassDto, ClassStatus } from '../../types/classes.ts';
import type { MockClass, MockRosterStudent } from '../mockState.ts';
import { PROGRAM_GROUPS } from '../../constants/majors.ts';
import {
  allocateId,
  allocateRowVersion,
  asNumber,
  asString,
  classMutationGuard,
  created,
  failure,
  findClass,
  getMockState,
  ok,
  parseBody,
  persistMockState,
  refreshClassCounts,
  requestParams,
  routeId,
  touchClass,
} from '../mockHelpers.ts';

const validMajorCodes = new Set<string>(PROGRAM_GROUPS.flatMap((group) =>
  group.majors.map((major) => major.code.toUpperCase())));

function classResponse(cls: MockClass): ClassDto {
  refreshClassCounts(cls.id);
  const { previousStatus: _previousStatus, ...response } = cls;
  return {
    ...response,
    statusBeforeArchive: cls.status === 'Archived' ? cls.previousStatus : null,
  };
}

function addAudit(classId: string, action: string, details: Record<string, unknown>): void {
  const state = getMockState();
  state.audits[classId] ||= [];
  state.audits[classId].unshift({
    id: allocateId(),
    action,
    performedByUserId: state.users.find((user) => user.role === 'ADMIN')?.id || allocateId(),
    performedByName: 'Mock API Admin',
    occurredAtUtc: new Date().toISOString(),
    detailsJson: JSON.stringify(details),
  });
}

function adminOnlyGuard() {
  const state = getMockState();
  const user = state.users.find((candidate) => candidate.id === state.sessionUserId);
  return user?.role === 'ADMIN'
    ? null
    : failure(403, 'CLASS_ACCESS_DENIED', 'Only an administrator can create or assign classes.');
}

function studentClassSummary(cls: MockClass, enrollmentStatus: string) {
  return {
    id: cls.id,
    classCode: cls.classCode,
    subjectCode: cls.subjectCode,
    subjectName: cls.subjectName,
    semester: cls.semesterCode.slice(0, 2),
    year: cls.year,
    classStatus: cls.status,
    enrollmentStatus,
    lectureId: cls.primaryLecturerId
      ? { id: cls.primaryLecturerId, name: cls.primaryLecturerName, email: cls.primaryLecturerEmail }
      : null,
    mentors: cls.mentors,
  };
}

function completionPreview(cls: MockClass) {
  const state = getMockState();
  const roster = state.rosters[cls.id] || [];
  const classTeams = state.teams.filter((team) => team.classId === cls.id);
  const teamIds = new Set(classTeams.map((team) => team.id));
  const openProposals = state.proposals.filter((proposal) =>
    proposal.classId === cls.id && ['Draft', 'Pending', 'NeedsRevision'].includes(proposal.status));
  const openDirections = state.directions.filter((direction) =>
    teamIds.has(direction.teamId) && direction.status !== 'Approved');
  const activeMentors = classTeams.filter((team) => team.currentMentorAssignment?.status === 'Active');
  const warnings: string[] = [];
  if (openProposals.length) warnings.push(`${openProposals.length} open team proposal(s) will be cancelled.`);
  if (openDirections.length) warnings.push(`${openDirections.length} project direction(s) will be retained as read-only in their current state.`);
  if (activeMentors.length) warnings.push(`${activeMentors.length} active mentor assignment(s) will be ended.`);

  return {
    classId: cls.id,
    classCode: cls.classCode,
    status: cls.status,
    activeEnrollmentCount: roster.filter((student) => student.enrollmentStatus === 'Active').length,
    droppedEnrollmentCount: roster.filter((student) => student.enrollmentStatus === 'Dropped').length,
    activeMentorAssignmentCount: activeMentors.length,
    openTeamProposalCount: openProposals.length,
    openProjectDirectionCount: openDirections.length,
    processingImportSessionCount: 0,
    scheduledMentoringSessionCount: 0,
    blockers: cls.status === 'Active' || cls.status === 'Completed' ? [] : ['Only an active class can be completed.'],
    warnings,
    rowVersion: cls.rowVersion,
  };
}

function createClassFromBulk(body: Record<string, unknown>, classIndex: number): MockClass | null {
  const state = getMockState();
  const subjectCode = asString(body.subjectCode).toUpperCase();
  const subject = state.subjects.find((item) =>
    item._id === asString(body.courseId) || item.subjectCode === subjectCode);
  if (!subject) return null;
  const semester = asString(body.semester, state.currentSemester?.semester ?? 'FA').toUpperCase();
  const year = asNumber(body.year, state.currentSemester?.year ?? new Date().getFullYear());
  const semesterCode = `${semester}${year}`;
  const primaryLecturer = state.users.find((user) => user.id === asString(body.primaryLecturerId) && user.role === 'LECTURER');
  return {
    id: allocateId(),
    slug: `${semesterCode}-${subject.subjectCode}-${classIndex}`.toLowerCase(),
    classCode: `${subject.subjectCode}_${classIndex}`,
    classIndex,
    courseId: subject._id,
    subjectCode: subject.subjectCode,
    subjectName: subject.subjectName,
    semesterId: asString(body.semesterId) || allocateId(),
    semesterCode,
    year,
    primaryLecturerId: primaryLecturer?.id || null,
    primaryLecturerName: primaryLecturer?.name || null,
    primaryLecturerEmail: primaryLecturer?.email || null,
    room: asString(body.room) || null,
    schedules: [],
    isEnrollmentMajorLocked: false,
    status: 'Draft',
    previousStatus: 'Draft',
    studentCount: 0,
    teamCount: 0,
    mentors: [],
    createdAtUtc: new Date().toISOString(),
    rowVersion: allocateRowVersion(),
  };
}

function bulkIndices(body: Record<string, unknown>): number[] {
  const explicit = Array.isArray(body.classIndices)
    ? body.classIndices.map(Number).filter((value) => Number.isInteger(value) && value > 0)
    : [];
  if (explicit.length) return explicit;
  const start = Math.max(1, asNumber(body.startClassIndex, 1));
  const quantity = Math.min(100, Math.max(1, asNumber(body.quantity, 1)));
  return Array.from({ length: quantity }, (_, index) => start + index);
}

function registerClassQueries(mock: MockAdapter): void {
  mock.onGet('/classes').reply((config) => {
    const params = requestParams(config);
    const status = asString(params.status) as ClassStatus | '';
    const assignmentStatus = asString(params.assignmentStatus);
    const query = asString(params.search).trim().toLowerCase();
    const page = Math.max(1, asNumber(params.page, 1));
    const pageSize = Math.min(100, Math.max(1, asNumber(params.pageSize, 10)));
    const state = getMockState();
    const sessionUser = state.users.find((user) => user.id === state.sessionUserId);
    let classes = state.classes.filter((cls) =>
      (status ? cls.status === status : cls.status === 'Active' || cls.status === 'Draft')
      && (sessionUser?.role !== 'LECTURER' || cls.primaryLecturerId === sessionUser.id)
      && (assignmentStatus !== 'Assigned' || cls.primaryLecturerId !== null)
      && (assignmentStatus !== 'Unassigned' || cls.primaryLecturerId === null)
      && (!params.semesterCode || cls.semesterCode.toUpperCase() === asString(params.semesterCode).toUpperCase())
      && (!params.year || cls.year === asNumber(params.year, cls.year))
      && (!params.subjectCode || cls.subjectCode.toUpperCase() === asString(params.subjectCode).toUpperCase())
      && (!query || [cls.classCode, cls.subjectCode, cls.subjectName].some((value) => value.toLowerCase().includes(query))));
    const sort = asString(params.sort, 'code').toLowerCase();
    classes = [...classes].sort((left, right) => {
      if (sort.includes('createdat')) return left.createdAtUtc.localeCompare(right.createdAtUtc) * (sort.startsWith('-') ? -1 : 1);
      if (sort.includes('classindex')) return (left.classIndex - right.classIndex) * (sort.startsWith('-') ? -1 : 1);
      return left.classCode.localeCompare(right.classCode, undefined, { numeric: true }) * (sort.startsWith('-') ? -1 : 1);
    });
    const totalCount = classes.length;
    const items = classes.slice((page - 1) * pageSize, page * pageSize).map(classResponse);
    return ok({ items, totalCount, page, pageSize, totalPages: Math.max(1, Math.ceil(totalCount / pageSize)) }, 'Classes retrieved successfully.');
  });

  mock.onGet('/classes/my-classes').reply((config) => {
    const state = getMockState();
    const sessionUser = state.users.find((user) => user.id === state.sessionUserId && user.role === 'STUDENT');
    const studentId = sessionUser?.id;
    const history = asString(requestParams(config).scope, 'Current').toLowerCase() === 'history';
    const expectedEnrollment = history ? 'Completed' : 'Active';
    const classes = state.classes.flatMap((cls) => {
      const enrollment = state.rosters[cls.id]?.find((student) =>
        student.userId === studentId && student.enrollmentStatus === expectedEnrollment);
      const inExpectedClassState = history
        ? cls.status === 'Completed' || cls.status === 'Archived'
        : cls.status === 'Draft' || cls.status === 'Active';
      return enrollment && inExpectedClassState
        ? [studentClassSummary(cls, enrollment.enrollmentStatus)]
        : [];
    });
    return ok({ classes }, 'Student classes retrieved.');
  });

  mock.onGet('/classes/my-team').reply(() => {
    const state = getMockState();
    const studentId = state.users.find((user) => user.id === state.sessionUserId && user.role === 'STUDENT')?.id;
    const team = state.teams.find((item) => item.members.some((member) => member.studentId === studentId)) || null;
    const cls = team ? findClass(team.classId) : undefined;
    const classSummary = cls ? studentClassSummary(cls, 'Active') : null;
    return ok({ team, class: classSummary, members: team?.members || [] }, 'Student team retrieved.');
  });

  mock.onGet(/^\/classes\/my-class-detail\/[^/]+$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/my-class-detail\/([^/]+)$/);
    const cls = findClass(classId);
    if (!cls) return failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
    const state = getMockState();
    const sessionUserId = state.users.find((user) => user.id === state.sessionUserId && user.role === 'STUDENT')?.id;
    const currentEnrollment = (state.rosters[classId] || []).find((student) => student.userId === sessionUserId);
    const rosterStatus = currentEnrollment?.enrollmentStatus === 'Completed' ? 'Completed' : 'Active';
    const classSummary = studentClassSummary(cls, rosterStatus);
    const students = (state.rosters[classId] || []).filter((student) => student.enrollmentStatus === rosterStatus).map((student) => ({ studentId: student.studentId, userId: student.userId, rollNumber: student.rollNumber, fullName: student.fullName, email: student.email, majorCode: student.majorCode, teamId: student.teamId }));
    const teams = getMockState().teams.filter((team) => team.classId === classId);
    return ok({ class: classSummary, students, teams }, 'Student class detail retrieved.');
  });

  mock.onGet(/^\/classes\/[^/]+\/students$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/students$/);
    if (!findClass(classId)) return failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
    const params = requestParams(config);
    const query = asString(params.search).trim().toLowerCase();
    const major = asString(params.majorCode).toUpperCase();
    const status = asString(params.status);
    const page = Math.max(1, asNumber(params.page, 1));
    const pageSize = Math.min(100, Math.max(1, asNumber(params.pageSize, 20)));
    let students = (getMockState().rosters[classId] || []).filter((student) =>
      (!query || [student.rollNumber, student.fullName, student.email].some((value) => value.toLowerCase().includes(query)))
      && (!major || student.majorCode?.toUpperCase() === major)
      && (!status || student.enrollmentStatus === status));
    const totalCount = students.length;
    students = students.slice((page - 1) * pageSize, page * pageSize);
    return ok({ items: students, totalCount, page, pageSize, totalPages: Math.max(1, Math.ceil(totalCount / pageSize)) }, 'Class roster retrieved successfully.');
  });

  mock.onGet(/^\/classes\/[^/]+\/audit$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/audit$/);
    if (!findClass(classId)) return failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
    const params = requestParams(config);
    const page = Math.max(1, asNumber(params.page, 1));
    const pageSize = Math.min(100, Math.max(1, asNumber(params.pageSize, 25)));
    const entries = getMockState().audits[classId] || [];
    return ok({ items: entries.slice((page - 1) * pageSize, page * pageSize), totalCount: entries.length, page, pageSize, totalPages: Math.max(1, Math.ceil(entries.length / pageSize)) }, 'Class audit trail retrieved successfully.');
  });

  mock.onGet(/^\/classes\/[^/]+\/completion-preview$/).reply((config) => {
    const cls = findClass(routeId(config, /^\/classes\/([^/]+)\/completion-preview$/));
    return cls
      ? ok(completionPreview(cls), 'Class completion preview generated successfully.')
      : failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
  });

  mock.onGet(/^\/classes\/[^/]+$/).reply((config) => {
    const cls = findClass(routeId(config, /^\/classes\/([^/]+)$/));
    return cls ? ok(classResponse(cls), 'Class retrieved successfully.') : failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
  });
}

function registerClassCrud(mock: MockAdapter): void {
  mock.onPost('/classes').reply((config) => {
    const permissionError = adminOnlyGuard();
    if (permissionError) return permissionError;
    const body = parseBody(config);
    const cls = createClassFromBulk(body, Math.max(1, asNumber(body.classIndex, 1)));
    if (!cls) return failure(400, 'CLASS_COURSE_NOT_FOUND', 'The selected subject does not exist.');
    getMockState().classes.push(cls);
    getMockState().rosters[cls.id] = [];
    addAudit(cls.id, 'CLASS_CREATED', { status: cls.status });
    persistMockState();
    return created(classResponse(cls), 'Class created successfully.');
  });

  mock.onPost('/classes/bulk/preview').reply((config) => {
    const permissionError = adminOnlyGuard();
    if (permissionError) return permissionError;
    const body = parseBody(config);
    const items = bulkIndices(body).map((index) => {
      const candidate = createClassFromBulk(body, index);
      if (!candidate) return { classCode: '-', classIndex: index, subjectCode: asString(body.subjectCode), semesterCode: '-', primaryLecturerName: null, isValid: false, errorMessage: 'Subject not found.' };
      const duplicate = getMockState().classes.some((cls) => cls.courseId === candidate.courseId && cls.semesterCode === candidate.semesterCode && cls.classIndex === index);
      return { classCode: candidate.classCode, classIndex: index, subjectCode: candidate.subjectCode, semesterCode: candidate.semesterCode, primaryLecturerName: candidate.primaryLecturerName, isValid: !duplicate, errorMessage: duplicate ? 'Class index already exists for this subject and semester.' : null };
    });
    return ok({ items, totalCount: items.length, validCount: items.filter((item) => item.isValid).length, invalidCount: items.filter((item) => !item.isValid).length }, 'Bulk class preview generated.');
  });

  mock.onPost('/classes/bulk/commit').reply((config) => {
    const permissionError = adminOnlyGuard();
    if (permissionError) return permissionError;
    const body = parseBody(config);
    const createdClasses: ClassDto[] = [];
    for (const index of bulkIndices(body)) {
      const cls = createClassFromBulk(body, index);
      if (!cls || getMockState().classes.some((item) => item.courseId === cls.courseId && item.semesterCode === cls.semesterCode && item.classIndex === index)) continue;
      getMockState().classes.push(cls);
      getMockState().rosters[cls.id] = [];
      addAudit(cls.id, 'CLASS_CREATED', { bulk: true, status: cls.status });
      createdClasses.push(classResponse(cls));
    }
    persistMockState();
    return ok(createdClasses, `${createdClasses.length} classes created successfully.`);
  });

  mock.onPut(/^\/classes\/[^/]+$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)$/);
    const body = parseBody(config);
    const guard = classMutationGuard(classId, body.rowVersion);
    if (guard) return guard;
    const cls = findClass(classId)!;
    if ('room' in body) cls.room = asString(body.room) || null;
    touchClass(cls);
    persistMockState();
    return ok(classResponse(cls), 'Class updated successfully.');
  });

  mock.onPut(/^\/classes\/[^/]+\/rename$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/rename$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    const cls = findClass(classId)!;
    const classCode = asString(parseBody(config).classCode).trim().toUpperCase();
    if (classCode.length < 3) return failure(400, 'CLASS_VALIDATION_ERROR', 'Class code must contain at least three characters.');
    cls.classCode = classCode;
    touchClass(cls);
    addAudit(classId, 'CLASS_RENAMED', { classCode });
    persistMockState();
    return ok(classResponse(cls), 'Class renamed successfully.');
  });

  mock.onPut(/^\/classes\/[^/]+\/schedule$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/schedule$/);
    const body = parseBody(config);
    const guard = classMutationGuard(classId, body.rowVersion);
    if (guard) return guard;
    const cls = findClass(classId)!;
    const schedules = body.schedules;
    if (!Array.isArray(schedules)) return failure(400, 'CLASS_SCHEDULE_REQUIRED', 'Schedules must be provided.');
    const normalized = schedules.map((slot) => slot as Record<string, unknown>).map((slot) => ({ dayOfWeek: asNumber(slot.dayOfWeek, 1), slotNumber: asNumber(slot.slotNumber, 1), room: asString(slot.room) || null }));
    const keys = normalized.map((slot) => `${slot.dayOfWeek}:${slot.slotNumber}`);
    if (new Set(keys).size !== keys.length) return failure(409, 'CLASS_SCHEDULE_CONFLICT', 'A class cannot contain duplicate schedule slots.');
    cls.schedules = normalized;
    touchClass(cls);
    addAudit(classId, 'CLASS_SCHEDULE_UPDATED', { schedules: normalized });
    persistMockState();
    return ok(classResponse(cls), 'Class schedule updated successfully.');
  });

  mock.onPut(/^\/classes\/[^/]+\/teaching-assignment$/).reply((config) => {
    const permissionError = adminOnlyGuard();
    if (permissionError) return permissionError;
    const classId = routeId(config, /^\/classes\/([^/]+)\/teaching-assignment$/);
    const body = parseBody(config);
    const guard = classMutationGuard(classId, body.rowVersion);
    if (guard) return guard;
    const cls = findClass(classId)!;
    const lecturerId = asString(body.primaryLecturerId);
    if (!lecturerId && cls.status === 'Active') return failure(409, 'CLASS_ACTIVE_LECTURER_REQUIRED', 'An active class cannot be unassigned.');
    const lecturer = getMockState().users.find((user) => user.id === lecturerId && user.role === 'LECTURER' && user.status === 'APPROVED');
    if (lecturerId && !lecturer) return failure(400, 'CLASS_LECTURER_INVALID', 'The selected lecturer is unavailable.');
    cls.primaryLecturerId = lecturer?.id || null;
    cls.primaryLecturerName = lecturer?.name || null;
    cls.primaryLecturerEmail = lecturer?.email || null;
    touchClass(cls);
    addAudit(classId, 'CLASS_LECTURER_REASSIGNED', { primaryLecturerId: lecturer?.id || null });
    persistMockState();
    return ok(classResponse(cls), 'Teaching assignment updated successfully.');
  });

  mock.onPost(/^\/classes\/[^/]+\/archive$/).reply((config) => changeLifecycle(config, 'Archived'));
  mock.onPost(/^\/classes\/[^/]+\/restore$/).reply((config) => changeLifecycle(config, 'Restore'));
  mock.onPost(/^\/classes\/[^/]+\/complete$/).reply((config) => changeCompletion(config, false));
  mock.onPost(/^\/classes\/[^/]+\/reopen$/).reply((config) => changeCompletion(config, true));

  mock.onPost(/^\/classes\/[^/]+\/repair-chat-memberships$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/repair-chat-memberships$/);
    const cls = findClass(classId);
    if (!cls) return failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
    const repairedBefore = (getMockState().audits[classId] || []).some((entry) => entry.action === 'CHAT_MEMBERSHIPS_REPAIRED');
    const result = { classId, groupsCreated: repairedBefore ? 0 : Math.max(1, cls.teamCount), membershipsAdded: repairedBefore ? 0 : cls.studentCount + cls.teamCount, membershipsReactivated: 0, membershipsEnded: 0, isReadOnly: cls.status === 'Completed' || cls.status === 'Archived' };
    if (!repairedBefore) addAudit(classId, 'CHAT_MEMBERSHIPS_REPAIRED', result);
    persistMockState();
    return ok(result, 'Class chat memberships repaired successfully.');
  });
}

function changeCompletion(config: AxiosRequestConfig, reopen: boolean) {
  const classId = routeId(config, /^\/classes\/([^/]+)\/(complete|reopen)$/);
  const cls = findClass(classId);
  if (!cls) return failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
  const body = parseBody(config);
  const reason = asString(body.reason).trim();
  if (reason.length < 3 || reason.length > 500)
    return failure(400, 'CLASS_VALIDATION_ERROR', 'Reason must contain between 3 and 500 characters.');

  if (reopen && cls.status === 'Active' || !reopen && cls.status === 'Completed') {
    return ok({ classId, status: cls.status, completedAtUtc: cls.completedAtUtc ?? null, archivedAtUtc: null, rowVersion: cls.rowVersion });
  }
  if (asString(body.rowVersion) !== cls.rowVersion)
    return failure(409, 'CLASS_CONCURRENCY_CONFLICT', 'The class was changed by another request. Refresh and try again.');

  const state = getMockState();
  const now = new Date().toISOString();
  if (reopen) {
    if (cls.status !== 'Completed')
      return failure(409, 'CLASS_COMPLETION_BLOCKED', 'Only a completed class can be reopened.');
    const semester = state.semesters.find((item) => item.id === cls.semesterId);
    const subject = state.subjects.find((item) => item._id === cls.courseId);
    if (semester?.status !== 'Active')
      return failure(409, 'CLASS_COMPLETION_BLOCKED', 'The semester must be active before this class can be reopened.');
    if (subject?.status !== 'active')
      return failure(409, 'CLASS_COMPLETION_BLOCKED', 'The subject must be active before this class can be reopened.');
    if (!cls.primaryLecturerId || cls.schedules.length === 0)
      return failure(409, 'CLASS_COMPLETION_BLOCKED', 'A reopened class requires an active lecturer and schedule.');

    (state.rosters[classId] || []).forEach((student) => {
      if (student.enrollmentStatus === 'Completed') student.enrollmentStatus = 'Active';
    });
    cls.status = 'Active';
    cls.completedAtUtc = null;
    cls.completionReason = null;
    cls.rowVersion = allocateRowVersion();
    addAudit(classId, 'CLASS_REOPENED', { reason });
  } else {
    if (cls.status !== 'Active')
      return failure(409, 'CLASS_COMPLETION_BLOCKED', 'Only an active class can be completed.');
    const preview = completionPreview(cls);
    if (preview.blockers.length)
      return failure(409, 'CLASS_COMPLETION_BLOCKED', preview.blockers.join(' '));

    (state.rosters[classId] || []).forEach((student) => {
      if (student.enrollmentStatus === 'Active') student.enrollmentStatus = 'Completed';
    });
    state.proposals.filter((proposal) =>
      proposal.classId === classId && ['Draft', 'Pending', 'NeedsRevision'].includes(proposal.status))
      .forEach((proposal) => {
        const previousStatus = proposal.status;
        proposal.status = 'Cancelled';
        proposal.history.unshift({
          id: allocateId(), fromStatus: previousStatus, toStatus: 'Cancelled',
          action: 'CancelledByClassCompletion', comment: reason,
          performedByUserId: state.sessionUserId || state.users[0].id, occurredAtUtc: now,
        });
      });
    state.teams.filter((team) => team.classId === classId).forEach((team) => {
      if (team.currentMentorAssignment?.status === 'Active') {
        team.currentMentorAssignment.status = 'Ended';
        team.currentMentorAssignment.endedAtUtc = now;
      }
    });
    cls.status = 'Completed';
    cls.completedAtUtc = now;
    cls.completionReason = reason;
    cls.isEnrollmentMajorLocked = false;
    cls.rowVersion = allocateRowVersion();
    addAudit(classId, 'CLASS_COMPLETED', { reason });
  }

  refreshClassCounts(classId);
  persistMockState();
  return ok({
    classId,
    status: cls.status,
    completedAtUtc: cls.completedAtUtc ?? null,
    archivedAtUtc: null,
    rowVersion: cls.rowVersion,
  }, `Class ${reopen ? 'reopened' : 'completed'} successfully.`);
}

function changeLifecycle(config: AxiosRequestConfig, target: 'Archived' | 'Restore') {
  const classId = routeId(config, /^\/classes\/([^/]+)\/(archive|restore)$/);
  const cls = findClass(classId);
  if (!cls) return failure(404, 'CLASS_NOT_FOUND', 'Class not found.');
  const body = parseBody(config);
  const reason = asString(body.reason).trim();
  if (reason.length < 3) return failure(400, 'CLASS_VALIDATION_ERROR', 'A reason of at least three characters is required.');
  if (asString(body.rowVersion) && body.rowVersion !== cls.rowVersion) return failure(409, 'CLASS_CONCURRENCY_CONFLICT', 'The class was changed by another request.');
  if (target === 'Archived') {
    if (cls.status !== 'Archived') cls.previousStatus = cls.status === 'Inactive' ? 'Inactive' : cls.status;
    cls.status = 'Archived';
    cls.rowVersion = allocateRowVersion();
    addAudit(classId, 'CLASS_ARCHIVED', { reason });
  } else {
    if (cls.status !== 'Archived') return ok({ classId, status: cls.status, archivedAtUtc: null, rowVersion: cls.rowVersion }, 'Class is already restored.');
    cls.status = cls.primaryLecturerId && cls.schedules.length ? cls.previousStatus : 'Draft';
    cls.rowVersion = allocateRowVersion();
    addAudit(classId, 'CLASS_RESTORED', { reason });
  }
  persistMockState();
  return ok({ classId, status: cls.status, archivedAtUtc: cls.status === 'Archived' ? new Date().toISOString() : null, rowVersion: cls.rowVersion }, `Class ${target === 'Archived' ? 'archived' : 'restored'} successfully.`);
}

function registerRosterHandlers(mock: MockAdapter): void {
  mock.onPost(/^\/classes\/[^/]+\/students$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/students$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    const body = parseBody(config);
    const code = asString(body.studentCode).trim().toUpperCase();
    const fullName = asString(body.fullName).trim();
    const email = asString(body.email).trim().toLowerCase();
    const requestedMajor = asString(body.majorCode).trim().toUpperCase();
    if (!code || code.length > 20 || !fullName || fullName.length > 150 ||
        !email || email.length > 150 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      return failure(400, 'CLASS_VALIDATION_ERROR', 'Student code, full name, and a valid email are required.');
    }
    if (requestedMajor && !validMajorCodes.has(requestedMajor))
      return failure(400, 'CLASS_VALIDATION_ERROR', `Major code '${requestedMajor}' is invalid.`);

    const state = getMockState();
    const roster = state.rosters[classId] ||= [];
    const allProfiles = Object.values(state.rosters).flat();
    const userByCode = state.users.find((item) => item.studentId?.toUpperCase() === code);
    const userByEmail = state.users.find((item) => item.email.toLowerCase() === email);
    const profileByCode = allProfiles.find((item) => item.rollNumber.toUpperCase() === code);
    const profileByEmail = allProfiles.find((item) => item.email.toLowerCase() === email);
    const codeIdentity = userByCode?.id || profileByCode?.studentId;
    const emailIdentity = userByEmail?.id || profileByEmail?.studentId;
    if (codeIdentity && emailIdentity && codeIdentity !== emailIdentity) {
      return failure(409, 'STUDENT_IDENTITY_CONFLICT', `Student code '${code}' and email '${email}' belong to different student profiles.`);
    }

    const user = userByCode || userByEmail;
    const profile = profileByCode || profileByEmail;
    const profileId = user?.id || profile?.studentId;
    const profileEmail = user?.email || profile?.email;
    const profileCode = user?.studentId || profile?.rollNumber;
    if (profileEmail && profileEmail.toLowerCase() !== email) {
      return failure(409, 'STUDENT_IDENTITY_CONFLICT', `Student code '${code}' is registered with email '${profileEmail}'.`);
    }
    if (profileCode && profileCode.toUpperCase() !== code) {
      return failure(409, 'STUDENT_IDENTITY_CONFLICT', `Email '${email}' is registered with student code '${profileCode}'.`);
    }

    const profileMajor = (user?.major || profile?.profileMajorCode || '').toUpperCase();
    if (profileMajor && requestedMajor && profileMajor !== requestedMajor) {
      return failure(409, 'STUDENT_MAJOR_MISMATCH', `Selected major '${requestedMajor}' does not match the registered major '${profileMajor}'.`);
    }
    const enrollmentMajor = profileMajor || requestedMajor;
    if (!enrollmentMajor) {
      return failure(400, 'CLASS_VALIDATION_ERROR', profileId
        ? 'The existing student profile has no valid registered major. Select a major for this enrollment.'
        : 'Major is required when creating a new student profile.');
    }

    const existing = roster.find((student) =>
      student.studentId === profileId || student.rollNumber.toUpperCase() === code || student.email.toLowerCase() === email);
    if (existing) {
      return existing.enrollmentStatus === 'Dropped'
        ? failure(409, 'STUDENT_RE_ENROLLMENT_REQUIRED', `Student '${code}' has a dropped enrollment. Use the explicit re-enroll action.`)
        : failure(409, 'STUDENT_ALREADY_ENROLLED', `Student '${code}' already has an enrollment in this class.`);
    }

    const targetClass = findClass(classId)!;
    const conflictingClass = state.classes.find((candidate) =>
      candidate.id !== classId && candidate.courseId === targetClass.courseId && candidate.semesterId === targetClass.semesterId &&
      (state.rosters[candidate.id] || []).some((student) =>
        student.studentId === profileId && ['Active', 'Completed'].includes(student.enrollmentStatus)));
    if (conflictingClass) {
      return failure(409, 'STUDENT_ENROLLMENT_CONFLICT', `Student '${code}' is already enrolled in active class '${conflictingClass.classCode}' for the same subject and academic term.`);
    }

    const student: MockRosterStudent = {
      studentId: profileId || allocateId(), userId: user?.id || null, rollNumber: code, fullName, email,
      majorCode: enrollmentMajor, profileMajorCode: profileMajor || enrollmentMajor,
      majorVerificationStatus: 'Unverified', memberCode: `MEM-${state.sequence}`,
      enrollmentStatus: 'Active', teamId: null, teamName: null, isTeamLeader: false,
      joinedAtUtc: new Date().toISOString(),
    };
    roster.push(student);
    refreshClassCounts(classId);
    persistMockState();
    return ok(student, 'Student added to class successfully.');
  });

  mock.onPut(/^\/classes\/[^/]+\/students\/[^/]+\/major$/).reply((config) => {
    const match = config.url?.match(/^\/classes\/([^/]+)\/students\/([^/]+)\/major$/);
    const guard = classMutationGuard(match?.[1] || '');
    if (guard) return guard;
    const student = (getMockState().rosters[match?.[1] || ''] || []).find((item) => item.studentId === match?.[2]);
    if (!student) return failure(404, 'CLASS_STUDENT_NOT_FOUND', 'Student enrollment not found.');
    student.majorCode = asString(parseBody(config).majorCode).toUpperCase();
    student.majorVerificationStatus = 'Unverified';
    persistMockState();
    return ok(student, 'Enrollment major updated successfully.');
  });

  mock.onPost(/^\/classes\/[^/]+\/major-lock$/).reply((config) => setMajorLock(config, true));
  mock.onDelete(/^\/classes\/[^/]+\/major-lock$/).reply((config) => setMajorLock(config, false));

  mock.onPost(/^\/classes\/[^/]+\/students\/synchronize-profile-majors$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/students\/synchronize-profile-majors$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    let synchronizedCount = 0;
    for (const student of getMockState().rosters[classId] || []) {
      if (!student.userId || !student.majorCode || student.majorCode === 'UNDECLARED' || student.profileMajorCode === student.majorCode) continue;
      student.profileMajorCode = student.majorCode;
      synchronizedCount++;
    }
    persistMockState();
    return ok({ mismatchCount: synchronizedCount, synchronizedCount }, `Synchronized ${synchronizedCount} registered major(s).`);
  });

  mock.onPost(/^\/classes\/[^/]+\/students\/[^/]+\/drop$/).reply((config) => changeEnrollment(config, 'Dropped'));
  mock.onPost(/^\/classes\/[^/]+\/students\/[^/]+\/re-enroll$/).reply((config) => changeEnrollment(config, 'Active'));

  mock.onPost(/^\/classes\/[^/]+\/major-verification$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/major-verification$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    const rows = (getMockState().rosters[classId] || []).filter((student) => student.enrollmentStatus === 'Active');
    const matched = rows.filter((_, index) => index % 4 !== 3).map((student, index) => ({ rowNumber: index + 2, studentId: student.studentId, rollNumber: student.rollNumber, fullName: student.fullName, email: student.email, majorInFile: student.majorCode, majorInDb: student.majorCode, status: 'Matched', message: null }));
    const mismatched = rows.filter((_, index) => index % 4 === 3).map((student, index) => ({ rowNumber: index + 2, studentId: student.studentId, rollNumber: student.rollNumber, fullName: student.fullName, email: student.email, majorInFile: student.majorCode === 'BIT_SE' ? 'BBA_IB' : 'BIT_SE', majorInDb: student.majorCode, status: 'Mismatched', message: 'Major in file differs from enrollment major.' }));
    matched.forEach((row) => { const student = rows.find((item) => item.studentId === row.studentId); if (student) student.majorVerificationStatus = 'Verified'; });
    persistMockState();
    return ok({ matched, mismatched, missing: [], notFound: [] }, 'Student majors verified successfully.');
  });

  mock.onPost(/^\/classes\/[^/]+\/import-students\/preview$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/import-students\/preview$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    const sessionId = allocateId();
    const rows = [
      { rowNumber: 2, studentCode: `MOCK${getMockState().sequence}`, fullName: 'Mock Import Student', email: `mock.import.${getMockState().sequence}@fpt.edu.vn`, majorCode: 'BIT_SE', isValid: true, status: 'Valid', errorMessage: null },
      { rowNumber: 3, studentCode: '', fullName: 'Invalid Mock Row', email: 'invalid-email', majorCode: '', isValid: false, status: 'Invalid', errorMessage: 'StudentCode, valid Email, and MajorCode are required.' },
    ];
    getMockState().imports[sessionId] = { classId, consumed: false, rows };
    persistMockState();
    return ok({ sessionId, totalRows: rows.length, validRowsCount: 1, errorRowsCount: 1, majorMismatchCount: 0, rows }, 'Student import preview generated.');
  });

  mock.onPost(/^\/classes\/[^/]+\/import-students\/commit$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/import-students\/commit$/);
    const guard = classMutationGuard(classId);
    if (guard) return guard;
    const session = getMockState().imports[asString(parseBody(config).sessionId)];
    if (!session || session.classId !== classId) return failure(404, 'CLASS_IMPORT_SESSION_NOT_FOUND', 'Import session was not found or has expired.');
    if (session.consumed) return failure(409, 'CLASS_IMPORT_SESSION_CONSUMED', 'Import session has already been committed.');
    const validRows = session.rows.filter((row) => row.isValid);
    const roster = getMockState().rosters[classId] ||= [];
    for (const row of validRows) {
      roster.push({ studentId: allocateId(), userId: null, rollNumber: row.studentCode, fullName: row.fullName, email: row.email, majorCode: row.majorCode, profileMajorCode: null, majorVerificationStatus: 'Unverified', memberCode: `MEM-${getMockState().sequence}`, enrollmentStatus: 'Active', teamId: null, teamName: null, isTeamLeader: false, joinedAtUtc: new Date().toISOString() });
    }
    session.consumed = true;
    refreshClassCounts(classId);
    persistMockState();
    return ok({ insertedCount: validRows.length, updatedCount: 0, synchronizedMajorCount: 0, skippedCount: 0, errorCount: 0, errors: [] }, 'Students imported successfully.');
  });
}

function setMajorLock(config: AxiosRequestConfig, locked: boolean) {
  const classId = routeId(config, /^\/classes\/([^/]+)\/major-lock$/);
  const guard = classMutationGuard(classId);
  if (guard) return guard;
  const cls = findClass(classId)!;
  cls.isEnrollmentMajorLocked = locked;
  cls.rowVersion = allocateRowVersion();
  addAudit(classId, locked ? 'ENROLLMENT_MAJOR_LOCKED' : 'ENROLLMENT_MAJOR_UNLOCKED', {});
  persistMockState();
  return ok({ classId, isLocked: locked }, `Enrollment majors ${locked ? 'locked' : 'unlocked'} successfully.`);
}

function changeEnrollment(config: AxiosRequestConfig, status: 'Dropped' | 'Active') {
  const match = config.url?.match(/^\/classes\/([^/]+)\/students\/([^/]+)\/(drop|re-enroll)$/);
  const classId = match?.[1] || '';
  const guard = classMutationGuard(classId);
  if (guard) return guard;
  const student = (getMockState().rosters[classId] || []).find((item) => item.studentId === match?.[2]);
  if (!student) return failure(404, 'CLASS_STUDENT_NOT_FOUND', 'Student enrollment not found.');
  student.enrollmentStatus = status;
  if (status === 'Dropped') {
    const team = getMockState().teams.find((item) => item.id === student.teamId);
    if (team) team.members = team.members.filter((member) => member.studentId !== student.studentId);
    student.teamId = null;
    student.teamName = null;
    student.isTeamLeader = false;
  }
  refreshClassCounts(classId);
  persistMockState();
  return ok(status === 'Active' ? student : null, `Student enrollment ${status === 'Active' ? 'restored' : 'dropped'} successfully.`);
}

function registerDownloads(mock: MockAdapter): void {
  const excelHeaders = { 'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' };
  mock.onGet('/classes/import-template').reply(() => [200, new Blob(['Mock E-HUB student import template']), excelHeaders]);
  mock.onGet('/classes/major-verification-template').reply(() => [200, new Blob(['Mock E-HUB major verification template']), excelHeaders]);
  mock.onGet(/^\/classes\/[^/]+\/(export-excel|export-students)$/).reply((config) => {
    const classId = routeId(config, /^\/classes\/([^/]+)\/(export-excel|export-students)$/);
    const rows = (getMockState().rosters[classId] || []).map((student) => `${student.rollNumber},${student.fullName},${student.email},${student.majorCode || ''}`).join('\n');
    return [200, new Blob([`StudentCode,FullName,Email,MajorCode\n${rows}`]), excelHeaders];
  });
}

export function registerClassMockHandlers(mock: MockAdapter): void {
  // Static download routes must be registered before the generic /classes/:id route.
  registerDownloads(mock);
  registerClassQueries(mock);
  registerClassCrud(mock);
  registerRosterHandlers(mock);
}
