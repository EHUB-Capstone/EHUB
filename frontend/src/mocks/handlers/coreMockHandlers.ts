import type MockAdapter from 'axios-mock-adapter';
import type { AxiosRequestConfig } from 'axios';
import type { LoginPayload, RegisterPayload } from '../../types/auth.ts';
import {
  normalizeLoginPayload,
  normalizeRegisterPayload,
  validateLoginPayload,
  validateRegisterPayload,
} from '../../utils/authValidation.ts';
import type { MockCurriculum, MockUser } from '../mockState.ts';
import type { MockReply } from '../mockHelpers.ts';
import {
  allocateId,
  asNumber,
  asString,
  asStringArray,
  created,
  failure,
  getMockState,
  ok,
  parseBody,
  persistMockState,
  requestParams,
  routeId,
} from '../mockHelpers.ts';

const emptyCurriculum = (): MockCurriculum => ({ roadmapItems: [], rubrics: [], checkpoints: [] });

const backendRole = (role: MockUser['role']): string =>
  role.charAt(0) + role.slice(1).toLowerCase();

const backendStatus: Record<MockUser['status'], string> = {
  APPROVED: 'Active',
  PENDING: 'PendingApproval',
  REJECTED: 'Rejected',
  BLOCKED: 'Blocked',
  INACTIVE: 'Inactive',
};

const validationFailure = (
  errors: ReturnType<typeof validateLoginPayload> | ReturnType<typeof validateRegisterPayload>,
) => failure(400, 'COMMON_VALIDATION_ERROR', 'Validation failed', errors);

function userResponse(user: MockUser): MockUser {
  return { ...user, _id: user.id };
}

function accountStatusFailure(user: MockUser): MockReply | null {
  switch (user.status) {
    case 'PENDING':
      return failure(403, 'AUTH_ACCOUNT_PENDING_APPROVAL', 'Your account is pending admin approval.');
    case 'REJECTED':
      return failure(403, 'AUTH_ACCOUNT_REJECTED', 'Your account registration has been rejected.');
    case 'BLOCKED':
      return failure(403, 'AUTH_USER_BLOCKED', 'Your account has been blocked.');
    case 'INACTIVE':
      return failure(403, 'AUTH_USER_INACTIVE', 'Your account is inactive.');
    case 'APPROVED':
      return null;
    default:
      return failure(403, 'AUTH_USER_INACTIVE', 'Your account is inactive.');
  }
}

interface MockGoogleIdentity {
  email: string;
  emailVerified: boolean;
}

function googleIdentityFromToken(idToken: string): MockGoogleIdentity | null {
  const verifiedPrefix = 'mock-google:';
  const unverifiedPrefix = 'mock-google-unverified:';
  if (idToken.startsWith(verifiedPrefix)) {
    return { email: idToken.slice(verifiedPrefix.length), emailVerified: true };
  }
  if (idToken.startsWith(unverifiedPrefix)) {
    return { email: idToken.slice(unverifiedPrefix.length), emailVerified: false };
  }

  try {
    const parts = idToken.split('.');
    if (parts.length !== 3 || !parts[1]) return null;
    const normalized = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=');
    const bytes = Uint8Array.from(atob(padded), character => character.charCodeAt(0));
    const payload = JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>;
    if (typeof payload.email !== 'string') return null;
    return { email: payload.email, emailVerified: payload.email_verified === true };
  } catch {
    return null;
  }
}

function authUser(user: MockUser) {
  return {
    id: user.id,
    fullName: user.name,
    email: user.email,
    roles: [backendRole(user.role)],
    status: backendStatus[user.status],
    majorCode: user.major,
  };
}

function authResponse(user: MockUser) {
  return {
    accessToken: `mock-access-token-${user.id}`,
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: authUser(user),
  };
}

