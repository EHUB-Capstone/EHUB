import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildScheduleUpdatePayload,
  getClassLifecyclePresentation,
  isClassReadOnly,
  validateImportFileSelection,
} from '../src/utils/classComponentPolicy.ts';

test('ClassDetail presents Archive for an active class and Restore for an archived class', () => {
  assert.deepEqual(getClassLifecyclePresentation('Active'), {
    action: 'archive', label: 'Archive Class', confirmLabel: 'Archive class',
  });
  assert.deepEqual(getClassLifecyclePresentation('Archived'), {
    action: 'restore', label: 'Restore Class', confirmLabel: 'Restore class',
  });
  assert.equal(isClassReadOnly('Archived'), true);
  assert.equal(isClassReadOnly('Draft'), false);
});

test('ClassManagement archived-card policy never treats an active class as restorable', () => {
  assert.equal(getClassLifecyclePresentation('Archived').action, 'restore');
  assert.equal(getClassLifecyclePresentation('Active').action, 'archive');
});

test('EditScheduleModal builds the exact backend schedule contract without lecturer data', () => {
  assert.deepEqual(buildScheduleUpdatePayload([
    { dayOfWeek: 2, slotNumber: 1, room: '  P.301  ' },
    { dayOfWeek: 5, slotNumber: 3, room: ' ' },
  ], '42'), {
    schedules: [
      { dayOfWeek: 2, slotNumber: 1, room: 'P.301' },
      { dayOfWeek: 5, slotNumber: 3, room: null },
    ],
    rowVersion: '42',
  });
});

test('ImportStudentsModal accepts xls/xlsx and rejects unsupported or oversized files', () => {
  assert.equal(validateImportFileSelection({ name: 'students.xls', size: 1_024 }), '');
  assert.equal(validateImportFileSelection({ name: 'students.XLSX', size: 1_024 }), '');
  assert.match(validateImportFileSelection({ name: 'students.csv', size: 1_024 }), /Unsupported/);
  assert.match(validateImportFileSelection({ name: 'students.xls', size: 11 * 1024 * 1024 }), /10 MB/);
});
