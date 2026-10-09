import assert from 'node:assert/strict';
import test from 'node:test';
import {
  CHECKPOINT_END_AFTER_START_ERROR,
  checkpointDateParts,
  checkpointDateRangeError,
  formatCheckpointDateTime,
  parseCheckpointLocalDateTime,
} from '../src/utils/checkpointDateTime.ts';

test('checkpoint date and time stays in 24-hour DD/MM/YYYY HH:mm format', () => {
  const date = parseCheckpointLocalDateTime('2026-09-23T08:00');
  assert.ok(date);
  assert.equal(formatCheckpointDateTime(date), '23/09/2026 08:00');
  assert.deepEqual(checkpointDateParts('2026-09-30T23:59', '08:00'), {
    date: '2026-09-30', hour: '23', minute: '59',
  });
  assert.deepEqual(checkpointDateParts('', '23:59'), { date: '', hour: '23', minute: '59' });
});

test('invalid calendar dates and 12-hour or incomplete time values cannot be saved', () => {
  assert.equal(parseCheckpointLocalDateTime('2026-02-30T08:00'), null);
  assert.equal(parseCheckpointLocalDateTime('2026-09-23T24:00'), null);
  assert.equal(parseCheckpointLocalDateTime('2026-09-23T08:60'), null);
  assert.equal(parseCheckpointLocalDateTime('2026-09-23T8:00'), null);
  assert.equal(parseCheckpointLocalDateTime('2026-09-23T08:00 PM'), null);
});

test('end must be later than start, including on the same date', () => {
  assert.equal(checkpointDateRangeError('2026-09-23T08:00', '2026-09-23T08:00'), CHECKPOINT_END_AFTER_START_ERROR);
  assert.equal(checkpointDateRangeError('2026-09-23T08:00', '2026-09-23T07:59'), CHECKPOINT_END_AFTER_START_ERROR);
  assert.equal(checkpointDateRangeError('2026-09-23T08:00', '2026-09-23T08:01'), '');
});
