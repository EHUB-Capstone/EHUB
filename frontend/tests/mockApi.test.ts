import assert from 'node:assert/strict';
import test from 'node:test';
import axiosClient from '../src/api/axiosClient.ts';
import { enableApiMocks } from '../src/mocks/mockApi.ts';
import { getMockState, resetMockState } from '../src/mocks/mockHelpers.ts';

resetMockState();
enableApiMocks();

test('mock authentication opens an admin session for protected UI testing', async () => {
  const login = await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  assert.deepEqual(login.data.user.roles, ['Admin']);
  const me = await axiosClient.get('/auth/me');
  assert.equal(me.data.email, 'admin@ehub.local');
});

test('mock auth validation returns the backend field-error envelope in validator order', async () => {
  await assert.rejects(
    axiosClient.post('/auth/register', {
      fullName: '',
      email: '',
      password: '',
      confirmPassword: '',
      role: '',
    }),
    (error: unknown) => {
      const response = (error as {
        response?: {
          status?: number;
          data?: {
            success?: boolean;
            message?: string;
            code?: string;
            data?: unknown;
            errors?: Array<{ field: string; message: string; code: string }>;
          };
        };
      }).response;

      assert.equal(response?.status, 400);
      assert.equal(response?.data?.success, false);
      assert.equal(response?.data?.message, 'Validation failed');
      assert.equal(response?.data?.code, 'COMMON_VALIDATION_ERROR');
      assert.equal(response?.data?.data, null);
      assert.deepEqual(response?.data?.errors, [
        { field: 'fullName', message: 'Full name is required.', code: 'NotEmptyValidator' },
        { field: 'fullName', message: 'Full name must not consist of only whitespace.', code: 'PredicateValidator' },
        { field: 'fullName', message: 'Full name must be at least 2 characters.', code: 'MinimumLengthValidator' },
        { field: 'email', message: 'Email is required.', code: 'NotEmptyValidator' },
        { field: 'email', message: 'Email is not in a valid format.', code: 'EmailValidator' },
        { field: 'password', message: 'Password is required.', code: 'NotEmptyValidator' },
        { field: 'password', message: 'Password must be at least 6 characters.', code: 'MinimumLengthValidator' },
        { field: 'confirmPassword', message: 'Confirm password is required.', code: 'NotEmptyValidator' },
        { field: 'role', message: 'Role is required.', code: 'NotEmptyValidator' },
        {
          field: 'role',
          message: 'Role is invalid. Only Lecturer, Student, Mentor roles are allowed for public registration.',
          code: 'AUTH_INVALID_ROLE',
        },
      ]);
      return true;
    },
  );

  await assert.rejects(
    axiosClient.post('/auth/login', { email: '', password: '' }),
    (error: unknown) => {
      const response = (error as {
        response?: { status?: number; data?: { code?: string; errors?: Array<{ field: string; code: string }> } };
      }).response;
      assert.equal(response?.status, 400);
      assert.equal(response?.data?.code, 'COMMON_VALIDATION_ERROR');
      assert.deepEqual(response?.data?.errors?.map(({ field, code }) => ({ field, code })), [
        { field: 'email', code: 'NotEmptyValidator' },
        { field: 'email', code: 'EmailValidator' },
        { field: 'password', code: 'NotEmptyValidator' },
        { field: 'password', code: 'MinimumLengthValidator' },
      ]);
      return true;
    },
  );
});

test('mock register enforces exact backend role and major values', async () => {
  const validBase = {
    fullName: 'Backend Contract User',
    email: 'contract-role@ehub.local',
    password: 'Secret123!',
    confirmPassword: 'Secret123!',
  };

  await assert.rejects(
    axiosClient.post('/auth/register', { ...validBase, role: 'student', majorCode: 'BIT_SE' }),
    (error: unknown) => {
      const response = (error as {
        response?: { status?: number; data?: { errors?: Array<{ field: string; code: string }> } };
      }).response;
      return response?.status === 400
        && response.data?.errors?.some((item) => item.field === 'role' && item.code === 'AUTH_INVALID_ROLE') === true;
    },
  );

  await assert.rejects(
    axiosClient.post('/auth/register', { ...validBase, role: 'Student', majorCode: 'UNDECLARED' }),
    (error: unknown) => {
      const response = (error as {
        response?: { status?: number; data?: { errors?: Array<{ field: string; code: string }> } };
      }).response;
      return response?.status === 400
        && response.data?.errors?.some((item) => item.field === 'majorCode' && item.code === 'AUTH_INVALID_MAJOR') === true;
    },
  );
});

