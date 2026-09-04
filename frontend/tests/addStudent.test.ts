import assert from 'node:assert/strict';
import test from 'node:test';
import { buildAddStudentPayload, validateAddStudentForm } from '../src/utils/addStudent.ts';

test('allows Major to remain blank for backend profile resolution', () => {
  const values = {
    studentCode: ' se123456 ',
    fullName: ' Nguyen Van A ',
    email: ' A@FPT.EDU.VN ',
    majorCode: '',
  };

  assert.deepEqual(validateAddStudentForm(values), {});
  assert.deepEqual(buildAddStudentPayload(values), {
    studentCode: 'SE123456',
    fullName: 'Nguyen Van A',
    email: 'a@fpt.edu.vn',
    majorCode: null,
  });
});

test('returns field-specific validation messages for missing identity values', () => {
  const result = validateAddStudentForm({
    studentCode: '',
    fullName: ' ',
    email: 'invalid-email',
    majorCode: '',
  });

  assert.equal(result.studentCode, 'Student code is required.');
  assert.equal(result.fullName, 'Full name is required.');
  assert.equal(result.email, 'Enter a valid email address.');
  assert.equal(result.majorCode, undefined);
});

test('preserves an explicitly selected valid major for backend matching', () => {
  assert.equal(buildAddStudentPayload({
    studentCode: 'SE123456',
    fullName: 'Nguyen Van A',
    email: 'a@fpt.edu.vn',
    majorCode: 'BIT_SE',
  }).majorCode, 'BIT_SE');
});
