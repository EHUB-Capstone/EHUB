import assert from 'node:assert/strict';
import test from 'node:test';
import { toClassViewModel, unwrapApiData } from '../src/utils/classMappers.ts';
import type { ClassDto } from '../src/types/classes.ts';

const createClassDto = (overrides: Partial<ClassDto> = {}): ClassDto => ({
  id: 'class-1',
  classCode: 'SWE101_1',
  classIndex: 1,
  courseId: 'course-1',
  subjectCode: 'SWE101',
  subjectName: 'Software Engineering',
  semesterId: 'semester-1',
  semesterCode: 'SP2026',
  year: 2026,
  primaryLecturerId: 'lecturer-1',
  primaryLecturerName: 'Lecturer One',
  primaryLecturerEmail: 'lecturer@example.edu',
  room: 'P.301',
  schedules: [{ dayOfWeek: 2, slotNumber: 1, room: null }],
  isEnrollmentMajorLocked: false,
  status: 'Active',
  studentCount: 20,
  teamCount: 4,
  createdAtUtc: '2026-08-05T00:00:00Z',
  rowVersion: '7',
  ...overrides,
});

test('unwrapApiData extracts an API envelope', () => {
  const value = unwrapApiData({ success: true, message: 'ok', data: { count: 2 } });
  assert.deepEqual(value, { count: 2 });
});

test('toClassViewModel maps the typed schedule and primary lecturer', () => {
  const result = toClassViewModel(createClassDto());

  assert.equal(result._id, 'class-1');
  assert.equal(result.semester, 'SP');
  assert.equal(result.lectureId?._id, 'lecturer-1');
  assert.deepEqual(result.schedules, [{ dayOfWeek: 2, slotNumber: 1, room: null }]);
});

test('toClassViewModel keeps one isolated fallback for legacy scheduleJson', () => {
  const source = createClassDto({
    schedules: undefined as unknown as ClassDto['schedules'],
    scheduleJson: '[{"dayOfWeek":4,"slotNumber":3,"room":"P.401"}]',
  });

  assert.deepEqual(toClassViewModel(source).schedules, [
    { dayOfWeek: 4, slotNumber: 3, room: 'P.401' },
  ]);
});