test('mock register verifies OTP before storing normalized user state', async () => {
  const email = 'new-bla-student@ehub.local';
  const registration = await axiosClient.post('/auth/register', {
    fullName: '  New BLA Student  ',
    email: `  ${email.toUpperCase()}  `,
    password: 'Secret123!',
    confirmPassword: 'Secret123!',
    role: 'Student',
    majorCode: 'bla_cn',
  });

  assert.equal(registration.success, true);
  assert.equal(registration.data.status, 'PendingEmailVerification');
  assert.equal(registration.data.requiresEmailVerification, true);
  assert.equal(registration.data.requiresApproval, false);
  assert.equal(registration.data.user, null);
  assert.equal(getMockState().users.some((user) => user.email === email), false);

  const verification = await axiosClient.post('/auth/register/verify-otp', {
    registrationId: registration.data.registrationId,
    otp: '123456',
  });
  assert.equal(verification.data.status, 'Active');
  assert.equal(verification.data.requiresEmailVerification, false);
  assert.deepEqual(verification.data.user.roles, ['Student']);
  assert.equal(verification.data.user.majorCode, 'BLA_CN');
  assert.ok(verification.data.accessToken);
  assert.ok(verification.data.expiresAt);

  const stored = getMockState().users.find((user) => user.email === email);
  assert.equal(stored?.name, 'New BLA Student');
  assert.equal(stored?.role, 'STUDENT');
  assert.equal(stored?.programGroup, 'BLA');
  assert.equal(stored?.major, 'BLA_CN');
  assert.equal(getMockState().authPasswords[stored?.id ?? ''], 'Secret123!');

  const login = await axiosClient.post('/auth/login', { email, password: 'Secret123!' });
  assert.deepEqual(login.data.user.roles, ['Student']);
});

test('mock register preserves backend duplicate-email business error', async () => {
  await assert.rejects(
    axiosClient.post('/auth/register', {
      fullName: 'Duplicate Admin',
      email: 'ADMIN@EHUB.LOCAL',
      password: 'Secret123!',
      confirmPassword: 'Secret123!',
      role: 'Mentor',
    }),
    (error: unknown) => {
      const response = (error as {
        response?: { status?: number; data?: { code?: string; message?: string; errors?: unknown } };
      }).response;
      assert.equal(response?.status, 409);
      assert.equal(response?.data?.code, 'AUTH_EMAIL_ALREADY_EXISTS');
      assert.equal(response?.data?.message, 'Email already exists.');
      assert.equal(response?.data?.errors, null);
      return true;
    },
  );
});

test('mock login preserves backend credential and approval business errors', async () => {
  await assert.rejects(
    axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Wrong123!' }),
    (error: unknown) => {
      const response = (error as {
        response?: { status?: number; data?: { code?: string; message?: string; errors?: unknown } };
      }).response;
      return response?.status === 401
        && response.data?.code === 'AUTH_INVALID_CREDENTIALS'
        && response.data.message === 'Invalid email or password.'
        && response.data.errors === null;
    },
  );

  await assert.rejects(
    axiosClient.post('/auth/login', { email: 'yen.mentor@ehub.local', password: 'Mock123!' }),
    (error: unknown) => {
      const response = (error as {
        response?: { status?: number; data?: { code?: string; message?: string; errors?: unknown } };
      }).response;
      return response?.status === 403
        && response.data?.code === 'AUTH_ACCOUNT_PENDING_APPROVAL'
        && response.data.message === 'Your account is pending admin approval.'
        && response.data.errors === null;
    },
  );
});

test('mock auth mirrors backend blocked and inactive account failures', async () => {
  for (const [email, code, message] of [
    ['rejected.mentor@ehub.local', 'AUTH_ACCOUNT_REJECTED', 'Your account registration has been rejected.'],
    ['blocked.mentor@ehub.local', 'AUTH_USER_BLOCKED', 'Your account has been blocked.'],
    ['inactive.lecturer@ehub.local', 'AUTH_USER_INACTIVE', 'Your account is inactive.'],
  ] as const) {
    await assert.rejects(
      axiosClient.post('/auth/login', { email, password: 'Mock123!' }),
      (error: unknown) => {
        const response = (error as {
          response?: { status?: number; data?: { code?: string; message?: string; errors?: unknown } };
        }).response;
        return response?.status === 403
          && response.data?.code === code
          && response.data.message === message
          && response.data.errors === null;
      },
    );
  }
});