function registerAuthHandlers(mock: MockAdapter): void {
  mock.onPost('/auth/login').reply((config) => {
    const body = parseBody(config);
    const rawPayload: LoginPayload = {
      email: asString(body.email),
      password: asString(body.password),
    };
    const validationErrors = validateLoginPayload(rawPayload);
    if (validationErrors.length > 0) return validationFailure(validationErrors);

    const payload = normalizeLoginPayload(rawPayload);
    const user = getMockState().users.find((item) => item.email.toLowerCase() === payload.email);
    const expectedPassword = user ? getMockState().authPasswords[user.id] ?? 'Mock123!' : null;
    if (!user || payload.password !== expectedPassword) {
      return failure(401, 'AUTH_INVALID_CREDENTIALS', 'Invalid email or password.');
    }
    const statusFailure = accountStatusFailure(user);
    if (statusFailure) return statusFailure;
    getMockState().sessionUserId = user.id;
    persistMockState();
    return ok(authResponse(user), 'Login successfully');
  });

  mock.onPost('/auth/google').reply((config) => {
    const idToken = asString(parseBody(config).idToken);
    const validationErrors = [];
    if (!idToken.trim()) {
      validationErrors.push({
        field: 'idToken',
        message: 'Google ID Token is required.',
        code: 'NotEmptyValidator',
      });
    }
    if (idToken.length > 5_000) {
      validationErrors.push({
        field: 'idToken',
        message: 'Google ID Token must not exceed 5000 characters.',
        code: 'MaximumLengthValidator',
      });
    }
    if (validationErrors.length > 0) {
      return failure(400, 'COMMON_VALIDATION_ERROR', 'Validation failed', validationErrors);
    }

    const identity = googleIdentityFromToken(idToken);
    if (!identity) return failure(401, 'AUTH_INVALID_GOOGLE_TOKEN', 'Invalid Google token.');
    if (!identity.emailVerified) {
      return failure(401, 'AUTH_GOOGLE_EMAIL_NOT_VERIFIED', 'Google email is not verified.');
    }

    const email = identity.email.trim().toLowerCase();
    const user = getMockState().users.find((item) => item.email.toLowerCase() === email);
    if (!user) {
      return failure(404, 'AUTH_ACCOUNT_NOT_REGISTERED', 'Account is not registered. Please create an account first.');
    }
    const statusFailure = accountStatusFailure(user);
    if (statusFailure) return statusFailure;

    getMockState().sessionUserId = user.id;
    persistMockState();
    return ok(authResponse(user), 'Google login successfully');
  });

  mock.onPost('/auth/refresh-token').reply(() => {
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    if (!user) {
      return failure(401, 'AUTH_REFRESH_TOKEN_INVALID', 'Refresh token is missing or invalid.');
    }
    const statusFailure = accountStatusFailure(user);
    if (statusFailure) {
      state.sessionUserId = null;
      persistMockState();
      return statusFailure;
    }
    return ok(authResponse(user), 'Token refreshed successfully');
  });

  mock.onGet('/auth/me').reply(() => {
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    if (!user) return failure(401, 'COMMON_UNAUTHORIZED', 'Unauthorized access.');
    const statusFailure = accountStatusFailure(user);
    if (statusFailure) return statusFailure;
    return ok(authUser(user), 'Current user retrieved successfully');
  });

  mock.onPost('/auth/logout').reply(() => {
    getMockState().sessionUserId = null;
    persistMockState();
    return ok(null, 'Logout successfully');
  });

  mock.onPost('/auth/register').reply((config) => {
    const body = parseBody(config);
    const rawPayload: RegisterPayload = {
      fullName: asString(body.fullName),
      email: asString(body.email),
      password: asString(body.password),
      confirmPassword: asString(body.confirmPassword),
      role: asString(body.role),
      majorCode: body.majorCode === undefined || body.majorCode === null
        ? undefined
        : asString(body.majorCode),
    };
    const validationErrors = validateRegisterPayload(rawPayload);
    if (validationErrors.length > 0) return validationFailure(validationErrors);

    const payload = normalizeRegisterPayload(rawPayload);
    const email = payload.email;
    if (getMockState().users.some((user) => user.email.toLowerCase() === email)) {
      return failure(409, 'AUTH_EMAIL_ALREADY_EXISTS', 'Email already exists.');
    }
    const role = payload.role.toUpperCase() as MockUser['role'];
    const major = role === 'STUDENT' ? payload.majorCode ?? null : null;
    const id = allocateId();
    const user: MockUser = {
      id, _id: id, name: payload.fullName, email, avatar: null, role,
      status: role === 'STUDENT' ? 'APPROVED' : 'PENDING',
      studentId: null, programGroup: major?.split('_')[0] ?? null,
      major,
      phone: null, createdAt: new Date().toISOString(), lastSeen: null,
    };
    getMockState().users.unshift(user);
    getMockState().authPasswords[user.id] = payload.password;
    if (user.status === 'APPROVED') getMockState().sessionUserId = user.id;
    persistMockState();
    const session = user.status === 'APPROVED' ? authResponse(user) : null;
    const message = user.status === 'APPROVED'
      ? 'Register successfully'
      : 'Your account has been registered and is pending admin approval.';
    return ok({
      status: user.status === 'APPROVED' ? 'Active' : 'PendingApproval',
      requiresApproval: user.status !== 'APPROVED',
      message,
      user: authUser(user),
      accessToken: session?.accessToken ?? null,
      expiresAt: session?.expiresAt ?? null,
    }, message);
  });

  mock.onPost('/auth/forgot-password').reply(() => ok(null, 'If the email exists, a reset link has been sent.'));
  mock.onPost('/auth/reset-password').reply(() => ok(null, 'Password reset successfully.'));
}

