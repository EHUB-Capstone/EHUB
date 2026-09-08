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
import { buildWorkspaceCheckpointOverview } from '../src/utils/workspaceCheckpointOverview.ts';
import {
  buildLecturerDirectionOverviewLink,
  getLegacyNotificationClassId,
  isProjectDirectionSubmittedNotification,
} from '../src/utils/notificationNavigation.ts';
import {
  canSubmitProjectDirection,
  getProjectDirectionDecisionNotice,
  getProjectDirectionSubmitGuidance,
  hasUnsavedProjectDirectionChanges,
  hasProjectDirectionChanged,
  updateProjectDirectionOverviewTeams,
} from '../src/utils/projectDirectionSync.ts';

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

test('workspace checkpoint overview uses configured totals and counts any entered content as progress', () => {
  const result = buildWorkspaceCheckpointOverview([
    {
      number: 1,
      title: 'Startup idea',
      requirements: ['Problem', 'Solution'],
    },
    {
      number: 2,
      title: 'Market validation',
      requirements: ['Interviews'],
    },
  ], [{
    checkpointNumber: 1,
    status: 'Draft',
    requirementContents: [
      { index: 0, content: 'Validated problem' },
      { index: 1, content: '   ' },
    ],
    files: [
      { _id: 'older', originalName: 'older.pdf', fileType: 'pdf', fileSize: 10, uploadedAt: '2026-09-01T00:00:00Z' },
      { _id: 'latest', originalName: 'latest.pdf', fileType: 'pdf', fileSize: 20, uploadedAt: '2026-09-02T00:00:00Z' },
    ],
  }]);

  assert.equal(result.checkpoints.length, 2);
  assert.equal(result.checkpoints[0].icon, 'Users');
  assert.equal(result.checkpoints[1].icon, 'BarChart2');
  assert.deepEqual(result.stats[1], {
    count: 2,
    latest: {
      _id: 'latest',
      originalName: 'latest.pdf',
      fileType: 'pdf',
      fileSize: 20,
      uploadedAt: '2026-09-02T00:00:00Z',
    },
    reqFilled: 1,
    reqTotal: 2,
  });
  assert.deepEqual(result.stats[2], {
    count: 0,
    latest: null,
    reqFilled: 0,
    reqTotal: 1,
  });
});

test('project direction notifications deep-link to the lecturer overview and focused team', () => {
  const notification = {
    type: 'ProjectDirectionSubmitted',
    link: '/classes/class-1',
    data: { teamId: 'team-1' },
  };

  assert.equal(isProjectDirectionSubmittedNotification(notification), true);
  assert.equal(getLegacyNotificationClassId(notification), 'class-1');
  assert.equal(buildLecturerDirectionOverviewLink({
    semester: 'su',
    year: 2026,
    classId: 'class-1',
    teamId: 'team-1',
  }), '/lecturer/classes?semester=SU&year=2026&tab=overview&classId=class-1&teamId=team-1');
});

test('project direction live synchronization detects and announces lecturer decisions', () => {
  const submitted = { id: 'direction-1', status: 'Submitted', rowVersion: '10', reviews: [] };
  const approved = {
    id: 'direction-1',
    status: 'Approved',
    rowVersion: '11',
    reviewedAtUtc: '2026-09-07T01:00:00Z',
    reviews: [{ id: 'review-1', toStatus: 'Approved' }],
  };
  const needsRevision = {
    ...approved,
    status: 'NeedsRevision',
    reviews: [{ id: 'review-2', toStatus: 'NeedsRevision' }],
  };

  assert.equal(hasProjectDirectionChanged(submitted, { ...submitted }), false);
  assert.equal(hasProjectDirectionChanged(submitted, approved), true);
  assert.equal(getProjectDirectionDecisionNotice(submitted, approved), 'Lecturer approved your project direction.');
  assert.equal(getProjectDirectionDecisionNotice(submitted, needsRevision), 'Lecturer reviewed your project direction and requested changes.');
  assert.equal(getProjectDirectionDecisionNotice(approved, needsRevision), '');
});

test('project direction requires requested revisions to be changed and saved before submit', () => {
  const needsRevision = {
    id: 'direction-1',
    title: 'Initial direction',
    summary: 'The initial project direction summary.',
    status: 'NeedsRevision',
    rowVersion: '11',
  };

  assert.equal(canSubmitProjectDirection(needsRevision, needsRevision.title, needsRevision.summary), false);
  assert.equal(hasUnsavedProjectDirectionChanges(needsRevision, needsRevision.title, needsRevision.summary), false);
  assert.match(getProjectDirectionSubmitGuidance(needsRevision, needsRevision.title, needsRevision.summary), /Update.*Save draft/);

  const revisedSummary = 'The revised project direction includes lecturer feedback.';
  assert.equal(hasUnsavedProjectDirectionChanges(needsRevision, needsRevision.title, revisedSummary), true);
  assert.equal(canSubmitProjectDirection(needsRevision, needsRevision.title, revisedSummary), false);
  assert.match(getProjectDirectionSubmitGuidance(needsRevision, needsRevision.title, revisedSummary), /Save.*enable Submit/);

  const savedDraft = { ...needsRevision, summary: revisedSummary, status: 'Draft', rowVersion: '12' };
  assert.equal(canSubmitProjectDirection(savedDraft, savedDraft.title, savedDraft.summary), true);
  assert.equal(getProjectDirectionSubmitGuidance(savedDraft, savedDraft.title, savedDraft.summary), '');

  assert.equal(canSubmitProjectDirection(savedDraft, savedDraft.title, `${savedDraft.summary} Unsaved`), false);
  assert.match(getProjectDirectionSubmitGuidance(savedDraft, savedDraft.title, `${savedDraft.summary} Unsaved`), /Save your changes/);
});

test('lecturer review updates only the reviewed team without reloading the overview', () => {
  const firstTeam = { _id: 'team-1', projectDirectionStatus: 'PENDING', projectDirectionRowVersion: '10' };
  const secondTeam = { _id: 'team-2', projectDirectionStatus: 'PENDING', projectDirectionRowVersion: '20' };
  const result = updateProjectDirectionOverviewTeams([firstTeam, secondTeam], 'team-1', {
    title: 'Approved direction',
    summary: 'Approved summary',
    status: 'Approved',
    rowVersion: '11',
    reviews: [{ id: 'review-1', toStatus: 'Approved', comment: 'Proceed with this scope.' }],
  });

  assert.deepEqual(result[0], {
    _id: 'team-1',
    projectDirectionStatus: 'APPROVED',
    projectDirectionRowVersion: '11',
    projectDirection: 'Approved summary',
    projectDirectionTitle: 'Approved direction',
    projectDirectionReviewComment: 'Proceed with this scope.',
  });
  assert.equal(result[1], secondTeam);

  const submittedResult = updateProjectDirectionOverviewTeams(result, 'team-2', {
    title: 'New submission',
    summary: 'A newly submitted direction',
    status: 'Submitted',
    rowVersion: '21',
    reviews: [],
  });
  assert.equal(submittedResult[1].projectDirectionStatus, 'PENDING');
  assert.equal(submittedResult[1].projectDirectionRowVersion, '21');
});
