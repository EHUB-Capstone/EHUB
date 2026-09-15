import assert from 'node:assert/strict';
import test from 'node:test';
import {
  AUTH_FIELD_LIMITS,
  BACKEND_MAJOR_CODES,
  LOGIN_FIELDS,
  PUBLIC_REGISTER_ROLES,
  REGISTER_FIELDS,
  mapApiFieldErrors,
  normalizeLoginPayload,
  normalizeRegisterPayload,
  toFieldErrorMap,
  validateLoginPayload,
  validateRegisterPayload,
  type AuthValidationError,
  type RegisterField,
} from '../src/utils/authValidation.ts';
import type { LoginPayload, RegisterPayload } from '../src/types/auth.ts';
import { parseApiError } from '../src/utils/apiError.ts';

const validLogin = (overrides: Partial<LoginPayload> = {}): LoginPayload => ({
  email: 'student@fpt.edu.vn',
  password: 'Mock123!',
  ...overrides,
});

const validRegister = (overrides: Partial<RegisterPayload> = {}): RegisterPayload => ({
  fullName: 'Nguyen Van A',
  email: 'student@fpt.edu.vn',
  password: 'Mock123!',
  confirmPassword: 'Mock123!',
  role: 'Student',
  majorCode: 'BIT_SE',
  ...overrides,
});

const errorsFor = <Field extends string>(
  errors: readonly AuthValidationError<Field>[],
  field: Field,
): AuthValidationError<Field>[] => errors.filter((error) => error.field === field);

test('login mirrors backend required and ASP.NET-compatible email rules', () => {
  const emptyErrors = validateLoginPayload(validLogin({ email: '   ', password: '   ' }));
  assert.deepEqual(errorsFor(emptyErrors, 'email'), [
    { field: 'email', message: 'Email is required.', code: 'NotEmptyValidator' },
    { field: 'email', message: 'Email is not in a valid format.', code: 'EmailValidator' },
  ]);
  assert.deepEqual(errorsFor(emptyErrors, 'password'), [
    { field: 'password', message: 'Password is required.', code: 'NotEmptyValidator' },
    { field: 'password', message: 'Password must be at least 6 characters.', code: 'MinimumLengthValidator' },
  ]);

  for (const email of ['plain-address', '@domain', 'local@', 'one@two@three']) {
    assert.deepEqual(errorsFor(validateLoginPayload(validLogin({ email })), 'email'), [
      { field: 'email', message: 'Email is not in a valid format.', code: 'EmailValidator' },
    ]);
  }

  // FluentValidation's default EmailAddress mode intentionally accepts this.
  assert.equal(errorsFor(validateLoginPayload(validLogin({ email: 'a@b' })), 'email').length, 0);
});

test('login enforces backend email and password 6..100 boundaries', () => {
  const emailAtLimit = `${'a'.repeat(AUTH_FIELD_LIMITS.emailMax - 2)}@b`;
  const emailOverLimit = `${'a'.repeat(AUTH_FIELD_LIMITS.emailMax - 1)}@b`;

  assert.equal(errorsFor(validateLoginPayload(validLogin({ email: emailAtLimit })), 'email').length, 0);
  assert.deepEqual(errorsFor(validateLoginPayload(validLogin({ email: emailOverLimit })), 'email'), [
    { field: 'email', message: 'Email must not exceed 320 characters.', code: 'MaximumLengthValidator' },
  ]);
  assert.deepEqual(errorsFor(validateLoginPayload(validLogin({ password: '12345' })), 'password'), [
    { field: 'password', message: 'Password must be at least 6 characters.', code: 'MinimumLengthValidator' },
  ]);
  assert.equal(errorsFor(validateLoginPayload(validLogin({ password: '123456' })), 'password').length, 0);
  assert.equal(
    errorsFor(validateLoginPayload(validLogin({ password: 'p'.repeat(AUTH_FIELD_LIMITS.passwordMax) })), 'password').length,
    0,
  );
  assert.deepEqual(
    errorsFor(validateLoginPayload(validLogin({ password: 'p'.repeat(AUTH_FIELD_LIMITS.passwordMax + 1) })), 'password'),
    [{ field: 'password', message: 'Password must not exceed 100 characters.', code: 'MaximumLengthValidator' }],
  );
});