function registerUserHandlers(mock: MockAdapter): void {
  mock.onGet('/users').reply((config) => {
    const params = requestParams(config);
    const query = asString(params.search).trim().toLowerCase();
    const role = asString(params.role).toUpperCase();
    const status = asString(params.status).toUpperCase();
    const page = Math.max(1, asNumber(params.page, 1));
    const limit = Math.min(200, Math.max(1, asNumber(params.limit, 10)));
    let users = getMockState().users.filter((user) =>
      (!role || role === 'ALL' || user.role === role)
      && (!status || status === 'ALL' || user.status === status)
      && (!query || [user.name, user.email, user.studentId].some((value) => value?.toLowerCase().includes(query))));
    const total = users.length;
    users = users.slice((page - 1) * limit, page * limit);
    return ok({
      users: users.map(userResponse),
      pagination: { total, page, limit, pages: Math.max(1, Math.ceil(total / limit)) },
    }, 'Users retrieved successfully.');
  });

  mock.onGet(/^\/users\/[^/]+$/).reply((config) => {
    const user = getMockState().users.find((item) => item.id === routeId(config, /^\/users\/([^/]+)$/));
    return user ? ok(userResponse(user), 'User retrieved successfully.') : failure(404, 'USER_NOT_FOUND', 'User not found.');
  });

  mock.onPost('/users').reply((config) => {
    const body = parseBody(config);
    const email = asString(body.email).trim().toLowerCase();
    if (!email || getMockState().users.some((user) => user.email.toLowerCase() === email)) {
      return failure(409, 'USER_EMAIL_EXISTS', 'A user with this email already exists.');
    }
    const id = allocateId();
    const role = asString(body.role, 'STUDENT').toUpperCase() as MockUser['role'];
    const user: MockUser = {
      id,
      _id: id,
      name: asString(body.name, 'Mock User').trim(),
      email,
      avatar: null,
      role,
      status: asString(body.status, 'APPROVED').toUpperCase() as MockUser['status'],
      studentId: role === 'STUDENT' ? asString(body.studentId) || null : null,
      programGroup: role === 'STUDENT' ? asString(body.programGroup) || null : null,
      major: role === 'STUDENT' ? asString(body.major) || null : null,
      phone: asString(body.phone) || null,
      createdAt: new Date().toISOString(),
      lastSeen: null,
    };
    getMockState().users.unshift(user);
    persistMockState();
    return created(userResponse(user), 'User created successfully.');
  });

  mock.onPut(/^\/users\/[^/]+$/).reply((config) => {
    const user = getMockState().users.find((item) => item.id === routeId(config, /^\/users\/([^/]+)$/));
    if (!user) return failure(404, 'USER_NOT_FOUND', 'User not found.');
    const body = parseBody(config);
    user.name = asString(body.name, user.name).trim();
    user.email = asString(body.email, user.email).trim().toLowerCase();
    user.role = asString(body.role, user.role).toUpperCase() as MockUser['role'];
    user.status = asString(body.status, user.status).toUpperCase() as MockUser['status'];
    user.phone = asString(body.phone, user.phone || '') || null;
    user.studentId = user.role === 'STUDENT' ? asString(body.studentId, user.studentId || '') || null : null;
    user.programGroup = user.role === 'STUDENT' ? asString(body.programGroup, user.programGroup || '') || null : null;
    user.major = user.role === 'STUDENT' ? asString(body.major, user.major || '') || null : null;
    persistMockState();
    return ok(userResponse(user), 'User updated successfully.');
  });

  mock.onDelete(/^\/users\/[^/]+$/).reply((config) => {
    const id = routeId(config, /^\/users\/([^/]+)$/);
    const index = getMockState().users.findIndex((user) => user.id === id);
    if (index < 0) return failure(404, 'USER_NOT_FOUND', 'User not found.');
    getMockState().users.splice(index, 1);
    persistMockState();
    return ok(null, 'User deleted successfully.');
  });

  mock.onPost(/^\/admin\/users\/[^/]+\/(approve|reject)$/).reply((config) => {
    const match = config.url?.match(/^\/admin\/users\/([^/]+)\/(approve|reject)$/);
    const user = getMockState().users.find((item) => item.id === match?.[1]);
    if (!user) return failure(404, 'USER_NOT_FOUND', 'User not found.');
    user.status = match?.[2] === 'approve' ? 'APPROVED' : 'REJECTED';
    persistMockState();
    return ok(userResponse(user), `User ${match?.[2] === 'approve' ? 'approved' : 'rejected'} successfully.`);
  });
}

function subjectCodeFrom(config: AxiosRequestConfig, pattern: RegExp): string {
  return decodeURIComponent(routeId(config, pattern)).toUpperCase();
}