test('mock me and refresh re-check account status like the backend', async () => {
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  const admin = getMockState().users.find((user) => user.email === 'admin@ehub.local');
  assert.ok(admin);
  admin.status = 'BLOCKED';

  await assert.rejects(
    axiosClient.get('/auth/me'),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 403 && response.data?.code === 'AUTH_USER_BLOCKED';
    },
  );
  await assert.rejects(
    axiosClient.post('/auth/refresh-token'),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 403 && response.data?.code === 'AUTH_USER_BLOCKED';
    },
  );

  assert.equal(getMockState().sessionUserId, null);
  admin.status = 'APPROVED';
});

test('mock Google auth validates the request and mirrors backend business errors', async () => {
  await assert.rejects(
    axiosClient.post('/auth/google', { idToken: '' }),
    (error: unknown) => {
      const response = (error as {
        response?: { status?: number; data?: { code?: string; errors?: Array<{ field: string; code: string }> } };
      }).response;
      return response?.status === 400
        && response.data?.code === 'COMMON_VALIDATION_ERROR'
        && response.data.errors?.[0]?.field === 'idToken'
        && response.data.errors[0].code === 'NotEmptyValidator';
    },
  );
  await assert.rejects(
    axiosClient.post('/auth/google', { idToken: 'x'.repeat(5_001) }),
    (error: unknown) => {
      const response = (error as {
        response?: { status?: number; data?: { code?: string; errors?: Array<{ field: string; code: string }> } };
      }).response;
      return response?.status === 400
        && response.data?.code === 'COMMON_VALIDATION_ERROR'
        && response.data.errors?.[0]?.code === 'MaximumLengthValidator';
    },
  );
  await assert.rejects(
    axiosClient.post('/auth/google', { idToken: 'not-a-google-jwt' }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 401 && response.data?.code === 'AUTH_INVALID_GOOGLE_TOKEN';
    },
  );
  await assert.rejects(
    axiosClient.post('/auth/google', { idToken: 'mock-google-unverified:admin@ehub.local' }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 401 && response.data?.code === 'AUTH_GOOGLE_EMAIL_NOT_VERIFIED';
    },
  );
  await assert.rejects(
    axiosClient.post('/auth/google', { idToken: 'mock-google:not-registered@ehub.local' }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 404 && response.data?.code === 'AUTH_ACCOUNT_NOT_REGISTERED';
    },
  );
  await assert.rejects(
    axiosClient.post('/auth/google', { idToken: 'mock-google:blocked.mentor@ehub.local' }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 403 && response.data?.code === 'AUTH_USER_BLOCKED';
    },
  );

  const login = await axiosClient.post('/auth/google', { idToken: 'mock-google:admin@ehub.local' });
  assert.deepEqual(login.data.user.roles, ['Admin']);
  assert.equal(login.data.user.status, 'Active');
  assert.equal(login.message, 'Google login successfully');
});

test('reset restores pristine mock fixtures after mutable auth operations', async () => {
  resetMockState();
  const fixtureCount = getMockState().users.length;
  const fixtureRoom = getMockState().classes[0].room;
  const email = 'reset-proof@ehub.local';

  const registration = await axiosClient.post('/auth/register', {
    fullName: 'Reset Proof',
    email,
    password: 'Secret123!',
    confirmPassword: 'Secret123!',
    role: 'Student',
    majorCode: 'BIT_SE',
  });
  await axiosClient.post('/auth/register/verify-otp', {
    registrationId: registration.data.registrationId,
    otp: '123456',
  });
  getMockState().classes[0].room = 'MUTATED';
  assert.ok(getMockState().users.some((user) => user.email === email));

  resetMockState();
  assert.equal(getMockState().users.length, fixtureCount);
  assert.equal(getMockState().users.some((user) => user.email === email), false);
  assert.equal(getMockState().classes[0].room, fixtureRoom);
  assert.deepEqual(getMockState().authPasswords, {});
  assert.deepEqual(getMockState().pendingRegistrations, []);
  assert.equal(getMockState().sessionUserId, null);
});