test('registration mirrors backend full-name required, whitespace, minimum, and maximum rules', () => {
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ fullName: '' })), 'fullName'), [
    { field: 'fullName', message: 'Full name is required.', code: 'NotEmptyValidator' },
    { field: 'fullName', message: 'Full name must not consist of only whitespace.', code: 'PredicateValidator' },
    { field: 'fullName', message: 'Full name must be at least 2 characters.', code: 'MinimumLengthValidator' },
  ]);
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ fullName: '  ' })), 'fullName'), [
    { field: 'fullName', message: 'Full name is required.', code: 'NotEmptyValidator' },
    { field: 'fullName', message: 'Full name must not consist of only whitespace.', code: 'PredicateValidator' },
  ]);
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ fullName: 'A' })), 'fullName'), [
    { field: 'fullName', message: 'Full name must be at least 2 characters.', code: 'MinimumLengthValidator' },
  ]);
  assert.equal(errorsFor(validateRegisterPayload(validRegister({ fullName: 'Ab' })), 'fullName').length, 0);
  assert.equal(errorsFor(validateRegisterPayload(validRegister({ fullName: 'A'.repeat(150) })), 'fullName').length, 0);
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ fullName: 'A'.repeat(151) })), 'fullName'), [
    { field: 'fullName', message: 'Full name must not exceed 150 characters.', code: 'MaximumLengthValidator' },
  ]);
});

test('registration enforces backend email format and length boundaries', () => {
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ email: '' })), 'email'), [
    { field: 'email', message: 'Email is required.', code: 'NotEmptyValidator' },
    { field: 'email', message: 'Email is not in a valid format.', code: 'EmailValidator' },
  ]);
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ email: 'invalid' })), 'email'), [
    { field: 'email', message: 'Email is not in a valid format.', code: 'EmailValidator' },
  ]);

  const emailAtLimit = `${'a'.repeat(318)}@b`;
  const emailOverLimit = `${'a'.repeat(319)}@b`;
  assert.equal(errorsFor(validateRegisterPayload(validRegister({ email: emailAtLimit })), 'email').length, 0);
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ email: emailOverLimit })), 'email'), [
    { field: 'email', message: 'Email must not exceed 320 characters.', code: 'MaximumLengthValidator' },
  ]);
});

test('registration enforces backend password 6..100 boundaries', () => {
  const emptyPasswordErrors = errorsFor(
    validateRegisterPayload(validRegister({ password: '', confirmPassword: '' })),
    'password',
  );
  assert.deepEqual(emptyPasswordErrors, [
    { field: 'password', message: 'Password is required.', code: 'NotEmptyValidator' },
    { field: 'password', message: 'Password must be at least 6 characters.', code: 'MinimumLengthValidator' },
  ]);
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ password: '12345', confirmPassword: '12345' })), 'password'), [
    { field: 'password', message: 'Password must be at least 6 characters.', code: 'MinimumLengthValidator' },
  ]);
  assert.equal(errorsFor(validateRegisterPayload(validRegister({ password: '123456', confirmPassword: '123456' })), 'password').length, 0);
  assert.equal(
    errorsFor(validateRegisterPayload(validRegister({ password: 'p'.repeat(100), confirmPassword: 'p'.repeat(100) })), 'password').length,
    0,
  );
  assert.deepEqual(
    errorsFor(validateRegisterPayload(validRegister({ password: 'p'.repeat(101), confirmPassword: 'p'.repeat(101) })), 'password'),
    [{ field: 'password', message: 'Password must not exceed 100 characters.', code: 'MaximumLengthValidator' }],
  );
});

test('registration requires confirmation and uses the backend mismatch error code', () => {
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ confirmPassword: '' })), 'confirmPassword'), [
    { field: 'confirmPassword', message: 'Confirm password is required.', code: 'NotEmptyValidator' },
    {
      field: 'confirmPassword',
      message: 'Confirm password must match the password.',
      code: 'AUTH_PASSWORD_CONFIRMATION_MISMATCH',
    },
  ]);
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ confirmPassword: 'different' })), 'confirmPassword'), [
    {
      field: 'confirmPassword',
      message: 'Confirm password must match the password.',
      code: 'AUTH_PASSWORD_CONFIRMATION_MISMATCH',
    },
  ]);
  assert.equal(errorsFor(validateRegisterPayload(validRegister()), 'confirmPassword').length, 0);
});

test('registration accepts only the three exact public backend role values', () => {
  assert.deepEqual([...PUBLIC_REGISTER_ROLES].sort(), ['Lecturer', 'Mentor', 'Student']);
  for (const role of PUBLIC_REGISTER_ROLES) {
    const payload = validRegister({ role, majorCode: role === 'Student' ? 'BIT_SE' : undefined });
    assert.equal(errorsFor(validateRegisterPayload(payload), 'role').length, 0, role);
  }

  for (const role of ['Admin', 'student', 'LECTURER', 'Mentor ']) {
    assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ role })), 'role'), [
      {
        field: 'role',
        message: 'Role is invalid. Only Lecturer, Student, Mentor roles are allowed for public registration.',
        code: 'AUTH_INVALID_ROLE',
      },
    ]);
  }
});

