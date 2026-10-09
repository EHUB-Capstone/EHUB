import type MockAdapter from 'axios-mock-adapter';
import * as XLSX from 'xlsx';
import { ALL_TEAM_MAJOR_CODES } from '../../constants/majors.ts';
import type { AxiosRequestConfig } from 'axios';
import type { ChangePasswordPayload, LoginPayload, RegisterPayload } from '../../types/auth.ts';
import {
  normalizeLoginPayload,
  normalizeRegisterPayload,
  validateLoginPayload,
  validateRegisterPayload,
} from '../../utils/authValidation.ts';
import type { MockCurriculum, MockSemester, MockUser, MockSemesterStaffAssignment } from '../mockState.ts';
import type { MockReply } from '../mockHelpers.ts';
import {
  allocateId,
  allocateRowVersion,
  accepted,
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

const emptyCurriculum = (): MockCurriculum => ({ roadmapItems: [], rubrics: [], checkpoints: [], otherAssessments: [] });
function staffResponse(entry: MockSemesterStaffAssignment) {
  const state = getMockState();
  const user = state.users.find(x => x.id === entry.userId)!;
  const classes = state.classes.filter(x => x.semesterId === entry.semesterId && x.status !== 'Archived');
  const assignments = entry.role === 'LECTURER'
    ? classes.filter(x => x.primaryLecturerId === user.id).map(x => ({ _id: x.id, classCode: x.classCode, subjectCode: x.subjectCode }))
    : state.teams.filter(team => classes.some(x => x.id === team.classId) &&
      (team.currentMentorAssignments ?? (team.currentMentorAssignment ? [team.currentMentorAssignment] : []))
        .some(x => x.mentor.userId === user.id && x.status === 'Active' && !x.endedAtUtc))
      .map(team => { const cls = classes.find(x => x.id === team.classId)!; return { _id: team.id, classCode: cls.classCode, subjectCode: cls.subjectCode }; });
  return { _id: entry.id, userId: user.id, name: user.name, email: user.email, avatar: user.avatar, role: entry.role,
    status: entry.status === 'ACTIVE' ? 'Active' : 'Inactive', userStatus: user.status, isIncomplete: false,
    missingFields: [], rowVersion: entry.rowVersion ?? '0', classCount: assignments.length, assignments };
}
const checkpointDefinitionId = (courseId: string, checkpointNumber: number) =>
  `00000000-0000-4000-8000-${String(3000 + Number(courseId.slice(-3)) * 10 + checkpointNumber).padStart(12, '0')}`;
const checkpointEvaluationId = (teamId: string, checkpointNumber: number) =>
  `00000000-0000-4000-8000-${String(140000 + Number(teamId.slice(-3)) * 10 + checkpointNumber).padStart(12, '0')}`;

function isValidSemesterDateRange(semester: string, year: number, startDate: string, endDate: string): boolean {
  const startMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(startDate);
  const endMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(endDate);
  if (!startMatch || !endMatch || endDate <= startDate || Number(startMatch[1]) !== year) return false;

  const endYear = Number(endMatch[1]);
  const endMonth = Number(endMatch[2]);
  return endYear === year || (semester === 'FA' && endYear === year + 1 && endMonth === 1);
}

function formatSemesterDate(value: string): string {
  const [year, month, day] = value.split('-');
  return `${day}/${month}/${year}`;
}

function semesterOverlapMessage(semester: MockSemester): string {
  return `This date range overlaps with ${semester.semester} ${semester.year} (${formatSemesterDate(semester.startDate!)} – ${formatSemesterDate(semester.endDate!)}).`;
}

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

function userResponse(user: MockUser) {
  const state = getMockState();
  const viewer = state.users.find(item => item.id === state.sessionUserId);
  const currentSemester = state.semesters.find((semester) => semester.status === 'Active');
  const currentClasses = currentSemester
    ? state.classes.filter((cls) => cls.semesterId === currentSemester.id)
    : [];
  const currentClassIds = new Set(currentClasses.map((cls) => cls.id));
  const enrollments = Object.entries(state.rosters)
    .flatMap(([classId, roster]) => roster.map((student) => ({ classId, student })))
    .filter(({ classId, student }) => (
      currentClassIds.has(classId)
      && (user.role !== 'STUDENT' || viewer?.role !== 'LECTURER' ||
        state.classes.some(cls => cls.id === classId && cls.primaryLecturerId === viewer.id))
      &&
      (student.userId === user.id || student.studentId === user.id)
      && student.enrollmentStatus !== 'Dropped'
    ));
  const enrollment = enrollments[0];
  const assignedClass = enrollment ? state.classes.find((item) => item.id === enrollment.classId) : undefined;
  const classNames = new Set(enrollments
    .map(({ classId }) => state.classes.find((cls) => cls.id === classId)?.classCode)
    .filter((classCode): classCode is string => Boolean(classCode)));
  const groupNames = new Set(enrollments
    .map(({ student }) => student.teamName)
    .filter((teamName): teamName is string => Boolean(teamName)));

  if (user.role === 'LECTURER') {
    currentClasses
      .filter((cls) => cls.primaryLecturerId === user.id && cls.status !== 'Archived')
      .forEach((cls) => classNames.add(cls.classCode));
  }

  if (user.role === 'MENTOR') {
    state.teams
      .filter((team) => (
        currentClassIds.has(team.classId)
        && team.status === 'Active'
        && team.currentMentorAssignment?.status === 'Active'
        && team.currentMentorAssignment.mentor.userId === user.id
      ))
      .forEach((team) => {
        const cls = state.classes.find((item) => item.id === team.classId);
        if (cls) classNames.add(cls.classCode);
        groupNames.add(team.teamName);
      });
  }

  const isCurrentSemesterStaff = currentSemester
    ? state.semesterStaffAssignments.some((assignment) => (
      assignment.semesterId === currentSemester.id
      && assignment.userId === user.id
      && assignment.status === 'ACTIVE'
    ))
    : false;
  const hasCurrentSemesterContext = classNames.size > 0 || groupNames.size > 0 || isCurrentSemesterStaff;

  return {
    ...user,
    mentorProfileId: user.role === 'MENTOR' ? user.id : null,
    _id: user.id,
    fullName: user.name,
    rollNumber: user.studentId,
    majorCode: user.major,
    classId: assignedClass?.id || null,
    classCode: assignedClass?.classCode || null,
    teamId: enrollment?.student.teamId || null,
    teamName: enrollment?.student.teamName || null,
    semester: hasCurrentSemesterContext && currentSemester
      ? `${currentSemester.semester}${currentSemester.year}`
      : null,
    class: classNames.size ? [...classNames].sort().join(', ') : null,
    groupName: groupNames.size ? [...groupNames].sort().join(', ') : null,
  };
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
    const state = getMockState();
    const rosterStudents = Object.values(state.rosters)
      .flat()
      .filter((student) => student.email.toLowerCase() === email);
    let user = state.users.find((item) => item.email.toLowerCase() === email);
    if (!user) {
      const id = allocateId();
      user = { id, _id: id, email, name: rosterStudents[0]?.fullName || email, role: 'STUDENT', status: 'APPROVED',
        major: null, avatar: null, studentId: null, programGroup: null, phone: null,
        createdAt: new Date().toISOString(), lastSeen: null };
      state.users.push(user);
    }
    const statusFailure = accountStatusFailure(user);
    if (statusFailure) return statusFailure;

    if (user.role === 'STUDENT') {
      if (rosterStudents[0]?.fullName) user.name = rosterStudents[0].fullName;
      rosterStudents.forEach((student) => { student.userId = user.id; });
    }
    state.sessionUserId = user.id;
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

  mock.onPut('/auth/update-profile').reply((config) => {
    const state = getMockState();
    const user = state.users.find(item => item.id === state.sessionUserId);
    if (!user) return failure(401, 'COMMON_UNAUTHORIZED', 'Unauthorized access.');
    const statusFailure = accountStatusFailure(user);
    if (statusFailure) return statusFailure;
    const form = config.data;
    if (!(form instanceof FormData)) return failure(400, 'COMMON_VALIDATION_ERROR', 'Invalid profile form.');
    const name = String(form.get('fullName') ?? '').trim();
    const major = form.has('major') ? String(form.get('major')).trim().toUpperCase() : null;
    if (major !== null && user.role !== 'STUDENT') return failure(403, 'COMMON_FORBIDDEN', 'Forbidden access.');
    if (!name || name.length > 200 || (major !== null && !ALL_TEAM_MAJOR_CODES.includes(major)))
      return failure(400, 'COMMON_VALIDATION_ERROR', 'Enter a valid name and student major.');
    user.name = name;
    if (major !== null) user.major = major;
    persistMockState();
    return ok({ id: user.id, fullName: user.name, avatarUrl: user.avatar, majorCode: user.major }, 'Profile updated.');
  });

  mock.onPut('/auth/update-major').reply((config) => {
    const state = getMockState();
    const user = state.users.find(item => item.id === state.sessionUserId);
    if (!user) return failure(401, 'COMMON_UNAUTHORIZED', 'Unauthorized access.');
    if (user.role !== 'STUDENT') return failure(403, 'COMMON_FORBIDDEN', 'Forbidden access.');
    const majorCode = asString(parseBody(config).majorCode).trim().toUpperCase();
    if (!ALL_TEAM_MAJOR_CODES.includes(majorCode)) {
      return failure(400, 'AUTH_INVALID_MAJOR', 'Select a valid student major.');
    }

    const activeEnrollments = Object.entries(state.rosters).flatMap(([classId, roster]) => {
      const cls = state.classes.find(item => item.id === classId);
      if (!cls || !['Draft', 'Active', 'Inactive'].includes(cls.status)) return [];
      const enrollment = roster.find(item =>
        item.enrollmentStatus === 'Active' && (item.userId === user.id || item.studentId === user.id));
      return enrollment ? [{ cls, enrollment }] : [];
    });
    if (activeEnrollments.some(({ cls }) => cls.isEnrollmentMajorLocked)) {
      return failure(409, 'CLASS_ENROLLMENT_MAJOR_LOCKED', 'Your major is locked in an active class. Contact the assigned lecturer before changing it.');
    }

    user.major = majorCode;
    activeEnrollments.forEach(({ enrollment }) => {
      enrollment.majorCode = majorCode;
      enrollment.profileMajorCode = majorCode;
      enrollment.majorVerificationStatus = 'Unverified';
    });
    persistMockState();
    return ok({
      majorCode,
      updatedEnrollmentCount: activeEnrollments.length,
      updatedClassIds: activeEnrollments.map(({ cls }) => cls.id),
    }, 'Major updated successfully.');
  });

  mock.onPost('/auth/logout').reply(() => {
    getMockState().sessionUserId = null;
    persistMockState();
    return ok(null, 'Logout successfully');
  });

  mock.onPut('/auth/change-password').reply((config) => {
    const state = getMockState();
    const user = state.users.find((item) => item.id === state.sessionUserId);
    if (!user) return failure(401, 'COMMON_UNAUTHORIZED', 'Unauthorized access.');

    const body = parseBody(config);
    const payload: ChangePasswordPayload = {
      currentPassword: asString(body.currentPassword),
      newPassword: asString(body.newPassword),
      confirmPassword: asString(body.confirmPassword),
    };
    if (!payload.currentPassword || !payload.newPassword || !payload.confirmPassword) {
      return failure(400, 'COMMON_VALIDATION_ERROR', 'Validation failed');
    }
    if (payload.newPassword.length < 6 || payload.confirmPassword !== payload.newPassword) {
      return failure(400, 'COMMON_VALIDATION_ERROR', 'Validation failed');
    }

    const expectedPassword = state.authPasswords[user.id] ?? 'Mock123!';
    if (payload.currentPassword !== expectedPassword) {
      return failure(400, 'AUTH_CURRENT_PASSWORD_INVALID', 'Current password is incorrect.');
    }

    state.authPasswords[user.id] = payload.newPassword;
    persistMockState();
    return ok(null, 'Password changed successfully.');
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
      return failure(400, 'AUTH_REGISTRATION_FAILED', 'Unable to create account. Please try signing in or resetting your password.');
    }
    const role = payload.role.toUpperCase() as Exclude<MockUser['role'], 'ADMIN'>;
    const major = role === 'STUDENT' ? payload.majorCode ?? null : null;
    const now = Date.now();
    const registrationId = allocateId();
    getMockState().pendingRegistrations = getMockState().pendingRegistrations
      .filter((registration) => registration.email !== email);
    getMockState().pendingRegistrations.push({
      id: registrationId,
      fullName: payload.fullName,
      email,
      password: payload.password,
      role,
      major,
      otp: '123456',
      expiresAtUtc: new Date(now + 5 * 60_000).toISOString(),
      resendAvailableAtUtc: new Date(now + 60_000).toISOString(),
      failedAttempts: 0,
    });
    persistMockState();
    const message = 'Mock verification code sent. Use 123456.';
    return accepted({
      status: 'PendingEmailVerification',
      requiresEmailVerification: true,
      requiresApproval: false,
      message,
      registrationId,
      maskedEmail: email.replace(/^(.).+(@.+)$/, '$1***$2'),
      verificationExpiresAtUtc: new Date(now + 5 * 60_000).toISOString(),
      resendAvailableAtUtc: new Date(now + 60_000).toISOString(),
      user: null,
      accessToken: null,
      expiresAt: null,
    }, message);
  });

  mock.onPost('/auth/register/verify-otp').reply((config) => {
    const body = parseBody(config);
    const registrationId = asString(body.registrationId);
    const otp = asString(body.otp);
    if (!registrationId || !/^\d{6}$/.test(otp)) {
      return failure(400, 'COMMON_VALIDATION_ERROR', 'Validation failed');
    }

    const registration = getMockState().pendingRegistrations
      .find((item) => item.id === registrationId);
    if (!registration) {
      return failure(404, 'AUTH_REGISTRATION_NOT_FOUND', 'Registration is invalid or no longer available.');
    }
    if (Date.parse(registration.expiresAtUtc) <= Date.now()) {
      return failure(410, 'AUTH_VERIFICATION_CODE_EXPIRED', 'The verification code has expired. Request a new code.');
    }
    if (registration.otp !== otp) {
      registration.failedAttempts += 1;
      persistMockState();
      return registration.failedAttempts >= 5
        ? failure(429, 'AUTH_VERIFICATION_ATTEMPTS_EXCEEDED', 'Too many invalid verification attempts. Start a new registration.')
        : failure(400, 'AUTH_VERIFICATION_CODE_INVALID', 'The verification code is invalid.');
    }

    const id = allocateId();
    const user: MockUser = {
      id, _id: id, name: registration.fullName, email: registration.email, avatar: null,
      role: registration.role,
      status: registration.role === 'STUDENT' ? 'APPROVED' : 'PENDING',
      studentId: null, programGroup: registration.major?.split('_')[0] ?? null,
      major: registration.major,
      phone: null, createdAt: new Date().toISOString(), lastSeen: null,
    };
    getMockState().users.unshift(user);
    getMockState().authPasswords[user.id] = registration.password;
    getMockState().pendingRegistrations = getMockState().pendingRegistrations
      .filter((item) => item.id !== registrationId);
    if (user.status === 'PENDING') {
      const roleLabel = user.role === 'LECTURER' ? 'Lecturer' : 'Mentor';
      getMockState().users
        .filter((candidate) => candidate.role === 'ADMIN' && candidate.status === 'APPROVED')
        .forEach((administrator) => {
          getMockState().notifications.unshift({
            id: allocateId(),
            recipientUserId: administrator.id,
            type: 'AccountApprovalRequested',
            title: `${roleLabel} account awaiting approval`,
            message: `${user.name} registered as a ${roleLabel} and is ready for review.`,
            link: '/admin/account-approvals',
            data: { userId: user.id, fullName: user.name, role: roleLabel },
            isRead: false,
            readAt: null,
            createdAt: user.createdAt,
          });
        });
    }
    if (user.status === 'APPROVED') getMockState().sessionUserId = user.id;
    persistMockState();
    const session = user.status === 'APPROVED' ? authResponse(user) : null;
    const message = user.status === 'APPROVED'
      ? 'Your email has been verified and your account is ready.'
      : 'Your email has been verified. Your account is pending admin approval.';
    return ok({
      status: user.status === 'APPROVED' ? 'Active' : 'PendingApproval',
      requiresEmailVerification: false,
      requiresApproval: user.status !== 'APPROVED',
      message,
      registrationId: null,
      maskedEmail: null,
      verificationExpiresAtUtc: null,
      resendAvailableAtUtc: null,
      user: authUser(user),
      accessToken: session?.accessToken ?? null,
      expiresAt: session?.expiresAt ?? null,
    }, message);
  });

  mock.onPost('/auth/register/resend-otp').reply((config) => {
    const registrationId = asString(parseBody(config).registrationId);
    const registration = getMockState().pendingRegistrations
      .find((item) => item.id === registrationId);
    if (!registration) {
      return failure(404, 'AUTH_REGISTRATION_NOT_FOUND', 'Registration is invalid or no longer available.');
    }
    if (Date.parse(registration.resendAvailableAtUtc) > Date.now()) {
      return failure(429, 'AUTH_VERIFICATION_RESEND_TOO_SOON', 'Please wait before requesting another verification code.');
    }

    const now = Date.now();
    registration.otp = '123456';
    registration.expiresAtUtc = new Date(now + 5 * 60_000).toISOString();
    registration.resendAvailableAtUtc = new Date(now + 60_000).toISOString();
    persistMockState();
    const message = 'Mock verification code resent. Use 123456.';
    return accepted({
      status: 'PendingEmailVerification',
      requiresEmailVerification: true,
      requiresApproval: false,
      message,
      registrationId,
      maskedEmail: registration.email.replace(/^(.).+(@.+)$/, '$1***$2'),
      verificationExpiresAtUtc: registration.expiresAtUtc,
      resendAvailableAtUtc: registration.resendAvailableAtUtc,
      user: null,
      accessToken: null,
      expiresAt: null,
    }, message);
  });

  mock.onPost('/auth/forgot-password').reply(() => ok(null, 'If the email exists, a reset link has been sent.'));
  mock.onPost('/auth/reset-password').reply(() => ok(null, 'Password reset successfully.'));
}