test('mock API returns ApiResponse envelopes and applies class filters', async () => {
  const defaultResponse = await axiosClient.get('/classes', { params: { page: 1, pageSize: 20 } });
  assert.equal(defaultResponse.success, true);
  assert.ok(defaultResponse.data.items.length >= 2);
  assert.ok(defaultResponse.data.items.every((item: { status: string }) => item.status !== 'Archived'));

  const archivedResponse = await axiosClient.get('/classes', { params: { status: 'Archived' } });
  assert.equal(archivedResponse.data.items.length, 1);
  assert.equal(archivedResponse.data.items[0].status, 'Archived');

  const unassignedResponse = await axiosClient.get('/classes', { params: { assignmentStatus: 'Unassigned' } });
  assert.ok(unassignedResponse.data.items.every((item: { primaryLecturerId: string | null }) => item.primaryLecturerId === null));
});

test('mock API enforces admin-only class creation', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'giang.lecturer@ehub.local', password: 'Mock123!' });

  await assert.rejects(
    axiosClient.post('/classes/bulk/preview', {
      subjectCode: 'EXE101',
      semesterId: 'mock-semester-FA2026',
      startClassIndex: 20,
      quantity: 1,
    }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 403 && response.data?.code === 'CLASS_ACCESS_DENIED';
    },
  );

  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  const preview = await axiosClient.post('/classes/bulk/preview', {
    subjectCode: 'EXE101',
    semesterId: 'mock-semester-FA2026',
    startClassIndex: 20,
    quantity: 1,
  });
  assert.equal(preview.data.validCount, 1);
});

test('mock API keeps static spreadsheet downloads ahead of class detail routes', async () => {
  const template = await axiosClient.get('/classes/import-template', { responseType: 'blob' });
  assert.ok(template instanceof Blob);
});

test('mock API persists subject CRUD mutations for later queries', async () => {
  const code = 'MOCK101';
  await axiosClient.post('/subjects', { subjectCode: code, subjectName: 'Mock Contract Testing', status: 'active' });
  const response = await axiosClient.get('/subjects', { params: { search: code } });
  assert.equal(response.data.subjects.length, 1);
  assert.equal(response.data.subjects[0].subjectName, 'Mock Contract Testing');
});

test('mock API enforces archived class read-only behavior', async () => {
  const list = await axiosClient.get('/classes', { params: { pageSize: 20 } });
  const active = list.data.items.find((item: { status: string }) => item.status === 'Active');
  assert.ok(active);

  await axiosClient.post(`/classes/${active.id}/archive`, { rowVersion: active.rowVersion, reason: 'Mock archive test' });
  await assert.rejects(
    axiosClient.post(`/classes/${active.id}/students`, {
      studentCode: 'MOCK999',
      fullName: 'Blocked Student',
      email: 'blocked@fpt.edu.vn',
      majorCode: 'BIT_SE',
    }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 409 && response.data?.code === 'CLASS_ARCHIVED';
    },
  );
});

test('mock API mirrors class completion, read-only state, and audit side effects', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  const list = await axiosClient.get('/classes', { params: { status: 'Active', pageSize: 20 } });
  const active = list.data.items[0];
  assert.ok(active);

  const preview = await axiosClient.get(`/classes/${active.id}/completion-preview`);
  assert.equal(preview.data.rowVersion, active.rowVersion);
  assert.equal(preview.data.blockers.length, 0);
  assert.ok(preview.data.warnings.length > 0);

  const completed = await axiosClient.post(`/classes/${active.id}/complete`, {
    rowVersion: preview.data.rowVersion,
    reason: 'Finished all academic work',
  });
  assert.equal(completed.data.status, 'Completed');
  assert.notEqual(completed.data.rowVersion, active.rowVersion);

  const detail = await axiosClient.get(`/classes/${active.id}`);
  assert.equal(detail.data.status, 'Completed');
  assert.equal(detail.data.completionReason, 'Finished all academic work');
  assert.ok(detail.data.completedAtUtc);
  assert.ok(getMockState().rosters[active.id].every((student) => student.enrollmentStatus === 'Completed'));
  assert.ok(getMockState().proposals.filter((proposal) => proposal.classId === active.id)
    .every((proposal) => proposal.status === 'Cancelled'));
  assert.ok(getMockState().teams.filter((team) => team.classId === active.id)
    .every((team) => team.currentMentorAssignment?.status !== 'Active'));

  await assert.rejects(
    axiosClient.post(`/classes/${active.id}/students`, {
      studentCode: 'MOCK999', fullName: 'Read Only Student',
      email: 'readonly@fpt.edu.vn', majorCode: 'BIT_SE',
    }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 409 && response.data?.code === 'CLASS_COMPLETED';
    },
  );
});