test('student major is required and every non-empty major must be a backend major', () => {
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ majorCode: undefined })), 'majorCode'), [
    {
      field: 'majorCode',
      message: 'Major is required for Student role.',
      code: 'AUTH_STUDENT_MAJOR_REQUIRED',
    },
  ]);
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ majorCode: 'UNKNOWN' })), 'majorCode'), [
    { field: 'majorCode', message: 'Selected major is invalid.', code: 'AUTH_INVALID_MAJOR' },
  ]);
  assert.deepEqual(errorsFor(validateRegisterPayload(validRegister({ majorCode: '   ' })), 'majorCode'), [
    {
      field: 'majorCode',
      message: 'Major is required for Student role.',
      code: 'AUTH_STUDENT_MAJOR_REQUIRED',
    },
    { field: 'majorCode', message: 'Selected major is invalid.', code: 'AUTH_INVALID_MAJOR' },
  ]);
  assert.equal(
    errorsFor(validateRegisterPayload(validRegister({ role: 'Lecturer', majorCode: undefined })), 'majorCode').length,
    0,
  );
  assert.deepEqual(
    errorsFor(validateRegisterPayload(validRegister({ role: 'Lecturer', majorCode: 'UNKNOWN' })), 'majorCode'),
    [{ field: 'majorCode', message: 'Selected major is invalid.', code: 'AUTH_INVALID_MAJOR' }],
  );
});

test('frontend major list exactly covers the 10 supported backend registration majors', () => {
  const backendMajors = [
    'BBA_HM', 'BBA_IB', 'BBA_MC', 'BBA_MKT', 'BEN', 'BBA_TM',
    'BIT_AI', 'BIT_GD', 'BIT_IA', 'BIT_SE',
  ];
  assert.equal(BACKEND_MAJOR_CODES.length, 10);
  assert.deepEqual([...BACKEND_MAJOR_CODES].sort(), backendMajors.sort());

  for (const majorCode of backendMajors) {
    assert.equal(
      errorsFor(validateRegisterPayload(validRegister({ majorCode })), 'majorCode').length,
      0,
      majorCode,
    );
  }
});

test('auth payload normalization matches backend handler expectations without changing secrets or role', () => {
  assert.deepEqual(normalizeLoginPayload({ email: '  Student@FPT.EDU.VN  ', password: '  Secret  ' }), {
    email: 'student@fpt.edu.vn',
    password: '  Secret  ',
  });
  assert.deepEqual(normalizeRegisterPayload({
    fullName: '  Nguyen Van A  ',
    email: '  Student@FPT.EDU.VN  ',
    password: '  Secret  ',
    confirmPassword: '  Secret  ',
    role: 'Student',
    majorCode: '  bit_se  ',
  }), {
    fullName: 'Nguyen Van A',
    email: 'student@fpt.edu.vn',
    password: '  Secret  ',
    confirmPassword: '  Secret  ',
    role: 'Student',
    majorCode: 'BIT_SE',
  });
  assert.equal(normalizeRegisterPayload(validRegister({ majorCode: '   ' })).majorCode, undefined);
});

test('API field mapping handles backend casing and nested paths and keeps the first error', () => {
  const loginErrors = mapApiFieldErrors({
    'request.Email': 'First email error',
    EMAIL: 'Later email error',
    'payload.PASSWORD': 'Password error',
    Unknown: 'Ignored',
  }, LOGIN_FIELDS);
  assert.deepEqual(loginErrors, {
    email: 'First email error',
    password: 'Password error',
  });

  const registerErrors = mapApiFieldErrors({
    'request.FullName': 'Name error',
    'command.confirm_password': 'Confirmation error',
    MajorCode: 'First major error',
    'request.major-code': 'Later major error',
  }, REGISTER_FIELDS);
  assert.deepEqual(registerErrors, {
    fullName: 'Name error',
    confirmPassword: 'Confirmation error',
    majorCode: 'First major error',
  });
});

test('API error parser reads backend top-level and validation error envelopes', () => {
  const parsed = parseApiError({
    response: {
      data: {
        success: false,
        message: 'Validation failed',
        code: 'COMMON_VALIDATION_ERROR',
        data: null,
        errors: [
          { field: 'email', message: 'Email is required.', code: 'NotEmptyValidator' },
          { field: 'email', message: 'Email is not in a valid format.', code: 'EmailValidator' },
        ],
      },
    },
  }, 'Fallback');

  assert.deepEqual(parsed, {
    code: 'COMMON_VALIDATION_ERROR',
    message: 'Validation failed',
    fieldErrors: { email: 'Email is required.' },
  });

  assert.equal(parseApiError({ response: { data: { errorCode: 'LEGACY_CODE' } } }, 'Fallback').code, 'LEGACY_CODE');
});

test('field error map displays only the first FluentValidation error for each field', () => {
  const errors: AuthValidationError<RegisterField>[] = [
    { field: 'fullName', message: 'Full name is required.', code: 'NotEmptyValidator' },
    { field: 'fullName', message: 'Full name must not consist of only whitespace.', code: 'PredicateValidator' },
    { field: 'email', message: 'Email is required.', code: 'NotEmptyValidator' },
  ];
  assert.deepEqual(toFieldErrorMap(errors), {
    fullName: 'Full name is required.',
    email: 'Email is required.',
  });
});