function registerSubjectHandlers(mock: MockAdapter): void {
  mock.onGet('/subjects').reply((config) => {
    const params = requestParams(config);
    const query = asString(params.search).trim().toLowerCase();
    const status = asString(params.status).toLowerCase();
    const subjects = getMockState().subjects.filter((subject) =>
      (!status || subject.status === status)
      && (!query || subject.subjectCode.toLowerCase().includes(query) || subject.subjectName.toLowerCase().includes(query)));
    return ok({ subjects }, 'Subjects retrieved successfully.');
  });

  mock.onGet('/subjects/active').reply(() =>
    ok({ subjects: getMockState().subjects.filter((subject) => subject.status === 'active') }, 'Active subjects retrieved successfully.'));

  mock.onPost('/subjects').reply((config) => {
    const body = parseBody(config);
    const subjectCode = asString(body.subjectCode).trim().toUpperCase();
    if (!subjectCode || getMockState().subjects.some((subject) => subject.subjectCode === subjectCode)) {
      return failure(409, 'SUBJECT_CODE_EXISTS', 'Subject code already exists.');
    }
    const subject = {
      _id: allocateId(),
      subjectCode,
      subjectName: asString(body.subjectName).trim(),
      status: asString(body.status, 'active').toLowerCase() === 'disabled' ? 'disabled' as const : 'active' as const,
    };
    getMockState().subjects.unshift(subject);
    getMockState().curricula[subjectCode] = emptyCurriculum();
    persistMockState();
    return created(subject, 'Subject created successfully.');
  });

  mock.onPut(/^\/subjects\/[^/]+$/).reply((config) => {
    const id = routeId(config, /^\/subjects\/([^/]+)$/);
    const subject = getMockState().subjects.find((item) => item._id === id);
    if (!subject) return failure(404, 'SUBJECT_NOT_FOUND', 'Subject not found.');
    const body = parseBody(config);
    subject.subjectName = asString(body.subjectName, subject.subjectName).trim();
    subject.status = asString(body.status, subject.status).toLowerCase() === 'disabled' ? 'disabled' : 'active';
    persistMockState();
    return ok(subject, 'Subject updated successfully.');
  });

  mock.onDelete(/^\/subjects\/[^/]+$/).reply((config) => {
    const subject = getMockState().subjects.find((item) => item._id === routeId(config, /^\/subjects\/([^/]+)$/));
    if (!subject) return failure(404, 'SUBJECT_NOT_FOUND', 'Subject not found.');
    subject.status = 'disabled';
    persistMockState();
    return ok(null, 'Subject disabled successfully.');
  });

  mock.onGet('/subjects/current-semester').reply(() => {
    const currentSemester = getMockState().currentSemester;
    return ok({ currentSemester, availableYears: [2025, 2026, 2027], isDecember: false }, 'Current semester retrieved successfully.');
  });

  mock.onPost('/subjects/current-semester').reply((config) => {
    const body = parseBody(config);
    const semester = asString(body.semester, 'FA').toUpperCase();
    if (!['SP', 'SU', 'FA'].includes(semester)) return failure(400, 'SEMESTER_INVALID', 'Semester must be SP, SU, or FA.');
    getMockState().currentSemester = { semester: semester as 'SP' | 'SU' | 'FA', year: asNumber(body.year, 2026) };
    persistMockState();
    return ok({ currentSemester: getMockState().currentSemester }, 'Current semester updated successfully.');
  });

  mock.onGet('/subjects/teaching-staff').reply(() => {
    const staff = getMockState().users.filter((user) => user.role === 'LECTURER' || user.role === 'MENTOR').map((user) => {
      const assignments = user.role === 'LECTURER'
        ? getMockState().classes.filter((cls) => cls.primaryLecturerId === user.id && cls.status !== 'Archived').map((cls) => ({ _id: cls.id, classCode: cls.classCode, subjectCode: cls.subjectCode }))
        : getMockState().teams.filter((team) => team.currentMentorAssignment?.mentor.userId === user.id && team.currentMentorAssignment.status === 'Active').map((team) => {
          const cls = getMockState().classes.find((item) => item.id === team.classId);
          return { _id: team.id, classCode: cls?.classCode || '-', subjectCode: cls?.subjectCode || '-' };
        });
      return { _id: user.id, name: user.name, email: user.email, avatar: user.avatar, role: user.role, status: user.status, classCount: assignments.length, assignments };
    });
    const lecturers = staff.filter((item) => item.role === 'LECTURER').length;
    const mentors = staff.filter((item) => item.role === 'MENTOR').length;
    const assigned = staff.filter((item) => item.assignments.length > 0).length;
    return ok({ staff, summary: { lecturers, mentors, assigned, unassigned: staff.length - assigned, classes: getMockState().classes.filter((cls) => cls.status !== 'Archived').length } }, 'Teaching staff retrieved successfully.');
  });

  mock.onGet(/^\/subjects\/[^/]+\/curriculum$/).reply((config) => {
    const subjectCode = subjectCodeFrom(config, /^\/subjects\/([^/]+)\/curriculum$/);
    const subject = getMockState().subjects.find((item) => item.subjectCode === subjectCode);
    if (!subject) return failure(404, 'SUBJECT_NOT_FOUND', 'Subject not found.');
    return ok({ subject, ...(getMockState().curricula[subjectCode] || emptyCurriculum()) }, 'Subject curriculum retrieved successfully.');
  });

  mock.onPut(/^\/subjects\/[^/]+\/checkpoints$/).reply((config) => {
    const subjectCode = subjectCodeFrom(config, /^\/subjects\/([^/]+)\/checkpoints$/);
    const curriculum = getMockState().curricula[subjectCode];
    if (!curriculum) return failure(404, 'SUBJECT_NOT_FOUND', 'Subject not found.');
    const checkpoints = parseBody(config).checkpoints;
    curriculum.checkpoints = Array.isArray(checkpoints) ? checkpoints as MockCurriculum['checkpoints'] : [];
    persistMockState();
    const subject = getMockState().subjects.find((item) => item.subjectCode === subjectCode)!;
    return ok({ subject, ...curriculum }, 'Subject checkpoints synchronized successfully.');
  });

  mock.onPost(/^\/subjects\/[^/]+\/roadmap$/).reply((config) => saveRoadmap(config, false));
  mock.onPut(/^\/subjects\/[^/]+\/roadmap\/[^/]+$/).reply((config) => saveRoadmap(config, true));
  mock.onDelete(/^\/subjects\/[^/]+\/roadmap\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/subjects\/([^/]+)\/roadmap\/([^/]+)$/);
    const curriculum = getMockState().curricula[decodeURIComponent(match?.[1] || '').toUpperCase()];
    if (!curriculum) return failure(404, 'SUBJECT_NOT_FOUND', 'Subject not found.');
    curriculum.roadmapItems = curriculum.roadmapItems.filter((item) => item._id !== match?.[2]);
    persistMockState();
    return ok(null, 'Roadmap item deleted successfully.');
  });

  mock.onPost(/^\/subjects\/[^/]+\/rubrics$/).reply((config) => saveRubric(config, false));
  mock.onPut(/^\/subjects\/[^/]+\/rubrics\/[^/]+$/).reply((config) => saveRubric(config, true));
  mock.onDelete(/^\/subjects\/[^/]+\/rubrics\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/subjects\/([^/]+)\/rubrics\/([^/]+)$/);
    const curriculum = getMockState().curricula[decodeURIComponent(match?.[1] || '').toUpperCase()];
    if (!curriculum) return failure(404, 'SUBJECT_NOT_FOUND', 'Subject not found.');
    curriculum.rubrics = curriculum.rubrics.filter((item) => item._id !== match?.[2]);
    persistMockState();
    return ok(null, 'Rubric deleted successfully.');
  });

  mock.onPost(/^\/subjects\/[^/]+\/rubrics\/[^/]+\/criteria$/).reply((config) => saveCriterion(config, false));
  mock.onPut(/^\/subjects\/[^/]+\/rubrics\/[^/]+\/criteria\/[^/]+$/).reply((config) => saveCriterion(config, true));
  mock.onDelete(/^\/subjects\/[^/]+\/rubrics\/[^/]+\/criteria\/[^/]+$/).reply((config) => {
    const match = config.url?.match(/^\/subjects\/([^/]+)\/rubrics\/([^/]+)\/criteria\/([^/]+)$/);
    const rubric = getMockState().curricula[decodeURIComponent(match?.[1] || '').toUpperCase()]?.rubrics.find((item) => item._id === match?.[2]);
    if (!rubric) return failure(404, 'RUBRIC_NOT_FOUND', 'Rubric not found.');
    rubric.criteria = rubric.criteria.filter((criterion) => criterion._id !== match?.[3]);
    persistMockState();
    return ok(null, 'Criterion deleted successfully.');
  });
}