test('mock student self-service separates current classes from completed history', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'se200001@fpt.edu.vn', password: 'Mock123!' });

  const current = await axiosClient.get('/classes/my-classes', { params: { scope: 'Current' } });
  assert.ok(current.data.classes.some((cls: { classStatus: string; enrollmentStatus: string }) =>
    cls.classStatus === 'Active' && cls.enrollmentStatus === 'Active'));

  const history = await axiosClient.get('/classes/my-classes', { params: { scope: 'History' } });
  assert.ok(history.data.classes.some((cls: { classStatus: string; enrollmentStatus: string }) =>
    cls.classStatus === 'Archived' && cls.enrollmentStatus === 'Completed'));
});

test('mock semester lifecycle returns typed records and backend-style blockers', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });

  const current = await axiosClient.get('/subjects/current-semester');
  assert.equal(current.data.currentSemester.status, 'Active');
  assert.ok(current.data.currentSemester.id);
  assert.ok(current.data.currentSemester.rowVersion);

  const semesters = await axiosClient.get('/subjects/semesters');
  assert.equal(semesters.data.semesters.length, 2);
  const preview = await axiosClient.get(`/subjects/semesters/${current.data.currentSemester.id}/completion-preview`);
  assert.ok(preview.data.blockers.length > 0);
  assert.ok(preview.data.activeClassCount > 0);

  await assert.rejects(
    axiosClient.post(`/subjects/semesters/${current.data.currentSemester.id}/complete`, {
      rowVersion: preview.data.rowVersion,
      reason: 'Close academic semester',
    }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 409 && response.data?.code === 'SEMESTER_COMPLETION_BLOCKED';
    },
  );
});

test('mock semester schedule supports admin planning and date correction', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });

  const planned = await axiosClient.post('/subjects/semesters', {
    semester: 'SP', year: 2027, startDate: '2027-01-05', endDate: '2027-04-25',
  });
  assert.equal(planned.data.status, 'Planned');
  assert.equal(planned.data.startDate, '2027-01-05');

  const updated = await axiosClient.put(`/subjects/semesters/${planned.data.id}/dates`, {
    startDate: '2027-01-08', endDate: '2027-04-28',
    rowVersion: planned.data.rowVersion, reason: 'Academic calendar correction',
  });
  assert.equal(updated.data.startDate, '2027-01-08');
  assert.notEqual(updated.data.rowVersion, planned.data.rowVersion);

  await assert.rejects(
    axiosClient.post('/subjects/semesters', {
      semester: 'SU', year: 2027, startDate: '2027-04-20', endDate: '2027-08-20',
    }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 409 && response.data?.code === 'SEMESTER_INVALID_STATE';
    },
  );
});

test('mock manual enrollment preserves backend identity and explicit re-enroll rules', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  const list = await axiosClient.get('/classes', { params: { status: 'Active', pageSize: 20 } });
  const active = list.data.items[0];

  await assert.rejects(
    axiosClient.post(`/classes/${active.id}/students`, {
      studentCode: 'NEW200099', fullName: 'Invalid Major',
      email: 'invalid-major@fpt.edu.vn', majorCode: 'UNKNOWN',
    }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 400 && response.data?.code === 'CLASS_VALIDATION_ERROR';
    },
  );

  const added = await axiosClient.post(`/classes/${active.id}/students`, {
    studentCode: 'SE200011',
    fullName: 'Existing Student',
    email: 'se200011@fpt.edu.vn',
    majorCode: null,
  });
  assert.equal(added.data.majorCode, 'BIT_SE');

  await assert.rejects(
    axiosClient.post(`/classes/${active.id}/students`, {
      studentCode: 'SE200011',
      fullName: 'Existing Student',
      email: 'mk200012@fpt.edu.vn',
      majorCode: null,
    }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 409 && response.data?.code === 'STUDENT_IDENTITY_CONFLICT';
    },
  );

  added.data.enrollmentStatus = 'Dropped';
  const stored = getMockState().rosters[active.id].find((student) => student.studentId === added.data.studentId);
  assert.ok(stored);
  stored.enrollmentStatus = 'Dropped';
  await assert.rejects(
    axiosClient.post(`/classes/${active.id}/students`, {
      studentCode: 'SE200011',
      fullName: 'Existing Student',
      email: 'se200011@fpt.edu.vn',
      majorCode: null,
    }),
    (error: unknown) => {
      const response = (error as { response?: { status?: number; data?: { code?: string } } }).response;
      return response?.status === 409 && response.data?.code === 'STUDENT_RE_ENROLLMENT_REQUIRED';
    },
  );
});