function registerUserHandlers(mock: MockAdapter): void {
  mock.onGet('/admin/users/pending-approval').reply(() => {
    const users = getMockState().users
      .filter((user) => (
        user.status === 'PENDING'
        && (user.role === 'LECTURER' || user.role === 'MENTOR')
      ))
      .map((user) => ({
        id: user.id,
        fullName: user.name,
        email: user.email,
        roles: [backendRole(user.role)],
        status: backendStatus[user.status],
        createdAt: user.createdAt,
      }));

    return ok(users, 'Pending approval users retrieved successfully.');
  });

  mock.onGet('/users').reply((config) => {
    const state = getMockState();
    const viewer = state.users.find(item => item.id === state.sessionUserId);
    if (!viewer) return failure(401, 'UNAUTHORIZED', 'Authentication is required.');
    if (!['ADMIN', 'LECTURER'].includes(viewer.role)) return failure(403, 'COMMON_FORBIDDEN', 'Staff access is required.');
    const params = requestParams(config);
    const query = asString(params.search).trim().toLowerCase();
    const role = asString(params.role).toUpperCase();
    const status = asString(params.status).toUpperCase();
    const mentorType = asString(params.mentorType);
    const page = Math.max(1, asNumber(params.page, 1));
    const limit = Math.min(200, Math.max(1, asNumber(params.limit, 10)));
    let users = state.users.filter((user) =>
      (viewer.role === 'ADMIN' || user.id === viewer.id || user.role !== 'STUDENT' ||
        state.classes.some(cls => cls.primaryLecturerId === viewer.id &&
          state.rosters[cls.id]?.some(student => student.userId === user.id && student.enrollmentStatus !== 'Dropped')))
      &&
      (!role || role === 'ALL' || user.role === role)
      && (!status || status === 'ALL' || user.status === status)
      && (!mentorType || mentorType === 'ALL' || user.mentorType === mentorType)
      && (!query || [user.name, user.email, user.studentId].some((value) => value?.toLowerCase().includes(query))));
    const total = users.length;
    users = users.slice((page - 1) * limit, page * limit);
    return ok({
      users: users.map(userResponse),
      pagination: { total, page, limit, pages: Math.max(1, Math.ceil(total / limit)) },
    }, 'Users retrieved successfully.');
  });

  function mentorProfileResponse(user: MockUser) {
    const profile = user.mentorProfile ?? {
      status: 'Active' as const, expertise: [], startupDomains: [], technologySkills: [], mentorTags: [], bio: null, availabilityNote: null, organization: null, department: null,
      jobTitle: null, contractType: null, educationLevel: null, currentAddress: null, linkedInUrl: null, fptEmail: null,
      dateOfBirth: null, version: 1,
    };
    const { version, ...fields } = profile;
    const teams = getMockState().teams ?? [];
    return {
      id: user.id, userId: user.id, fullName: user.name, email: user.email, phone: user.phone, avatarUrl: user.avatar,
      mentorType: user.mentorType ?? 'Enterprise', ...fields,
      activeTeamCount: teams.filter((team) => team.currentMentorAssignments?.some((assignment) => assignment.mentor.userId === user.id && assignment.status === 'Active')).length,
      rowVersion: String(version),
    };
  }

  mock.onGet(/^\/admin\/mentor-profiles\/[^/]+$/).reply((config) => {
    const state = getMockState();
    const viewer = state.users.find((item) => item.id === state.sessionUserId);
    if (!viewer) return failure(401, 'UNAUTHORIZED', 'Authentication is required.');
    if (viewer.role !== 'ADMIN') return failure(403, 'COMMON_FORBIDDEN', 'Only an administrator can manage mentor profiles.');
    const user = state.users.find((item) => item.id === routeId(config, /^\/admin\/mentor-profiles\/([^/]+)$/) && item.role === 'MENTOR');
    return user ? ok(mentorProfileResponse(user), 'Mentor profile retrieved successfully.') : failure(404, 'MENTOR_PROFILE_NOT_FOUND', 'The mentor profile was not found.');
  });

  mock.onPut(/^\/admin\/mentor-profiles\/[^/]+$/).reply((config) => {
    const state = getMockState();
    const viewer = state.users.find((item) => item.id === state.sessionUserId);
    if (!viewer) return failure(401, 'UNAUTHORIZED', 'Authentication is required.');
    if (viewer.role !== 'ADMIN') return failure(403, 'COMMON_FORBIDDEN', 'Only an administrator can manage mentor profiles.');
    const user = state.users.find((item) => item.id === routeId(config, /^\/admin\/mentor-profiles\/([^/]+)$/) && item.role === 'MENTOR');
    if (!user) return failure(404, 'MENTOR_PROFILE_NOT_FOUND', 'The mentor profile was not found.');
    const body = parseBody(config);
    const current = mentorProfileResponse(user);
    if (asString(body.rowVersion) !== current.rowVersion) {
      return failure(409, 'MENTOR_PROFILE_CONFLICT', 'The mentor profile was changed by someone else. Reload it and try again.');
    }
    if (!['Active', 'Inactive', 'Unavailable'].includes(asString(body.status))) return failure(400, 'COMMON_VALIDATION_ERROR', 'Status must be Active, Inactive or Unavailable.');
    const readTags = (key: string, label: string): { tags: string[] } | { error: MockReply } => {
      const tags = (Array.isArray(body[key]) ? (body[key] as unknown[]).map((tag) => asString(tag).trim().replace(/\s+/g, ' ')) : []).filter(Boolean);
      if (new Set(tags.map((tag) => tag.toLowerCase())).size !== tags.length) return { error: failure(400, 'COMMON_VALIDATION_ERROR', `${label} is listed more than once.`) };
      if (tags.length > 20 || tags.some((tag) => tag.length < 2 || tag.length > 50)) return { error: failure(400, 'COMMON_VALIDATION_ERROR', `${label} tags must be 2 to 50 characters, up to 20.`) };
      return { tags };
    };
    const expertise = readTags('expertise', 'An expertise');
    const startupDomains = readTags('startupDomains', 'A startup domain');
    const technologySkills = readTags('technologySkills', 'A technology skill');
    const mentorTags = readTags('mentorTags', 'A mentor tag');
    for (const result of [expertise, startupDomains, technologySkills, mentorTags]) if ('error' in result) return result.error;
    if (asString(body.bio).length > 2000) return failure(400, 'COMMON_VALIDATION_ERROR', 'Background may contain at most 2000 characters.');
    const text = (key: string) => asString(body[key]).trim() || null;
    user.mentorProfile = {
      status: asString(body.status) as 'Active' | 'Inactive' | 'Unavailable',
      expertise: (expertise as { tags: string[] }).tags, startupDomains: (startupDomains as { tags: string[] }).tags, technologySkills: (technologySkills as { tags: string[] }).tags, mentorTags: (mentorTags as { tags: string[] }).tags, bio: text('bio'), availabilityNote: text('availabilityNote'), organization: text('organization'),
      department: text('department'), jobTitle: text('jobTitle'), contractType: text('contractType'),
      educationLevel: text('educationLevel'), currentAddress: text('currentAddress'), linkedInUrl: text('linkedInUrl'),
      fptEmail: text('fptEmail'), dateOfBirth: text('dateOfBirth'), version: Number(current.rowVersion) + 1,
    };
    persistMockState();
    return ok(mentorProfileResponse(user), 'Mentor profile updated successfully.');
  });

  mock.onPost(/^\/admin\/users\/[^/]+\/(approve|reject)$/).reply((config) => {
    const match = config.url?.match(/^\/admin\/users\/([^/]+)\/(approve|reject)$/);
    const user = getMockState().users.find((item) => item.id === match?.[1]);
    if (!user) return failure(404, 'USER_NOT_FOUND', 'User was not found.');
    if (user.status !== 'PENDING') {
      return failure(409, 'APPROVAL_USER_NOT_PENDING', 'The user is not pending approval.');
    }
    if (user.role !== 'LECTURER' && user.role !== 'MENTOR') {
      return failure(400, 'APPROVAL_INVALID_TARGET_ROLE', 'Only Lecturer or Mentor accounts can be approved or rejected.');
    }

    user.status = match?.[2] === 'approve' ? 'APPROVED' : 'REJECTED';
    persistMockState();
    return ok(null, match?.[2] === 'approve' ? 'User approved successfully.' : 'User rejected successfully.');
  });

  mock.onGet(/^\/users\/[^/]+$/).reply((config) => {
    const state = getMockState();
    const viewer = state.users.find(item => item.id === state.sessionUserId);
    if (!viewer) return failure(401, 'UNAUTHORIZED', 'Authentication is required.');
    if (!['ADMIN', 'LECTURER'].includes(viewer.role)) return failure(403, 'COMMON_FORBIDDEN', 'Staff access is required.');
    const user = state.users.find((item) => item.id === routeId(config, /^\/users\/([^/]+)$/));
    if (viewer.role !== 'ADMIN' && (!user || user.role === 'STUDENT' &&
      !state.classes.some(cls => cls.primaryLecturerId === viewer.id &&
        state.rosters[cls.id]?.some(student => student.userId === user.id && student.enrollmentStatus !== 'Dropped'))))
      return failure(403, 'COMMON_FORBIDDEN', 'The user is unavailable or outside your class scope.');
    return user ? ok(userResponse(user), 'User retrieved successfully.') : failure(404, 'USER_NOT_FOUND', 'User not found.');
  });

  mock.onPost('/users/import-lecturers/preview').reply(() => {
    const email = 'imported.lecturer@example.org';
    const exists = getMockState().users.some((user) => user.email.toLowerCase() === email);
    return ok({
      sessionId: exists ? '' : '00000000-0000-0000-0000-000000000104',
      totalRows: 1,
      readyCount: exists ? 0 : 1,
      willActivateCount: 0,
      existingCount: exists ? 1 : 0,
      errorCount: 0,
      canCommit: !exists,
      rows: [{
        rowNumber: 2,
        fullName: 'Imported Lecturer',
        position: 'Lecturer',
        contactEmail: null,
        googleEmail: email,
        status: exists ? 'AlreadyExists' : 'Ready',
        isValid: true,
        message: exists ? 'An active Lecturer account already exists; this row will be skipped.' : null,
      }],
    }, 'Lecturer import preview generated successfully.');
  });

  mock.onPost('/users/import-lecturers/commit').reply((config) => {
    const sessionId = asString(parseBody(config).sessionId);
    if (sessionId !== '00000000-0000-0000-0000-000000000104') {
      return failure(409, 'LECTURER_IMPORT_SESSION_INVALID', 'The lecturer import session is invalid.');
    }

    const email = 'imported.lecturer@example.org';
    if (getMockState().users.some((user) => user.email.toLowerCase() === email)) {
      return ok({ createdCount: 0, activatedCount: 0, skippedCount: 1, errorCount: 0, errors: [] }, 'Lecturer accounts imported successfully.');
    }

    const id = allocateId();
    getMockState().users.unshift({
      id,
      _id: id,
      name: 'Imported Lecturer',
      email,
      avatar: null,
      role: 'LECTURER',
      status: 'APPROVED',
      studentId: null,
      programGroup: null,
      major: null,
      phone: null,
      createdAt: new Date().toISOString(),
      lastSeen: null,
    });
    persistMockState();
    return ok({ createdCount: 1, activatedCount: 0, skippedCount: 0, errorCount: 0, errors: [] }, 'Lecturer accounts imported successfully.');
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
      ...(role === 'MENTOR' ? {
        mentorType: asString(body.mentorType) === 'Academic' ? 'Academic' as const : 'Enterprise' as const,
        mentorProfile: {
          status: 'Active' as const,
          expertise: Array.isArray(body.expertise) ? body.expertise.map(String) : [],
          startupDomains: [], technologySkills: [], mentorTags: [],
          bio: asString(body.bio).trim() || null,
          availabilityNote: asString(body.availabilityNote).trim() || null,
          organization: null, department: null, jobTitle: null, contractType: null, educationLevel: null,
          currentAddress: null, linkedInUrl: null, fptEmail: null, dateOfBirth: null, version: 1,
        },
      } : {}),
      phone: asString(body.phone) || null,
      createdAt: new Date().toISOString(),
      lastSeen: null,
    };
    if (role === 'MENTOR' && !['Enterprise', 'Academic'].includes(asString(body.mentorType))) {
      return failure(400, 'VALIDATION_ERROR', 'Mentor type must be Enterprise or Academic.');
    }
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

function semesterCompletionPreview(semesterId: string) {
  const state = getMockState();
  const semester = state.semesters.find((item) => item.id === semesterId);
  if (!semester) return null;
  const classes = state.classes.filter((item) => item.semesterId === semesterId);
  const countStatus = (status: string) => classes.filter((item) => item.status === status).length;
  const activeEnrollmentCount = classes.reduce((count, cls) =>
    count + (state.rosters[cls.id] || []).filter((student) => student.enrollmentStatus === 'Active').length, 0);
  const blockingClasses = classes
    .map(cls => ({
      classId: cls.id,
      classCode: cls.classCode,
      slug: cls.slug,
      status: cls.status,
      activeEnrollmentCount: (state.rosters[cls.id] || [])
        .filter(student => student.enrollmentStatus === 'Active').length,
    }))
    .filter(item => ['Draft', 'Active', 'Inactive'].includes(item.status) || item.activeEnrollmentCount > 0);
  const draftClassCount = countStatus('Draft');
  const activeClassCount = countStatus('Active');
  const inactiveClassCount = countStatus('Inactive');
  const unfinishedClassCount = draftClassCount + activeClassCount + inactiveClassCount;
  const blockers: string[] = [];
  if (unfinishedClassCount)
    blockers.push(`Complete or archive the remaining ${unfinishedClassCount} non-completed class(es).`);
  if (activeEnrollmentCount)
    blockers.push(`Resolve ${activeEnrollmentCount} active enrollment(s).`);

  return {
    semesterId: semester.id,
    semester: semester.semester,
    year: semester.year,
    status: semester.status,
    draftClassCount,
    activeClassCount,
    inactiveClassCount,
    completedClassCount: countStatus('Completed'),
    archivedClassCount: countStatus('Archived'),
    activeEnrollmentCount,
    processingImportSessionCount: 0,
    blockers,
    blockingClasses,
    rowVersion: semester.rowVersion,
  };
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
    const state = getMockState();
    const currentSemester = state.semesters.find((item) => item.status === 'Active') || null;
    const availableYears = [...new Set([new Date().getFullYear(), ...state.semesters.map((item) => item.year)])]
      .sort((left, right) => right - left);
    return ok({ currentSemester, availableYears, isDecember: new Date().getMonth() === 11 }, 'Current semester retrieved successfully.');
  });

  mock.onGet('/subjects/semesters/class-creation-options').reply(() => {
    const today = new Date().toISOString().slice(0, 10);
    const candidates = [...getMockState().semesters]
      .filter(item =>
        item.status === 'Active'
          ? !item.endDate || item.endDate >= today
          : item.status === 'Planned' && Boolean(item.startDate && item.startDate > today) && (!item.endDate || item.endDate >= today))
      .sort((left, right) => (left.startDate || `${left.year}-01-01`).localeCompare(right.startDate || `${right.year}-01-01`));
    const active = candidates.find(item => item.status === 'Active');
    const planned = candidates.find(item => item.status === 'Planned' && (!active || (item.startDate || '') > (active.startDate || '')));
    const semesters = [active, planned].filter(Boolean).map(item => ({
      ...item!,
      availability: item!.status === 'Active' ? 'Current' : 'Next',
    }));
    return ok({ semesters }, 'Class creation semester options retrieved successfully.');
  });

  mock.onPost('/subjects/current-semester').reply((config) => {
    const body = parseBody(config);
    const semesterCode = asString(body.semester, 'FA').toUpperCase();
    const year = asNumber(body.year, new Date().getFullYear());
    if (!['SP', 'SU', 'FA'].includes(semesterCode))
      return failure(400, 'CLASS_VALIDATION_ERROR', 'Semester must be SP, SU, or FA.');
    const state = getMockState();
    const active = state.semesters.find((item) => item.status === 'Active');
    const semester = state.semesters.find((item) => item.semester === semesterCode && item.year === year);
    if (semester?.status === 'Active')
      return ok({ currentSemester: semester, availableYears: [...new Set(state.semesters.map((item) => item.year))], isDecember: false }, 'Active semester updated successfully.');
    if (active)
      return failure(409, 'SEMESTER_ACTIVATION_BLOCKED', `Complete ${active.semester} ${active.year} before activating another semester.`);
    if (semester?.status === 'Completed' || semester?.status === 'Archived')
      return failure(409, 'SEMESTER_INVALID_STATE', 'A completed or archived semester must be explicitly reopened before activation.');

    if (!semester) return failure(404, 'SEMESTER_NOT_FOUND', 'Plan the semester and configure its dates before activation.');
    if (!semester.startDate || !semester.endDate)
      return failure(409, 'SEMESTER_ACTIVATION_BLOCKED', 'Configure semester start and end dates before activation.');
    const today = new Date().toISOString().slice(0, 10);
    if (today < semester.startDate || today > semester.endDate)
      return failure(409, 'SEMESTER_ACTIVATION_BLOCKED', 'A semester can only be activated between its configured start and end dates.');
    semester.status = 'Active';
    semester.rowVersion = allocateRowVersion();
    state.currentSemester = { semester: semester.semester, year: semester.year };
    persistMockState();
    return ok({ currentSemester: semester, availableYears: [...new Set(state.semesters.map((item) => item.year))], isDecember: false }, 'Active semester updated successfully.');
  });

  mock.onPost('/subjects/current-semester/transition').reply((config) => {
    const body = parseBody(config);
    const state = getMockState();
    const current = state.semesters.find(item => item.id === asString(body.currentSemesterId));
    const target = state.semesters.find(item => item.id === asString(body.targetSemesterId));
    const reason = asString(body.reason).trim();
    if (reason.length < 3 || reason.length > 500)
      return failure(400, 'CLASS_VALIDATION_ERROR', 'Reason must contain between 3 and 500 characters.');
    if (!current || !target)
      return failure(404, 'SEMESTER_NOT_FOUND', 'The current or target semester was not found.');
    if (asString(body.currentRowVersion) !== current.rowVersion || asString(body.targetRowVersion) !== target.rowVersion)
      return failure(409, 'SEMESTER_CONCURRENCY_CONFLICT', 'A semester changed concurrently. Reload and try again.');
    if (current.status !== 'Active' || target.status !== 'Planned')
      return failure(409, 'SEMESTER_INVALID_STATE', 'The semester transition is no longer valid.');
    const today = new Date().toISOString().slice(0, 10);
    if (!current.endDate || today <= current.endDate)
      return failure(409, 'SEMESTER_ACTIVATION_BLOCKED', 'The current semester can move to Closing only after its configured end date.');
    if (!target.startDate || !target.endDate || today < target.startDate || today > target.endDate)
      return failure(409, 'SEMESTER_ACTIVATION_BLOCKED', 'The target semester can become active only between its configured start and end dates.');
    current.status = 'Closing';
    current.rowVersion = allocateRowVersion();
    target.status = 'Active';
    target.rowVersion = allocateRowVersion();
    state.currentSemester = { semester: target.semester, year: target.year };
    persistMockState();
    return ok({
      currentSemester: target,
      availableYears: [...new Set(state.semesters.map(item => item.year))],
      isDecember: new Date().getMonth() === 11,
    }, 'Semester transition completed successfully.');
  });

  mock.onPost('/subjects/semesters').reply((config) => {
    const body = parseBody(config);
    const semesterCode = asString(body.semester).toUpperCase() as 'SP' | 'SU' | 'FA';
    const year = asNumber(body.year, new Date().getFullYear());
    const startDate = asString(body.startDate);
    const endDate = asString(body.endDate);
    const state = getMockState();
    const currentYear = new Date().getFullYear();
    if (!['SP', 'SU', 'FA'].includes(semesterCode)
      || year < currentYear
      || year > currentYear + 2
      || endDate < new Date().toISOString().slice(0, 10))
      return failure(400, 'CLASS_VALIDATION_ERROR', 'Provide a valid semester and date range.');
    if (endDate <= startDate)
      return failure(400, 'SEMESTER_DATE_INVALID', 'End date must be after the start date.');
    if (!isValidSemesterDateRange(semesterCode, year, startDate, endDate))
      return failure(400, 'SEMESTER_DATE_INVALID', 'Start date and end date do not follow the selected semester year rules.');
    if (state.semesters.some((item) => item.semester === semesterCode && item.year === year))
      return failure(409, 'SEMESTER_ALREADY_PLANNED', `${semesterCode} ${year} has already been planned. You can edit its dates instead.`);
    const overlappingSemester = state.semesters.find((item) => item.startDate && item.endDate && item.startDate <= endDate && item.endDate >= startDate);
    if (overlappingSemester)
      return failure(409, 'SEMESTER_DATE_OVERLAP', semesterOverlapMessage(overlappingSemester));
    const semester = {
      id: allocateId(), semester: semesterCode, year, status: 'Planned' as const,
      startDate, endDate, completedAtUtc: null, completionReason: null,
      rowVersion: allocateRowVersion(),
    };
    state.semesters.push(semester);
    persistMockState();
    return created(semester, 'Semester planned successfully.');
  });

  mock.onPut(/^\/subjects\/semesters\/[^/]+\/dates$/).reply((config) => {
    const semesterId = routeId(config, /^\/subjects\/semesters\/([^/]+)\/dates$/);
    const semester = getMockState().semesters.find((item) => item.id === semesterId);
    if (!semester) return failure(404, 'SEMESTER_NOT_FOUND', 'The requested semester was not found.');
    if (semester.status === 'Completed' || semester.status === 'Archived')
      return failure(409, 'SEMESTER_INVALID_STATE', 'Dates of a completed or archived semester cannot be changed.');
    const body = parseBody(config);
    if (asString(body.rowVersion) !== semester.rowVersion)
      return failure(409, 'SEMESTER_CONCURRENCY_CONFLICT', 'The semester changed concurrently. Reload and try again.');
    const startDate = asString(body.startDate);
    const endDate = asString(body.endDate);
    if (endDate <= startDate)
      return failure(400, 'SEMESTER_DATE_INVALID', 'End date must be after the start date.');
    if (!isValidSemesterDateRange(semester.semester, semester.year, startDate, endDate))
      return failure(400, 'SEMESTER_DATE_INVALID', 'Start date and end date do not follow the selected semester year rules.');
    if (asString(body.reason).trim().length < 3)
      return failure(400, 'CLASS_VALIDATION_ERROR', 'Provide a valid rowVersion and reason.');
    const overlappingSemester = getMockState().semesters.find((item) => item.id !== semester.id && item.startDate && item.endDate && item.startDate <= endDate && item.endDate >= startDate);
    if (overlappingSemester)
      return failure(409, 'SEMESTER_DATE_OVERLAP', semesterOverlapMessage(overlappingSemester));
    semester.startDate = startDate;
    semester.endDate = endDate;
    semester.rowVersion = allocateRowVersion();
    persistMockState();
    return ok(semester, 'Semester dates updated successfully.');
  });

  mock.onGet('/subjects/semesters').reply(() => {
    const semesters = [...getMockState().semesters]
      .sort((left, right) => right.year - left.year || right.semester.localeCompare(left.semester));
    return ok({ semesters }, 'Semesters retrieved successfully.');
  });

  mock.onGet(/^\/subjects\/semesters\/[^/]+\/completion-preview$/).reply((config) => {
    const semesterId = routeId(config, /^\/subjects\/semesters\/([^/]+)\/completion-preview$/);
    const preview = semesterCompletionPreview(semesterId);
    return preview
      ? ok(preview, 'Semester completion preview generated successfully.')
      : failure(404, 'SEMESTER_NOT_FOUND', 'The requested semester was not found.');
  });

  mock.onPost(/^\/subjects\/semesters\/[^/]+\/(complete|reopen)$/).reply((config) => {
    const match = config.url?.match(/^\/subjects\/semesters\/([^/]+)\/(complete|reopen)$/);
    const semester = getMockState().semesters.find((item) => item.id === match?.[1]);
    if (!semester) return failure(404, 'SEMESTER_NOT_FOUND', 'The requested semester was not found.');
    const reopen = match?.[2] === 'reopen';
    const body = parseBody(config);
    const reason = asString(body.reason).trim();
    if (reason.length < 3 || reason.length > 500)
      return failure(400, 'CLASS_VALIDATION_ERROR', 'Reason must contain between 3 and 500 characters.');
    if (reopen && semester.status === 'Active' || !reopen && semester.status === 'Completed')
      return ok(semester, `Semester ${reopen ? 'reopened' : 'completed'} successfully.`);
    if (asString(body.rowVersion) !== semester.rowVersion)
      return failure(409, 'SEMESTER_CONCURRENCY_CONFLICT', 'The semester changed concurrently. Reload and try again.');

    const state = getMockState();
    if (reopen) {
      if (semester.status !== 'Completed')
        return failure(409, 'SEMESTER_INVALID_STATE', 'Only a completed semester can be reopened.');
      if (state.semesters.some((item) => item.id !== semester.id && item.status === 'Active'))
        return failure(409, 'SEMESTER_ACTIVATION_BLOCKED', 'Complete the active semester before reopening this semester.');
      semester.status = 'Active';
      semester.completedAtUtc = null;
      semester.completionReason = null;
      state.currentSemester = { semester: semester.semester, year: semester.year };
    } else {
      if (semester.status !== 'Active' && semester.status !== 'Closing')
        return failure(409, 'SEMESTER_INVALID_STATE', 'Only an active or closing semester can be completed.');
      const preview = semesterCompletionPreview(semester.id)!;
      if (preview.blockers.length)
        return failure(409, 'SEMESTER_COMPLETION_BLOCKED', preview.blockers.join(' '));
      const wasActive = semester.status === 'Active';
      semester.status = 'Completed';
      semester.completedAtUtc = new Date().toISOString();
      semester.completionReason = reason;
      if (wasActive) state.currentSemester = null;
    }
    semester.rowVersion = allocateRowVersion();
    persistMockState();
    return ok(semester, `Semester ${reopen ? 'reopened' : 'completed'} successfully.`);
  });

  // The mock staff list is derived from every lecturer and mentor user, so these only keep the contract in sync;
  // they do not model per-semester membership.
  mock.onGet('/subjects/teaching-staff/candidates').reply(() => {
    const candidates = getMockState().users
      .filter((user) => (user.role === 'LECTURER' || user.role === 'MENTOR') && user.status === 'APPROVED')
      .map((user) => ({ userId: user.id, name: user.name, email: user.email, avatar: user.avatar, role: user.role, mentorType: null, contractType: null,
        tags: user.role === 'MENTOR' && user.mentorProfile ? { expertise: user.mentorProfile.expertise, startupDomains: user.mentorProfile.startupDomains, technologySkills: user.mentorProfile.technologySkills, mentorTags: user.mentorProfile.mentorTags } : null }));
    return ok({ candidates }, 'Teaching staff candidates retrieved successfully.');
  });

  mock.onPost('/subjects/teaching-staff/batch').reply((config) => {
    const body = parseBody(config);
    const role = asString(body.role).toUpperCase();
    const userIds = Array.isArray(body.userIds) ? [...new Set(body.userIds.map(String))] : [];
    if (!['LECTURER', 'MENTOR'].includes(role) || userIds.length === 0) {
      return failure(400, 'VALIDATION_ERROR', 'A valid role and at least one staff member are required.');
    }
    const results = userIds.map((userId) => {
      const user = getMockState().users.find((item) => item.id === userId);
      return user && user.status === 'APPROVED' && user.role === role
        ? { userId, outcome: 'Added', message: null }
        : { userId, outcome: 'Rejected', message: `The selected user is inactive or does not have ${role} role.` };
    });
    const addedCount = results.filter((item) => item.outcome === 'Added').length;
    return ok({ results, addedCount, alreadyInListCount: 0, rejectedCount: results.length - addedCount }, 'Teaching staff batch processed successfully.');
  });

  mock.onGet('/subjects/teaching-staff').reply(config => {
    const state = getMockState();
    const viewer = state.users.find(x => x.id === state.sessionUserId);
    if (!viewer) return failure(401, 'COMMON_UNAUTHORIZED', 'Authentication required.');
    if (!['ADMIN', 'LECTURER'].includes(viewer.role)) return failure(403, 'CLASS_ACCESS_DENIED', 'Staff role required.');
    const params = requestParams(config);
    const semester = state.semesters.find(x => x.semester === asString(params.semester) && x.year === asNumber(params.year, 0));
    if (!semester) return failure(404, 'SEMESTER_NOT_FOUND', 'Semester was not found.');
    const staff = state.semesterStaffAssignments.filter(x => x.semesterId === semester.id && state.users.some(u => u.id === x.userId)).map(staffResponse);
    const lecturers = staff.filter((item) => item.role === 'LECTURER').length;
    const mentors = staff.filter((item) => item.role === 'MENTOR').length;
    const assigned = staff.filter((item) => item.assignments.length > 0).length;
    return ok({ staff, summary: { lecturers, mentors, assigned, unassigned: staff.length - assigned, classes: getMockState().classes.filter((cls) => cls.status !== 'Archived').length } }, 'Teaching staff retrieved successfully.');
  });

  mock.onPost('/subjects/teaching-staff').reply(config => {
    const state = getMockState();
    const viewer = state.users.find(x => x.id === state.sessionUserId);
    if (!viewer) return failure(401, 'COMMON_UNAUTHORIZED', 'Authentication required.');
    if (viewer.role !== 'ADMIN') return failure(403, 'CLASS_ACCESS_DENIED', 'Admin role required.');
    const body = parseBody(config);
    const semester = state.semesters.find(x => x.semester === asString(body.semester) && x.year === asNumber(body.year, 0));
    const user = state.users.find(x => x.id === asString(body.userId));
    if (!semester || !['Active', 'Planned'].includes(semester.status)) return failure(400, 'SEMESTER_INVALID_STATE', 'This semester cannot be changed.');
    if (!user || user.status !== 'APPROVED' || user.role !== body.role || !['MENTOR', 'LECTURER'].includes(user.role))
      return failure(400, 'SEMESTER_STAFF_CONFLICT', 'An active user with the requested role is required.');
    if (state.semesterStaffAssignments.some(x => x.semesterId === semester.id && x.userId === user.id && x.role === user.role))
      return failure(400, 'SEMESTER_STAFF_CONFLICT', 'This user already has a semester entry.');
    const entry: MockSemesterStaffAssignment = { id: allocateId(), semesterId: semester.id, userId: user.id, role: user.role as 'MENTOR' | 'LECTURER', status: 'ACTIVE', rowVersion: allocateRowVersion() };
    state.semesterStaffAssignments.push(entry); persistMockState();
    return ok(staffResponse(entry));
  });

  mock.onPut(/^\/subjects\/teaching-staff\/[^/]+$/).reply(config => {
    const state = getMockState();
    const viewer = state.users.find(x => x.id === state.sessionUserId);
    if (!viewer) return failure(401, 'COMMON_UNAUTHORIZED', 'Authentication required.');
    if (viewer.role !== 'ADMIN') return failure(403, 'CLASS_ACCESS_DENIED', 'Admin role required.');
    const entry = state.semesterStaffAssignments.find(x => x.id === routeId(config, /^\/subjects\/teaching-staff\/([^/]+)$/));
    if (!entry) return failure(404, 'SEMESTER_STAFF_NOT_FOUND', 'Entry was not found.');
    const semester = state.semesters.find(x => x.id === entry.semesterId);
    if (!semester || !['Active', 'Planned'].includes(semester.status)) return failure(400, 'SEMESTER_INVALID_STATE', 'This semester cannot be changed.');
    const body = parseBody(config);
    if (body.rowVersion !== (entry.rowVersion ?? '0')) return failure(409, 'SEMESTER_CONCURRENCY_CONFLICT', 'Availability changed. Reload and try again.');
    if (body.status !== 'Active' && body.status !== 'Inactive') return failure(400, 'CLASS_VALIDATION_ERROR', 'Invalid status.');
    const user = state.users.find(x => x.id === entry.userId);
    if (body.status === 'Active' && (!user || user.status !== 'APPROVED' || user.role !== entry.role))
      return failure(400, 'SEMESTER_STAFF_CONFLICT', 'The user must be active with the requested role.');
    if (body.status === 'Inactive' && staffResponse(entry).assignments.length > 0)
      return failure(400, 'SEMESTER_STAFF_CONFLICT', 'Reassign active classes or teams before removing availability.');
    entry.status = body.status === 'Active' ? 'ACTIVE' : 'INACTIVE'; entry.rowVersion = allocateRowVersion();
    persistMockState(); return ok(staffResponse(entry));
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
    const body = parseBody(config);
    const checkpoints = body.checkpoints;
    const otherAssessments = Array.isArray(body.otherAssessments) ? body.otherAssessments : [];
    const totalWeight = (Array.isArray(checkpoints) ? checkpoints : []).reduce((sum, item) => sum + asNumber(item.courseWeight, 0), 0)
      + otherAssessments.reduce((sum, item) => sum + asNumber(item.weight, 0), 0);
    if (Math.abs(totalWeight - 100) > 0.001) return failure(400, 'VALIDATION_ERROR', `Checkpoint and other assessment weights must total exactly 100.0% (currently ${totalWeight.toFixed(1)}%).`);
    curriculum.checkpoints = Array.isArray(checkpoints) ? checkpoints as MockCurriculum['checkpoints'] : [];
    curriculum.otherAssessments = otherAssessments.map((item) => ({
      _id: asString(item._id) || allocateId(),
      name: asString(item.name).trim(),
      weight: asNumber(item.weight, 0),
    }));
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

interface MockIndustryImportRow {
  rowNumber: number;
  name: string;
  description: string;
  isValid: boolean;
  status: 'Ready' | 'Error';
  errorMessage: string | null;
  errorCode: 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID' | 'STARTUP_INDUSTRY_IMPORT_CONFLICT' | null;
}

async function inspectIndustryImport(config: AxiosRequestConfig): Promise<{ error?: MockReply; rows?: MockIndustryImportRow[] }> {
  const formData = config.data instanceof FormData ? config.data : null;
  const file = formData?.get('file');
  if (!(file instanceof Blob) || file.size === 0 || file.size > 5 * 1024 * 1024) {
    return { error: failure(400, 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID', 'Select a non-empty Excel file not exceeding 5 MB.') };
  }

  const fileName = 'name' in file && typeof file.name === 'string' ? file.name : '';
  if (!/\.(xlsx|xls)$/i.test(fileName)) {
    return { error: failure(400, 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID', 'Only Excel files (.xlsx or .xls) are allowed.') };
  }

  try {
    const workbook = XLSX.read(await file.arrayBuffer(), { type: 'array' });
    const worksheet = workbook.Sheets[workbook.SheetNames[0]];
    const sourceRows = worksheet
      ? XLSX.utils.sheet_to_json<unknown[]>(worksheet, { header: 1, defval: '' })
      : [];
    const normalizeHeader = (value: unknown) => String(value ?? '').trim().toLowerCase().replace(/[^a-z0-9]/g, '');
    const headerIndex = sourceRows.slice(0, 10).findIndex((row) => {
      const headers = row.map(normalizeHeader);
      return headers.some((header) => header === 'industry' || header === 'industryname')
        && headers.includes('description');
    });
    if (headerIndex < 0) {
      return { error: failure(400, 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID', "The header must contain 'Industry name' (or 'Industry') and 'Description' columns.") };
    }

    const headers = sourceRows[headerIndex].map(normalizeHeader);
    const nameIndex = headers.findIndex((header) => header === 'industry' || header === 'industryname');
    const descriptionIndex = headers.indexOf('description');
    const rows: MockIndustryImportRow[] = sourceRows.slice(headerIndex + 1)
      .map((row, offset) => ({
        rowNumber: headerIndex + offset + 2,
        name: String(row[nameIndex] ?? '').trim(),
        description: String(row[descriptionIndex] ?? '').trim(),
        isValid: true,
        status: 'Ready' as const,
        errorMessage: null,
        errorCode: null,
      }))
      .filter((row) => row.name || row.description);

    if (rows.length === 0 || rows.length > 500) {
      return { error: failure(400, 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID', rows.length === 0
        ? 'The Excel worksheet contains no industry rows.'
        : 'The industry import may contain at most 500 data rows.') };
    }

    for (const row of rows) {
      row.errorMessage = !row.name
        ? 'Industry name is required.'
        : row.name.length > 100
          ? 'Industry name may contain at most 100 characters.'
          : row.description.length > 240
            ? 'Description may contain at most 240 characters.'
            : null;
      if (row.errorMessage) {
        row.isValid = false;
        row.status = 'Error';
        row.errorCode = 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID';
      }
    }

    const nameCounts = rows
      .filter((row) => row.isValid)
      .reduce<Map<string, number>>((counts, row) => {
        const normalized = row.name.toUpperCase();
        counts.set(normalized, (counts.get(normalized) ?? 0) + 1);
        return counts;
      }, new Map());
    for (const row of rows) {
      if (row.isValid && (nameCounts.get(row.name.toUpperCase()) ?? 0) > 1) {
        row.isValid = false;
        row.status = 'Error';
        row.errorMessage = `Industry '${row.name}' appears more than once in the file.`;
        row.errorCode = 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID';
      }
    }

    const existingByName = new Map(getMockState().startupIndustries.map((industry) => [
      industry.name.trim().toUpperCase(),
      industry.name,
    ]));
    for (const row of rows) {
      const existingName = row.isValid ? existingByName.get(row.name.toUpperCase()) : null;
      if (existingName) {
        row.isValid = false;
        row.status = 'Error';
        row.errorMessage = `Industry '${existingName}' already exists.`;
        row.errorCode = 'STARTUP_INDUSTRY_IMPORT_CONFLICT';
      }
    }

    return { rows };
  } catch {
    return { error: failure(400, 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID', 'The Excel file could not be read. Verify that it is a valid .xlsx or .xls workbook.') };
  }
}

function registerStartupIndustryHandlers(mock: MockAdapter): void {
  mock.onGet('/startup-industries/options').reply(() => {
    const industries = getMockState().startupIndustries
      .filter((industry) => industry.status === 'active')
      .sort((left, right) => left.name.localeCompare(right.name));
    return ok({ industries }, 'Active startup industries retrieved successfully.');
  });

  mock.onGet('/startup-industries').reply((config) => {
    const params = requestParams(config);
    const query = asString(params.search).trim().toLowerCase();
    const status = asString(params.status).trim().toLowerCase();
    const sort = asString(params.sort, 'name-asc').trim().toLowerCase();
    const industries = getMockState().startupIndustries
      .filter((industry) => (
        (!status || industry.status === status)
        && (!query || industry.name.toLowerCase().includes(query))
      ))
      .sort((left, right) => {
        const comparison = left.name.localeCompare(right.name);
        return sort === 'name-desc' ? -comparison : comparison;
      });
    return ok({ industries }, 'Startup industries retrieved successfully.');
  });

  mock.onPost('/startup-industries/import/preview').reply(async (config) => {
    const inspection = await inspectIndustryImport(config);
    if (inspection.error) return inspection.error;
    const rows = inspection.rows!;
    return ok({
      totalRows: rows.length,
      validRowsCount: rows.filter((row) => row.isValid).length,
      errorRowsCount: rows.filter((row) => !row.isValid).length,
      rows: rows.map((row) => ({
        rowNumber: row.rowNumber,
        name: row.name,
        description: row.description || null,
        isValid: row.isValid,
        status: row.status,
        errorMessage: row.errorMessage,
      })),
    }, 'Startup industry import preview generated successfully.');
  });

  mock.onPost('/startup-industries/import').reply(async (config) => {
    const inspection = await inspectIndustryImport(config);
    if (inspection.error) return inspection.error;
    const rows = inspection.rows!;
    const invalidRows = rows.filter((row) => !row.isValid);
    if (invalidRows.length > 0) {
      const hasFileError = invalidRows.some((row) => row.errorCode === 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID');
      return failure(
        hasFileError ? 400 : 409,
        hasFileError ? 'STARTUP_INDUSTRY_IMPORT_FILE_INVALID' : 'STARTUP_INDUSTRY_IMPORT_CONFLICT',
        invalidRows.length === 1
          ? `Row ${invalidRows[0].rowNumber}: ${invalidRows[0].errorMessage}`
          : `The workbook contains ${invalidRows.length} invalid rows. Preview the file and resolve every error before importing.`,
      );
    }

    const industries = rows.map((candidate) => ({
        id: allocateId(),
        name: candidate.name,
        description: candidate.description || null,
        status: 'active' as const,
    }));
    getMockState().startupIndustries.push(...industries);
    persistMockState();
    return ok({ importedCount: industries.length, industries }, `${industries.length} startup industries imported successfully.`);
  });

  mock.onPost('/startup-industries').reply((config) => {
    const body = parseBody(config);
    const name = asString(body.name).trim();
    if (!name) return failure(400, 'COMMON_VALIDATION_ERROR', 'Industry name is required.');
    if (getMockState().startupIndustries.some((item) => item.name.toLowerCase() === name.toLowerCase())) {
      return failure(409, 'STARTUP_INDUSTRY_NAME_EXISTS', 'An industry with this name already exists.');
    }
    const industry = {
      id: allocateId(),
      name,
      description: asString(body.description).trim() || null,
      status: asString(body.status, 'active').toLowerCase() === 'inactive' ? 'inactive' as const : 'active' as const,
    };
    getMockState().startupIndustries.push(industry);
    persistMockState();
    return created(industry, 'Startup industry created successfully.');
  });

  mock.onPut(/^\/startup-industries\/[^/]+\/status$/).reply((config) => {
    const id = routeId(config, /^\/startup-industries\/([^/]+)\/status$/);
    const industry = getMockState().startupIndustries.find((item) => item.id === id);
    if (!industry) return failure(404, 'STARTUP_INDUSTRY_NOT_FOUND', 'Startup industry was not found.');
    industry.status = asString(parseBody(config).status).toLowerCase() === 'inactive' ? 'inactive' : 'active';
    persistMockState();
    return ok(industry, 'Startup industry status updated successfully.');
  });

  mock.onPut(/^\/startup-industries\/[^/]+$/).reply((config) => {
    const id = routeId(config, /^\/startup-industries\/([^/]+)$/);
    const industry = getMockState().startupIndustries.find((item) => item.id === id);
    if (!industry) return failure(404, 'STARTUP_INDUSTRY_NOT_FOUND', 'Startup industry was not found.');
    const body = parseBody(config);
    const name = asString(body.name).trim();
    if (getMockState().startupIndustries.some((item) => item.id !== id && item.name.toLowerCase() === name.toLowerCase())) {
      return failure(409, 'STARTUP_INDUSTRY_NAME_EXISTS', 'An industry with this name already exists.');
    }
    industry.name = name;
    industry.description = asString(body.description).trim() || null;
    industry.status = asString(body.status, industry.status).toLowerCase() === 'inactive' ? 'inactive' : 'active';
    persistMockState();
    return ok(industry, 'Startup industry updated successfully.');
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
  const title = asString(body.title).trim();
  const description = asString(body.description).trim();
  const weekNumber = asNumber(body.weekNumber, existing?.weekNumber ?? 1);
  if (!title) return failure(400, 'COMMON_VALIDATION_ERROR', 'Title is required.');
  if (!description) return failure(400, 'COMMON_VALIDATION_ERROR', 'Description is required.');
  const comparableItems = curriculum.roadmapItems.filter((item) => item._id !== existing?._id);
  if (comparableItems.some((item) => item.title.trim().toLowerCase() === title.toLowerCase())) {
    return failure(400, 'WEEKLY_TASK_DUPLICATED', 'A roadmap item with this title already exists for this subject.');
  }
  if (comparableItems.some((item) => (item.description ?? '').trim().toLowerCase() === description.toLowerCase())) {
    return failure(400, 'WEEKLY_TASK_DUPLICATED', 'A roadmap item with this description already exists for this subject.');
  }
  const item = existing || { _id: allocateId(), title: '', description: null, taskType: 'COURSE_TEMPLATE', courseCode: subjectCode, weekNumber: 1, priority: 'MEDIUM', estimatedHours: null, tags: [] };
  item.title = title;
  item.description = description;
  item.taskType = asString(body.taskType, item.taskType);
  item.courseCode = asString(body.courseCode, subjectCode);
  item.weekNumber = weekNumber;
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

  mock.onGet('/dashboard/academic-overview').reply((config) => {
    const state = getMockState();
    const params = requestParams(config);
    const requestedSemesterId = asString(params.semesterId);
    const requestedCourseId = asString(params.courseId);
    const requestedClassId = asString(params.classId);
    // Without an explicit choice: the active semester, else the latest semester the lecturer teaches in, else the latest one.
    const termOrder: Record<string, number> = { SP: 1, SU: 2, FA: 3 };
    const latestSemester = <T extends { year: number; semester: string }>(items: T[]) =>
      [...items].sort((left, right) => right.year - left.year || (termOrder[right.semester] ?? 0) - (termOrder[left.semester] ?? 0))[0];
    const openSemesters = state.semesters.filter((semester) => semester.status !== 'Archived');
    const taughtSemesterIds = new Set(state.classes
      .filter((cls) => cls.primaryLecturerId === state.sessionUserId)
      .map((cls) => cls.semesterId));
    const selectedSemester = requestedSemesterId
      ? openSemesters.find((semester) => semester.id === requestedSemesterId)
      : openSemesters.find((semester) => semester.status === 'Active')
        ?? latestSemester(openSemesters.filter((semester) => taughtSemesterIds.has(semester.id)))
        ?? latestSemester(openSemesters);
    if (!selectedSemester) {
      return failure(400, 'SEMESTER_NOT_FOUND', requestedSemesterId
        ? 'The selected semester does not exist or is archived.'
        : 'No semester is configured for the academic overview.');
    }

    const semesterCode = `${selectedSemester.semester}${selectedSemester.year}`;
    const assignedClasses = state.classes.filter((cls) => {
      const semester = state.semesters.find((item) => item.id === cls.semesterId);
      return cls.primaryLecturerId === state.sessionUserId
        && cls.status !== 'Archived'
        && semester?.status !== 'Archived';
    });
    if (requestedClassId && !assignedClasses.some((cls) => cls.id === requestedClassId)) {
      return failure(403, 'CLASS_ACCESS_DENIED', 'You do not have access to the selected class.');
    }

    const semesterClasses = assignedClasses.filter((cls) => cls.semesterId === selectedSemester.id);
    if (requestedCourseId && !semesterClasses.some((cls) => cls.courseId === requestedCourseId)) {
      return failure(400, 'VALIDATION_ERROR', 'The selected subject is not available in the selected semester.');
    }
    const selectedClass = requestedClassId
      ? assignedClasses.find((cls) => cls.id === requestedClassId)
      : undefined;
    if (selectedClass && (selectedClass.semesterId !== selectedSemester.id
      || (requestedCourseId && selectedClass.courseId !== requestedCourseId))) {
      return failure(400, 'VALIDATION_ERROR', 'The selected class does not match the selected semester and subject.');
    }

    const classes = semesterClasses.filter((cls) =>
      (!requestedCourseId || cls.courseId === requestedCourseId)
      && (!requestedClassId || cls.id === requestedClassId));
    const classIds = new Set(classes.map((cls) => cls.id));
    const teams = state.teams.filter((team) => classIds.has(team.classId) && team.status === 'Active');
    const teamIds = new Set(teams.map((team) => team.id));
    const projects = teams.filter((team) => Boolean(team.projectName?.trim()));

    const submittedTeamCheckpoints = new Set<string>();
    for (const [key, files] of Object.entries(state.checkpointFiles)) {
      if (files.length > 0 && teamIds.has(key.split(':')[0])) submittedTeamCheckpoints.add(key);
    }
    for (const [key, links] of Object.entries(state.checkpointLinks)) {
      if (links.length > 0 && teamIds.has(key.split(':')[0])) submittedTeamCheckpoints.add(key);
    }

    const finalizedEvaluations = teams.reduce((count, team) => {
      const cls = state.classes.find((item) => item.id === team.classId);
      const checkpoints = cls ? state.curricula[cls.subjectCode]?.checkpoints ?? [] : [];
      return count + checkpoints.filter((checkpoint) =>
        Boolean(state.evaluationPublicationStatuses[checkpointEvaluationId(team.id, checkpoint.number)]))
        .length;
    }, 0);
    const checkpointProgressRows = classes.flatMap((cls) => {
      const classTeams = teams.filter((team) => team.classId === cls.id);
      return (state.curricula[cls.subjectCode]?.checkpoints ?? []).map((checkpoint) => ({
        checkpointId: checkpointDefinitionId(cls.courseId, checkpoint.number),
        courseCode: cls.subjectCode,
        checkpointNumber: checkpoint.number,
        title: checkpoint.title,
        expectedTeams: classTeams.length,
        submittedTeams: classTeams.filter((team) =>
          submittedTeamCheckpoints.has(`${team.id}:${checkpoint.number}`)).length,
        evaluatedProjects: classTeams.filter((team) =>
          Boolean(state.evaluationPublicationStatuses[checkpointEvaluationId(team.id, checkpoint.number)])).length,
        missedDeadlineTeams: 0,
      }));
    });
    const allCheckpointProgress = [...checkpointProgressRows.reduce((groups, item) => {
      const current = groups.get(item.checkpointId);
      groups.set(item.checkpointId, current
        ? {
            ...current,
            expectedTeams: current.expectedTeams + item.expectedTeams,
            submittedTeams: current.submittedTeams + item.submittedTeams,
            evaluatedProjects: current.evaluatedProjects + item.evaluatedProjects,
            missedDeadlineTeams: current.missedDeadlineTeams + item.missedDeadlineTeams,
          }
        : item);
      return groups;
    }, new Map<string, (typeof checkpointProgressRows)[number]>()).values()];
    const scopedSchedules = Object.values(state.checkpointSchedules)
      .filter((schedule) => classIds.has(schedule.classId) && Number.isFinite(Date.parse(schedule.endDateUtc)));
    const currentTime = Date.now();
    const nearestSchedule = scopedSchedules
      .filter((schedule) => Date.parse(schedule.endDateUtc) >= currentTime)
      .sort((left, right) => Date.parse(left.endDateUtc) - Date.parse(right.endDateUtc))[0]
      ?? scopedSchedules
        .filter((schedule) => Date.parse(schedule.endDateUtc) < currentTime)
        .sort((left, right) => Date.parse(right.endDateUtc) - Date.parse(left.endDateUtc))[0];
    const checkpointProgress = nearestSchedule
      ? allCheckpointProgress.filter((item) => item.checkpointId === nearestSchedule.checkpointId)
      : [];
    const classBreakdown = classes.map((cls) => {
      const classTeams = teams.filter((team) => team.classId === cls.id);
      const classTeamIds = new Set(classTeams.map((team) => team.id));
      return {
        classId: cls.id,
        classCode: cls.classCode,
        teams: classTeams.length,
        projects: classTeams.filter((team) => Boolean(team.projectName?.trim())).length,
        submissions: [...submittedTeamCheckpoints].filter((key) => classTeamIds.has(key.split(':')[0])).length,
        evaluations: classTeams.reduce((count, team) => {
          const checkpoints = state.curricula[cls.subjectCode]?.checkpoints ?? [];
          return count + checkpoints.filter((checkpoint) =>
            Boolean(state.evaluationPublicationStatuses[checkpointEvaluationId(team.id, checkpoint.number)]))
            .length;
        }, 0),
        potentialProjects: 0,
      };
    });
    const pendingEvaluations = [...submittedTeamCheckpoints].filter((key) => {
      const separator = key.lastIndexOf(':');
      const teamId = key.slice(0, separator);
      const checkpointNumber = Number(key.slice(separator + 1));
      return !state.evaluationPublicationStatuses[checkpointEvaluationId(teamId, checkpointNumber)];
    }).length;
    const currentWeek = new Date();
    currentWeek.setUTCHours(0, 0, 0, 0);
    currentWeek.setUTCDate(currentWeek.getUTCDate() - ((currentWeek.getUTCDay() + 6) % 7));
    const activityTrend = Array.from({ length: 8 }, (_, index) => ({
      weekStartUtc: new Date(currentWeek.getTime() - (7 - index) * 7 * 86_400_000).toISOString(),
      submissions: 0,
      evaluations: 0,
    }));

    return ok({
      scope: {
        semesterId: selectedSemester.id,
        semesterCode,
        semesterName: `${selectedSemester.semester} ${selectedSemester.year}`,
        isActiveSemester: selectedSemester.status === 'Active',
        courseId: requestedCourseId || null,
        subjectCode: requestedCourseId ? semesterClasses.find((cls) => cls.courseId === requestedCourseId)?.subjectCode ?? null : null,
        subjectName: requestedCourseId ? semesterClasses.find((cls) => cls.courseId === requestedCourseId)?.subjectName ?? null : null,
        classId: requestedClassId || null,
        classCode: selectedClass?.classCode ?? null,
      },
      filterOptions: {
        semesters: state.semesters
          .filter((semester) => semester.status !== 'Archived'
            && (semester.id === selectedSemester.id || assignedClasses.some((cls) => cls.semesterId === semester.id)))
          .sort((left, right) => right.year - left.year || left.semester.localeCompare(right.semester))
          .map((semester) => ({
            id: semester.id,
            code: `${semester.semester}${semester.year}`,
            name: `${semester.semester} ${semester.year}`,
            year: semester.year,
            isActive: semester.status === 'Active',
          })),
        subjects: [...new Map(semesterClasses.map((cls) => [cls.courseId, {
          id: cls.courseId,
          code: cls.subjectCode,
          name: cls.subjectName,
        }])).values()].sort((left, right) => left.code.localeCompare(right.code)),
        classes: semesterClasses
          .filter((cls) => !requestedCourseId || cls.courseId === requestedCourseId)
          .sort((left, right) => left.classCode.localeCompare(right.classCode))
          .map((cls) => ({ id: cls.id, code: cls.classCode, courseId: cls.courseId })),
      },
      metrics: {
        totalClasses: classes.length,
        totalTeams: teams.length,
        totalProjects: projects.length,
        totalSubmissions: submittedTeamCheckpoints.size,
        totalEvaluations: finalizedEvaluations,
        totalPotentialProjects: 0,
      },
      attention: {
        missedDeadlines: 0,
        pendingEvaluations,
      },
      activityTrend,
      checkpointProgress,
      classes: classBreakdown,
      topTeams: [],
      lastUpdatedAtUtc: new Date().toISOString(),
      hasAssignedClasses: assignedClasses.length > 0,
      hasMatchingClasses: classes.length > 0,
      hasClasses: classes.length > 0,
    }, 'Academic overview retrieved successfully.');
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
    const publishedTeams = teams.filter(team =>
      state.evaluationPublicationStatuses[checkpointEvaluationId(team.id, 1)] === 'PUBLISHED');
    return ok({
      myTeams: teams.length,
      pendingReviews: state.directions.filter((direction) => teams.some((team) => team.id === direction.teamId) && direction.status === 'Submitted').length,
      upcomingSessions: teams.length,
      averageScore: publishedTeams.length ? 88 : null,
      taskProgress: teams.length ? 72 : 0,
      recentEvaluations: teams.slice(0, 2).map((team, index) => ({
        _id: `mock-evaluation-${team.id}`,
        teamId: { teamName: team.teamName },
        ...(state.evaluationPublicationStatuses[checkpointEvaluationId(team.id, 1)] === 'PUBLISHED'
          ? { totalScore: 84 + index * 3 }
          : {}),
      })),
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
    const scorePublished = team
      ? state.evaluationPublicationStatuses[checkpointEvaluationId(team.id, 1)] === 'PUBLISHED'
      : false;
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
      latestEvaluation: team ? {
        ...(scorePublished ? { totalScore: 84.5 } : {}),
        comment: 'Strong validation plan; clarify the primary customer segment.',
      } : null,
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

  mock.onGet('/notifications').reply(() => {
    const state = getMockState();
    const notifications = state.notifications
      .filter((notification) => notification.recipientUserId === state.sessionUserId)
      .map((notification) => ({ ...notification, _id: notification.id }));
    return ok(notifications, 'Notifications retrieved successfully.');
  });
  mock.onGet('/notifications/unread-count').reply(() => {
    const state = getMockState();
    const count = state.notifications.filter((notification) => (
      notification.recipientUserId === state.sessionUserId && !notification.isRead
    )).length;
    return ok({ count }, 'Unread notification count retrieved successfully.');
  });
  mock.onPut(/^\/notifications\/[^/]+\/read$/).reply((config) => {
    const state = getMockState();
    const notificationId = routeId(config, /^\/notifications\/([^/]+)\/read$/);
    const notification = state.notifications.find((item) => (
      item.id === notificationId && item.recipientUserId === state.sessionUserId
    ));
    if (!notification) return failure(404, 'NOTIFICATION_NOT_FOUND', 'Notification was not found.');
    notification.isRead = true;
    notification.readAt = new Date().toISOString();
    persistMockState();
    return ok(null, 'Notification marked as read.');
  });
  mock.onPut('/notifications/mark-all-read').reply(() => {
    const state = getMockState();
    const now = new Date().toISOString();
    state.notifications
      .filter((notification) => notification.recipientUserId === state.sessionUserId && !notification.isRead)
      .forEach((notification) => {
        notification.isRead = true;
        notification.readAt = now;
      });
    persistMockState();
    return ok(null, 'All notifications marked as read.');
  });
}

export function registerCoreMockHandlers(mock: MockAdapter): void {
  registerAuthHandlers(mock);
  registerUserHandlers(mock);
  registerSubjectHandlers(mock);
  registerStartupIndustryHandlers(mock);
  registerDashboardHandlers(mock);
}
