import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildScheduleUpdatePayload,
  getClassLifecyclePresentation,
  isClassReadOnly,
  validateImportFileSelection,
} from '../src/utils/classComponentPolicy.ts';
import {
  buildApprovedLecturerQuery,
  normalizeLecturerOptions,
  USER_DIRECTORY_MAX_PAGE_SIZE,
} from '../src/utils/lecturerDirectory.ts';
import { resolveWorkspaceTab, WORKSPACE_TABS } from '../src/utils/workspaceNavigation.ts';

test('workspace keeps evaluation inside checkpoints and removes standalone evaluation and mentoring tabs', () => {
  assert.deepEqual(WORKSPACE_TABS, ['overview', 'roadmap', 'shortcut']);
  assert.equal(resolveWorkspaceTab('?tab=roadmap'), 'roadmap');
  assert.equal(resolveWorkspaceTab('?tab=evaluation'), 'overview');
  assert.equal(resolveWorkspaceTab('?tab=mentoring'), 'overview');
});

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

test('lecturer directory query respects the backend page-size contract', () => {
  assert.equal(USER_DIRECTORY_MAX_PAGE_SIZE, 100);
  assert.deepEqual(buildApprovedLecturerQuery(), {
    page: 1,
    limit: 100,
    role: 'LECTURER',
    status: 'APPROVED',
  });
});

test('lecturer directory normalizes backend identifiers and ignores incomplete records', () => {
  assert.deepEqual(normalizeLecturerOptions([
    { id: 'lecturer-1', fullName: 'Lecturer One', email: 'one@example.com' },
    { _id: 'lecturer-2', name: 'Lecturer Two', email: 'two@example.com' },
    { id: 'invalid' },
  ]), [
    { id: 'lecturer-1', fullName: 'Lecturer One', email: 'one@example.com', _id: 'lecturer-1', name: 'Lecturer One' },
    { _id: 'lecturer-2', name: 'Lecturer Two', email: 'two@example.com' },
  ]);
});
