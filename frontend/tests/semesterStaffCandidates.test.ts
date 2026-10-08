import assert from 'node:assert/strict';
import test from 'node:test';
import type { AddTeachingStaffBatchResponse, TeachingStaffCandidateDto } from '../src/types/subjects.ts';
import { groupCandidates, summarizeStaffBatch } from '../src/utils/semesterStaffCandidates.ts';

const candidate = (overrides: Partial<TeachingStaffCandidateDto>): TeachingStaffCandidateDto => ({
  userId: 'u1',
  name: 'Mentor One',
  email: 'one@example.com',
  role: 'MENTOR',
  mentorType: 'Enterprise',
  contractType: 'Thỉnh giảng',
  ...overrides,
});

const candidates = [
  candidate({ userId: 'b', name: 'Bình', email: 'binh@example.com' }),
  candidate({ userId: 'a', name: 'An', email: 'an@example.com', contractType: 'Khoán' }),
  candidate({ userId: 'h', name: 'Hoa', email: 'hoa@example.com', mentorType: 'Academic', contractType: null }),
  candidate({ userId: 'l', name: 'Lecturer Lan', email: 'lan@example.com', role: 'LECTURER', mentorType: null, contractType: null }),
];
const none = new Set<string>();

test('groupCandidates keeps only the requested role and sorts by name', () => {
  const mentors = groupCandidates(candidates, 'MENTOR', none, { search: '', mentorType: 'ALL' });
  assert.deepEqual(mentors.available.map(item => item.userId), ['a', 'b', 'h']);

  const lecturers = groupCandidates(candidates, 'LECTURER', none, { search: '', mentorType: 'ALL' });
  assert.deepEqual(lecturers.available.map(item => item.userId), ['l']);
});

test('groupCandidates separates accounts that are already in the semester', () => {
  const groups = groupCandidates(candidates, 'MENTOR', new Set(['b']), { search: '', mentorType: 'ALL' });
  assert.deepEqual(groups.available.map(item => item.userId), ['a', 'h']);
  assert.deepEqual(groups.inSemester.map(item => item.userId), ['b']);
});

test('groupCandidates filters mentors by kind but never filters lecturers by it', () => {
  assert.deepEqual(
    groupCandidates(candidates, 'MENTOR', none, { search: '', mentorType: 'Academic' }).available.map(item => item.userId),
    ['h'],
  );
  assert.deepEqual(
    groupCandidates(candidates, 'LECTURER', none, { search: '', mentorType: 'Academic' }).available.map(item => item.userId),
    ['l'],
  );
});

test('groupCandidates searches name, email and contract type without caring about accents', () => {
  const find = (search: string) =>
    groupCandidates(candidates, 'MENTOR', none, { search, mentorType: 'ALL' }).available.map(item => item.userId);

  assert.deepEqual(find('binh'), ['b']);
  assert.deepEqual(find('an@example'), ['a']);
  assert.deepEqual(find('khoan'), ['a']);
  assert.deepEqual(find('zzz'), []);
});

const response = (overrides: Partial<AddTeachingStaffBatchResponse>): AddTeachingStaffBatchResponse => ({
  results: [],
  addedCount: 0,
  alreadyInListCount: 0,
  rejectedCount: 0,
  ...overrides,
});
const nameOf = (id: string) => `Name ${id}`;

test('summarizeStaffBatch reports a clean success', () => {
  const summary = summarizeStaffBatch(
    response({ addedCount: 3, results: [{ userId: 'a', outcome: 'Added' }] }), nameOf, 'mentor');
  assert.equal(summary.tone, 'success');
  assert.equal(summary.message, 'Added 3 mentors.');
  assert.deepEqual(summary.problems, []);
});

test('summarizeStaffBatch lists who was skipped and why when some were added', () => {
  const summary = summarizeStaffBatch(response({
    addedCount: 1,
    alreadyInListCount: 1,
    rejectedCount: 1,
    results: [
      { userId: 'a', outcome: 'Added' },
      { userId: 'b', outcome: 'AlreadyInList', message: 'Already there.' },
      { userId: 'c', outcome: 'Rejected', message: 'Account is inactive.' },
    ],
  }), nameOf, 'mentor');

  assert.equal(summary.tone, 'partial');
  assert.equal(summary.message, 'Added 1 mentor, 2 skipped.');
  assert.deepEqual(summary.problems, [
    { userId: 'b', name: 'Name b', message: 'Already there.' },
    { userId: 'c', name: 'Name c', message: 'Account is inactive.' },
  ]);
});

test('summarizeStaffBatch distinguishes nothing added because of rejections from everyone already being listed', () => {
  assert.equal(
    summarizeStaffBatch(response({ rejectedCount: 2, results: [{ userId: 'a', outcome: 'Rejected' }] }), nameOf, 'lecturer').tone,
    'error',
  );
  const info = summarizeStaffBatch(
    response({ alreadyInListCount: 1, results: [{ userId: 'a', outcome: 'AlreadyInList' }] }), nameOf, 'lecturer');
  assert.equal(info.tone, 'info');
  assert.equal(info.problems[0].message, 'Already in the semester list.');
});
