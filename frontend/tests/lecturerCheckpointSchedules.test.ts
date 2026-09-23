import assert from 'node:assert/strict';
import test from 'node:test';
import { formatLecturerCheckpointName, groupCheckpointSchedules } from '../src/utils/lecturerCheckpointSchedules.ts';
import type { ClassCheckpointSchedule } from '../src/types/lecturerCheckpoints.ts';

const schedule = (classId: string, checkpointId: string, start: string | null, end: string | null): ClassCheckpointSchedule => ({
  classId,
  classCode: classId,
  checkpointId,
  checkpointNumber: checkpointId === 'cp1' ? 1 : 2,
  checkpointTitle: checkpointId === 'cp1' ? 'Discovery' : 'Market Validation',
  startDateUtc: start,
  endDateUtc: end,
  status: start ? 'Open' : 'NotScheduled',
  canReopen: false,
  reopenCount: 0,
});

test('Lecturer labels derive from the checkpoint number, not its Admin description', () => {
  assert.equal(formatLecturerCheckpointName(1), 'Checkpoint 1');
  assert.equal(formatLecturerCheckpointName(2), 'Checkpoint 2');
  assert.equal(formatLecturerCheckpointName(7), 'Checkpoint 7');
});

test('all classes render one group per Admin checkpoint and identical windows collapse', () => {
  const start = '2026-09-23T08:00:00Z';
  const end = '2026-09-30T23:59:00Z';
  const groups = groupCheckpointSchedules([
    schedule('EXE101_1', 'cp2', start, end),
    schedule('EXE101_3', 'cp2', start, end),
    schedule('EXE101_1', 'cp1', null, null),
    schedule('EXE101_3', 'cp1', null, null),
  ]);
  assert.equal(groups.length, 2);
  assert.equal(groups[1].schedules.length, 2);
  assert.equal(groups[1].uniform, true);
  assert.equal(groups[1].startDateUtc, start);
});

test('different existing windows are marked mixed instead of presented as shared', () => {
  const groups = groupCheckpointSchedules([
    schedule('EXE101_1', 'cp2', '2026-09-23T08:00:00Z', '2026-09-30T23:59:00Z'),
    schedule('EXE101_3', 'cp2', '2026-09-24T08:00:00Z', '2026-10-01T23:59:00Z'),
  ]);
  assert.equal(groups.length, 1);
  assert.equal(groups[0].uniform, false);
  assert.equal(groups[0].status, 'Mixed');
  assert.equal(groups[0].startDateUtc, null);
});

test('the same checkpoint number groups class schedules across subject definitions', () => {
  const groups = groupCheckpointSchedules([
    schedule('EXE101_1', 'cp2', null, null),
    { ...schedule('EXE201_1', 'exe201-cp2', null, null), checkpointTitle: 'Prototype Review' },
  ]);
  assert.equal(groups.length, 1);
  assert.equal(groups[0].schedules.length, 2);
  assert.equal(groups[0].checkpointTitle, 'Multiple subject definitions');
});