function saveRoadmap(config: AxiosRequestConfig, update: boolean) {
  const pattern = update ? /^\/subjects\/([^/]+)\/roadmap\/([^/]+)$/ : /^\/subjects\/([^/]+)\/roadmap$/;
  const match = config.url?.match(pattern);
  const subjectCode = decodeURIComponent(match?.[1] || '').toUpperCase();
  const curriculum = getMockState().curricula[subjectCode];
  if (!curriculum) return failure(404, 'SUBJECT_NOT_FOUND', 'Subject not found.');
  const body = parseBody(config);
  const existing = update ? curriculum.roadmapItems.find((item) => item._id === match?.[2]) : undefined;
  if (update && !existing) return failure(404, 'ROADMAP_ITEM_NOT_FOUND', 'Roadmap item not found.');
  const item = existing || { _id: allocateId(), title: '', description: null, taskType: 'COURSE_TEMPLATE', courseCode: subjectCode, weekNumber: 1, priority: 'MEDIUM', estimatedHours: null, tags: [] };
  item.title = asString(body.title, item.title).trim();
  item.description = asString(body.description, item.description || '') || null;
  item.taskType = asString(body.taskType, item.taskType);
  item.courseCode = asString(body.courseCode, subjectCode);
  item.weekNumber = asNumber(body.weekNumber, item.weekNumber);
  item.priority = asString(body.priority, item.priority);
  item.estimatedHours = body.estimatedHours === null ? null : asNumber(body.estimatedHours, item.estimatedHours || 0);
  item.tags = asStringArray(body.tags);
  if (!existing) curriculum.roadmapItems.push(item);
  persistMockState();
  return ok(item, `Roadmap item ${update ? 'updated' : 'created'} successfully.`);
}

