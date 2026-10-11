import assert from 'node:assert/strict';
import test from 'node:test';
import type { MentorSemesterClass } from '../src/types/mentorAdmin.ts';
import { filterSemesterClasses, missingSlots, totalMissingSlots } from '../src/utils/semesterMentorClasses.ts';

const item = (overrides: Partial<MentorSemesterClass>): MentorSemesterClass => ({
  classId: 'c1',
  classCode: 'EXE201_1',
  subjectCode: 'EXE201',
  lecturerName: 'Lecturer One',
  teamCount: 4,
  missingEnterpriseCount: 0,
  missingAcademicCount: 0,
  temporarySlotCount: 0,
  ...overrides,
});

const classes = [
  item({ classId: 'a', classCode: 'EXE201_2', missingAcademicCount: 1 }),
  item({ classId: 'b', classCode: 'EXE101_1', subjectCode: 'EXE101', lecturerName: 'Nguyen Van A' }),
  item({ classId: 'c', classCode: 'EXE201_1', missingEnterpriseCount: 2, missingAcademicCount: 2 }),
];

test('missing slots add up both kinds and the total spans every class', () => {
  assert.equal(missingSlots(classes[2]), 4);
  assert.equal(totalMissingSlots(classes), 5);
});

test('classes needing the most mentors come first, then by code', () => {
  const ids = filterSemesterClasses(classes, { search: '', subject: 'ALL', onlyMissing: false }).map(entry => entry.classId);
  assert.deepEqual(ids, ['c', 'a', 'b']);
});

test('filters by subject, by missing mentors and by search text', () => {
  assert.deepEqual(filterSemesterClasses(classes, { search: '', subject: 'EXE101', onlyMissing: false }).map(entry => entry.classId), ['b']);
  assert.deepEqual(filterSemesterClasses(classes, { search: '', subject: 'ALL', onlyMissing: true }).map(entry => entry.classId), ['c', 'a']);
  assert.deepEqual(filterSemesterClasses(classes, { search: 'nguyen', subject: 'ALL', onlyMissing: false }).map(entry => entry.classId), ['b']);
});
