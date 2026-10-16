import assert from 'node:assert/strict';
import test from 'node:test';
import type { IncompleteMentor } from '../src/types/mentorAdmin.ts';
import { toFormValues, toPayload, validateFormValues } from '../src/utils/incompleteMentorForm.ts';

const mentor = (overrides: Partial<IncompleteMentor> = {}): IncompleteMentor => ({
  id: 'm1',
  fullName: 'Nguyen Van A',
  mentorType: 'Enterprise',
  email: null,
  missingFields: ['Email'],
  updatedAtUtc: '2026-10-01T00:00:00Z',
  ...overrides,
});

test('the form starts from what is known and shows blanks for the rest', () => {
  const values = toFormValues(mentor({ phone: '0900000000', jobTitle: 'CEO' }));
  assert.equal(values.fullName, 'Nguyen Van A');
  assert.equal(values.phone, '0900000000');
  assert.equal(values.jobTitle, 'CEO');
  assert.equal(values.address, undefined);
  assert.equal(values.currentAddress, '');
});

test('a name of at least 2 characters is required and the limits are enforced', () => {
  const base = toFormValues(mentor());
  assert.match(validateFormValues({ ...base, fullName: ' a ' }, 'Enterprise')!, /name is required/);
  assert.match(validateFormValues({ ...base, fullName: 'x'.repeat(101) }, 'Enterprise')!, /at most 100/);
  assert.match(validateFormValues({ ...base, phone: '1'.repeat(31) }, 'Enterprise')!, /Phone number may contain at most 30/);
  assert.equal(validateFormValues(base, 'Enterprise'), null);
});

test('the Fpt email and the date of birth are checked for enterprise mentors only', () => {
  const base = toFormValues(mentor());
  assert.match(validateFormValues({ ...base, fptEmail: 'not-an-email' }, 'Enterprise')!, /valid email/);
  assert.match(validateFormValues({ ...base, dateOfBirth: '2999-01-01' }, 'Enterprise')!, /Date of birth/);
  assert.equal(validateFormValues({ ...base, fptEmail: 'not-an-email', dateOfBirth: '2999-01-01' }, 'Academic'), null);
  assert.equal(validateFormValues({ ...base, fptEmail: 'a@fpt.edu.vn', dateOfBirth: '1990-05-17' }, 'Enterprise'), null);
});

test('the payload carries only the fields of the mentor kind and turns blanks into null', () => {
  const enterprise = toPayload({ ...toFormValues(mentor()), fullName: '  Nguyen Van B ', phone: ' 0900 ', contractType: '  ' }, 'Enterprise');
  assert.equal(enterprise.fullName, 'Nguyen Van B');
  assert.equal(enterprise.phone, '0900');
  assert.equal(enterprise.contractType, null);
  assert.equal('department' in enterprise, false);

  const academic = toPayload({ ...toFormValues(mentor({ mentorType: 'Academic' })), department: 'Bộ môn CNTT' }, 'Academic');
  assert.equal(academic.department, 'Bộ môn CNTT');
  assert.equal('phone' in academic, false);
});

test('the login email is optional, must look like an email, and is sent only when filled in', () => {
  const base = toFormValues(mentor());
  assert.equal(validateFormValues(base, 'Enterprise'), null);
  assert.match(validateFormValues({ ...base, email: 'nope' }, 'Enterprise')!, /Login email must be a valid email/);
  assert.match(validateFormValues({ ...base, email: 'nope' }, 'Academic')!, /Login email must be a valid email/);
  assert.equal(toPayload(base, 'Enterprise').email, null);
  assert.equal(toPayload({ ...base, email: ' mentor@fpt.edu.vn ' }, 'Enterprise').email, 'mentor@fpt.edu.vn');
  assert.equal(toPayload({ ...toFormValues(mentor({ mentorType: 'Academic' })), email: 'l@fpt.edu.vn' }, 'Academic').email, 'l@fpt.edu.vn');
});