function saveRubric(config: AxiosRequestConfig, update: boolean) {
  const pattern = update ? /^\/subjects\/([^/]+)\/rubrics\/([^/]+)$/ : /^\/subjects\/([^/]+)\/rubrics$/;
  const match = config.url?.match(pattern);
  const curriculum = getMockState().curricula[decodeURIComponent(match?.[1] || '').toUpperCase()];
  if (!curriculum) return failure(404, 'SUBJECT_NOT_FOUND', 'Subject not found.');
  const body = parseBody(config);
  const existing = update ? curriculum.rubrics.find((item) => item._id === match?.[2]) : undefined;
  if (update && !existing) return failure(404, 'RUBRIC_NOT_FOUND', 'Rubric not found.');
  const rubric = existing || { _id: allocateId(), name: '', description: null, status: 'DRAFT', totalWeight: 100, checkpointNumber: null, criteria: [] };
  rubric.name = asString(body.name, rubric.name).trim();
  rubric.description = asString(body.description, rubric.description || '') || null;
  rubric.status = asString(body.status, rubric.status);
  rubric.totalWeight = asNumber(body.totalWeight, rubric.totalWeight);
  rubric.checkpointNumber = body.checkpointNumber === null ? null : asNumber(body.checkpointNumber, rubric.checkpointNumber || 1);
  if (!existing) curriculum.rubrics.push(rubric);
  persistMockState();
  return ok(rubric, `Rubric ${update ? 'updated' : 'created'} successfully.`);
}

function saveCriterion(config: AxiosRequestConfig, update: boolean) {
  const pattern = update
    ? /^\/subjects\/([^/]+)\/rubrics\/([^/]+)\/criteria\/([^/]+)$/
    : /^\/subjects\/([^/]+)\/rubrics\/([^/]+)\/criteria$/;
  const match = config.url?.match(pattern);
  const rubric = getMockState().curricula[decodeURIComponent(match?.[1] || '').toUpperCase()]?.rubrics.find((item) => item._id === match?.[2]);
  if (!rubric) return failure(404, 'RUBRIC_NOT_FOUND', 'Rubric not found.');
  const body = parseBody(config);
  const existing = update ? rubric.criteria.find((item) => item._id === match?.[3]) : undefined;
  if (update && !existing) return failure(404, 'CRITERION_NOT_FOUND', 'Criterion not found.');
  const criterion = existing || { _id: allocateId(), name: '', description: null, maxScore: 10, weight: 0, displayOrder: rubric.criteria.length + 1 };
  criterion.name = asString(body.name, criterion.name).trim();
  criterion.description = asString(body.description, criterion.description || '') || null;
  criterion.maxScore = asNumber(body.maxScore, criterion.maxScore);
  criterion.weight = asNumber(body.weight, criterion.weight);
  criterion.displayOrder = asNumber(body.displayOrder, criterion.displayOrder);
  if (!existing) rubric.criteria.push(criterion);
  persistMockState();
  return ok(criterion, `Criterion ${update ? 'updated' : 'created'} successfully.`);
}

function registerDashboardHandlers(mock: MockAdapter): void {
  mock.onGet('/dashboard/admin').reply(() => {
    const state = getMockState();
    const usersByRole = ['ADMIN', 'LECTURER', 'MENTOR', 'STUDENT'].map((role) => ({ role, count: state.users.filter((user) => user.role === role).length }));
    return ok({
      stats: { totalUsers: state.users.length, totalClasses: state.classes.length, totalTeams: state.teams.length, totalIdeas: 14, totalEvaluations: 28, submittedProposals: state.proposals.filter((proposal) => proposal.status === 'Pending').length, totalMentoringSessions: 16, totalTasks: 92, completedTasks: 61, overallTaskProgress: 66.3 },
      usersByRole,
      ideasByStatus: [{ status: 'DRAFT', count: 4 }, { status: 'VALIDATING', count: 6 }, { status: 'APPROVED', count: 4 }],
      topTeams: state.teams.slice(0, 8).map((team, index) => ({ startupName: team.teamName, team: { name: team.teamName, classId: { classCode: state.classes.find((cls) => cls.id === team.classId)?.classCode || '-' } }, avgScore: 8.7 - index * 0.6 })),
    }, 'Admin dashboard retrieved successfully.');
  });

  mock.onGet('/dashboard/lecturer').reply(() => {
    const state = getMockState();
    const lecturerId = state.sessionUserId;
    const myClasses = state.classes.filter((cls) =>
      cls.primaryLecturerId === lecturerId && cls.status !== 'Archived');
    const classIds = new Set(myClasses.map((cls) => cls.id));
    const assignedTeams = state.teams.filter((team) => classIds.has(team.classId));
    const totalStudents = myClasses.reduce((sum, cls) => sum + (state.rosters[cls.id]?.filter((student) => student.enrollmentStatus === 'Active').length || 0), 0);
    const submittedProposals = state.proposals.filter((proposal) => classIds.has(proposal.classId) && proposal.status === 'Pending');
    const submittedDirections = state.directions.filter((direction) =>
      direction.status === 'Submitted' && assignedTeams.some((team) => team.id === direction.teamId));

    return ok({
      totalClasses: myClasses.length,
      totalTeams: assignedTeams.length,
      totalStudents,
      pendingReviews: submittedProposals.length + submittedDirections.length,
      myClasses: myClasses.map((cls) => ({
        _id: cls.id,
        id: cls.id,
        code: cls.classCode,
        name: cls.subjectName || cls.subjectCode,
        semester: `${cls.semesterCode.slice(0, 2)} ${cls.year}`,
        members: state.rosters[cls.id]?.filter((student) => student.enrollmentStatus === 'Active') || [],
      })),
      pendingIdeas: submittedProposals.map((proposal) => {
        const cls = state.classes.find((item) => item.id === proposal.classId);
        return {
          _id: proposal.id,
          startupName: proposal.projectName || proposal.teamName,
          teamId: { name: proposal.teamName, classId: { name: cls?.classCode || '-' } },
        };
      }),
      recentSessions: assignedTeams.slice(0, 2).map((team, index) => ({
        _id: `mock-session-${team.id}`,
        title: index === 0 ? 'Customer validation review' : 'Weekly mentoring sync',
        teamId: { name: team.teamName },
        meetingDate: new Date(Date.now() - (index + 1) * 86_400_000).toISOString(),
      })),
      teamRankings: assignedTeams.map((team, index) => ({ team: { id: team.id, name: team.teamName }, avgScore: 86 - index * 8 })),
    }, 'Lecturer dashboard retrieved successfully.');
  });

  mock.onGet('/dashboard/mentor').reply(() => {
    const state = getMockState();
    const teams = state.teams.filter((team) =>
      team.currentMentorAssignment?.status === 'Active'
      && team.currentMentorAssignment.mentor.userId === state.sessionUserId);
    return ok({
      myTeams: teams.length,
      pendingReviews: state.directions.filter((direction) => teams.some((team) => team.id === direction.teamId) && direction.status === 'Submitted').length,
      upcomingSessions: teams.length,
      averageScore: teams.length ? 88 : 0,
      taskProgress: teams.length ? 72 : 0,
      recentEvaluations: teams.slice(0, 2).map((team, index) => ({ _id: `mock-evaluation-${team.id}`, teamId: { teamName: team.teamName }, totalScore: 84 + index * 3 })),
      recentSessions: teams.slice(0, 2).map((team, index) => ({ _id: `mock-mentor-session-${team.id}`, title: 'Mentoring checkpoint', teamId: { teamName: team.teamName }, meetingDate: new Date(Date.now() - (index + 1) * 86_400_000).toISOString() })),
    }, 'Mentor dashboard retrieved successfully.');
  });

  mock.onGet(/^\/dashboard\/student(?:\?.*)?$/).reply((config) => {
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    const enrollment = Object.entries(state.rosters).flatMap(([classId, roster]) =>
      roster.map((student) => ({ classId, student }))).find((item) => item.student.userId === user?.id && item.student.enrollmentStatus === 'Active');
    const cls = enrollment ? state.classes.find((item) => item.id === enrollment.classId) : undefined;
    const team = enrollment ? state.teams.find((item) => item.id === enrollment.student.teamId) : undefined;
    const direction = team ? state.directions.find((item) => item.teamId === team.id) : undefined;
    const weekNumber = Math.min(10, Math.max(1, asNumber(new URLSearchParams(config.url?.split('?')[1] || '').get('weekNumber'), 1)));

    return ok({
      hasTeam: Boolean(team),
      roleInTeam: team?.leaderId === enrollment?.student.studentId ? 'Leader' : 'Member',
      myClass: cls ? { _id: cls.id, id: cls.id, name: cls.classCode, semester: `${cls.semesterCode.slice(0, 2)} ${cls.year}` } : null,
      team: team ? {
        _id: team.id,
        id: team.id,
        name: team.teamName,
        members: team.members.map((member) => ({
          userId: { _id: member.studentId, name: member.fullName },
          roleInTeam: member.roleInTeam === 'LEADER' ? 'Leader' : 'Member',
        })),
      } : null,
      startupIdea: team ? {
        _id: direction?.id || `mock-idea-${team.id}`,
        startupName: direction?.title || team.teamName,
        problem: direction?.summary || team.description || 'Validate the proposed startup problem with target customers.',
        status: direction?.status?.toUpperCase() || 'DRAFT',
      } : null,
      aiAnalysis: team ? { aiScore: 78 } : null,
      latestEvaluation: team ? { totalScore: 84.5, comment: 'Strong validation plan; clarify the primary customer segment.' } : null,
      milestones: team ? [
        { _id: `mock-milestone-${team.id}-1`, title: 'Interview five target users', status: 'DONE', dueDate: new Date(Date.now() - 86_400_000).toISOString() },
        { _id: `mock-milestone-${team.id}-2`, title: 'Synthesize validation evidence', status: 'IN_PROGRESS', dueDate: new Date(Date.now() + 4 * 86_400_000).toISOString() },
      ] : [],
      mentoringSessions: team ? [{ _id: `mock-student-session-${team.id}`, title: 'Customer validation review', meetingDate: new Date(Date.now() - 86_400_000).toISOString() }] : [],
      milestoneProgress: team ? { done: 1, total: 2, percentage: 50 } : { done: 0, total: 0, percentage: 0 },
      weeklyTasksSummary: team ? { pending: weekNumber % 3 + 1, completed: weekNumber % 2 + 1, overdue: weekNumber === 1 ? 1 : 0, nextDeadline: new Date(Date.now() + 3 * 86_400_000).toISOString() } : null,
    }, 'Student dashboard retrieved successfully.');
  });

  mock.onGet(/^\/tracking\/auth-stats(?:\?.*)?$/).reply((config) => {
    const queryDays = new URLSearchParams(config.url?.split('?')[1] || '').get('days');
    const days = [7, 30].includes(asNumber(queryDays, 7)) ? asNumber(queryDays, 7) : 7;
    const series = Array.from({ length: days }, (_, index) => {
      const date = new Date();
      date.setDate(date.getDate() - (days - index - 1));
      return date.toISOString().slice(0, 10);
    });
    return ok({ totalUsers: getMockState().users.length, totalRegisters: 12, totalLogins: 184, failedLogins: 3, todayRegisters: 2, todayLogins: 11, activeUsersToday: 9, loginRate: series.map((date, index) => ({ date, count: 4 + (index * 3) % 13 })), registerRate: series.map((date, index) => ({ date, count: index % 4 })) }, 'Authentication statistics retrieved successfully.');
  });

  mock.onGet('/tracking/online-users').reply(() => {
    const users = getMockState().users.filter((user) => user.lastSeen);
    const onlineUsers = users.slice(0, 4).map(({ id, name, email, avatar, role, lastSeen }) => ({ id, name, email, avatar, role, lastSeen }));
    const recentlyActive = users.slice(4, 12).map(({ id, name, email, avatar, role, lastSeen }) => ({ id, name, email, avatar, role, lastSeen }));
    return ok({ onlineCount: onlineUsers.length, totalUsers: getMockState().users.length, onlineUsers, recentlyActive }, 'Online users retrieved successfully.');
  });

  mock.onGet('/notifications').reply(() => ok([
    { _id: 'mock-notification-1', type: 'TEAM', title: 'Team proposal submitted', message: 'Nova Crew is ready for review.', isRead: false, link: '/admin/classes', createdAt: new Date().toISOString() },
    { _id: 'mock-notification-2', type: 'MENTORING', title: 'Mentor assigned', message: 'Phạm Anh Khoa was assigned to Phoenix Founders.', isRead: true, link: '/admin/classes', createdAt: new Date(Date.now() - 3_600_000).toISOString() },
  ], 'Notifications retrieved successfully.'));
  mock.onGet('/notifications/unread-count').reply(() => ok({ count: 1 }, 'Unread notification count retrieved successfully.'));
  mock.onPut(/^\/notifications\/[^/]+\/read$/).reply(() => ok(null, 'Notification marked as read.'));
  mock.onPut('/notifications/mark-all-read').reply(() => ok(null, 'All notifications marked as read.'));
}

export function registerCoreMockHandlers(mock: MockAdapter): void {
  registerAuthHandlers(mock);
  registerUserHandlers(mock);
  registerSubjectHandlers(mock);
  registerDashboardHandlers(mock);
}
