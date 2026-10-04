import assert from 'node:assert/strict';
import test from 'node:test';
import {
  CLASS_LIST_PAGE_SIZE,
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
  directionOverviewTargetClassIds,
  getProjectDirectionDraftSeed,
  getApprovedProjectProfileDisplay,
  getProjectDirectionDecisionNotice,
  getProjectDirectionSubmitGuidance,
  hasUnsavedProjectDirectionChanges,
  hasProjectDirectionChanged,
  isProjectDirectionConcurrencyConflict,
  isProjectProfileAvailable,
  resolveDirectionOverviewClassId,
  shouldApplyProjectDirectionSubmission,
  updateProjectDirectionOverviewTeams,
} from '../src/utils/projectDirectionSync.ts';
import {
  canAccessEvaluationRankings,
  calculateWeightedCourseScore,
  filterEvaluationRecords,
  filterTeamsBySemester,
  resolveEvaluationMemberScore,
  resolveActiveEvaluationSemester,
  selectLatestOfficialEvaluation,
} from '../src/utils/evaluationGrading.ts';
import { filterTeamRankingRows, rankTeamResults } from '../src/utils/teamRankings.ts';
import type { TeamRankingItem } from '../src/types/rankings.ts';

const rankingItem = (
  teamId: string,
  rank: number | null,
  status: TeamRankingItem['status'],
): TeamRankingItem => ({
  teamId,
  teamName: `Team ${teamId}`,
  teamCode: `CODE-${teamId}`,
  projectName: `Project ${teamId}`,
  projectDescription: `Description ${teamId}`,
  semesterGroupName: `Group ${teamId}`,
  classId: 'class-1',
  classCode: 'EXE201_8',
  courseCode: 'EXE201',
  semester: 'FA2026',
  year: 2026,
  checkpoints: [{ checkpointId: 'checkpoint-1', number: 1, title: 'Checkpoint 1', weight: 100, status: status === 'PUBLISHED' ? 'PUBLISHED' : status === 'READY_TO_PUBLISH' ? 'SUBMITTED' : 'NOT_GRADED' }],
  assessments: [],
  rank,
  status,
  completedComponentCount: rank === null ? 0 : 1,
  publishedComponentCount: status === 'PUBLISHED' ? 1 : 0,
  totalComponentCount: 1,
});

test('team rankings preserve server ranks and ties without receiving scores', () => {
  const rows = rankTeamResults([
    rankingItem('a', 2, 'PUBLISHED'),
    rankingItem('b', 1, 'PUBLISHED'),
    rankingItem('c', 2, 'PUBLISHED'),
    rankingItem('d', 4, 'PUBLISHED'),
  ]);

  assert.deepEqual(rows.map(row => [row.teamId, row.rank]), [['b', 1], ['a', 2], ['c', 2], ['d', 4]]);
});

test('ranking filters do not renumber server ranks', () => {
  const rows = rankTeamResults([
    rankingItem('a', 1, 'PUBLISHED'),
    rankingItem('b', 2, 'PUBLISHED'),
  ]);
  const filtered = filterTeamRankingRows(rows, { search: 'description b', teamId: '', status: '' });

  assert.deepEqual(rows.map(row => [row.teamId, row.rank]), [['a', 1], ['b', 2]]);
  assert.deepEqual(filtered.map(row => [row.teamId, row.rank]), [['b', 2]]);
});

test('unpublished teams remain unranked', () => {
  const rows = rankTeamResults([
    rankingItem('a', null, 'INCOMPLETE'),
    rankingItem('b', 1, 'PUBLISHED'),
  ]);

  assert.deepEqual(rows.map(row => [row.teamId, row.rank]), [['b', 1], ['a', null]]);
});

test('course score applies checkpoint and other-assessment weights and reports missing grades', () => {
  assert.deepEqual(calculateWeightedCourseScore([
    { score: 8, weight: 10 },
    { score: 7, weight: 20 },
    { score: 9, weight: 15 },
    { score: 8.5, weight: 40 },
    { score: 10, weight: 15 },
  ]), { score: 8.45, complete: true });
  assert.deepEqual(calculateWeightedCourseScore([
    { score: 8, weight: 85 },
    { score: null, weight: 15 },
  ]), { score: 6.8, complete: false });
  assert.deepEqual(calculateWeightedCourseScore([
    { score: 7.5, weight: 85 },
    { score: 9, weight: 15 },
  ]), { score: 7.73, complete: true });
});

test('evaluation rankings are restricted to admin and lecturer roles', () => {
  assert.equal(canAccessEvaluationRankings('ADMIN'), true);
  assert.equal(canAccessEvaluationRankings('lecturer'), true);
  assert.equal(canAccessEvaluationRankings('MENTOR'), false);
  assert.equal(canAccessEvaluationRankings('STUDENT'), false);
  assert.equal(canAccessEvaluationRankings(undefined), false);
});

test('workspace keeps evaluation inside checkpoints and removes standalone evaluation and mentoring tabs', () => {
  assert.deepEqual(WORKSPACE_TABS, ['overview', 'roadmap', 'shortcut', 'history']);
  assert.equal(resolveWorkspaceTab('?tab=roadmap'), 'roadmap');
  assert.equal(resolveWorkspaceTab('?tab=evaluation'), 'overview');
  assert.equal(resolveWorkspaceTab('?tab=mentoring'), 'overview');
});

test('evaluation grading defaults to the active semester and filters the visible role scope', () => {
  const teams = [
    { teamId: 'team-1', teamName: 'Alpha', projectName: 'Campus Connect', projectDescription: 'Connect students across campus.', semesterGroupName: 'Group 01', classId: 'class-1', classCode: 'SE01', courseCode: 'PRM', semester: 'FA2026', accessMode: 'READ_WRITE', isArchived: false, isCurrent: true, hasWorkspace: true },
    { teamId: 'team-2', teamName: 'Beta', classId: 'class-2', classCode: 'SE02', courseCode: 'PRM', semester: 'SU2026', accessMode: 'READ_ONLY', isArchived: false, isCurrent: false, hasWorkspace: true },
  ];
  assert.deepEqual(resolveActiveEvaluationSemester(teams), { semester: 'FA', year: '2026' });
  assert.deepEqual(filterTeamsBySemester(teams, 'FA', '2026').map(team => team.teamId), ['team-1']);

  const records = [{
    key: 'record-1',
    team: teams[0],
    checkpoint: { number: 1, title: 'Problem validation' },
    evaluation: {
      _id: 'evaluation-1',
      lecturerId: { _id: 'lecturer-1', name: 'Lecturer One' },
      evaluatorRole: 'LECTURER',
      status: 'SUBMITTED',
      checkpointTotal: 8.5,
      overallFeedback: 'Strong validation evidence.',
      updatedAt: '2026-09-01T00:00:00Z',
      rubricScores: [{ criterionKey: 'evidence', criterionName: 'Evidence', score: 8.5, comment: 'Clear interviews.' }],
      memberScores: [],
    },
    status: 'SUBMITTED',
  }];

  assert.equal(filterEvaluationRecords(records, { search: 'interviews', status: 'SUBMITTED' }).length, 1);
  assert.equal(filterEvaluationRecords(records, { search: 'campus connect' }).length, 1);
  assert.equal(filterEvaluationRecords(records, { search: 'connect students' }).length, 1);
  assert.equal(filterEvaluationRecords(records, { search: 'group 01' }).length, 1);
  assert.equal(filterEvaluationRecords(records, { teamId: 'team-2' }).length, 0);
});

test('evaluation grading selects the newest official evaluation and never infers an undisclosed member score', () => {
  const base = {
    lecturerId: { _id: 'lecturer-1', name: 'Lecturer One' },
    evaluatorRole: 'LECTURER',
    checkpointTotal: 8.5,
    overallFeedback: '',
    rubricScores: [],
    memberScores: [{ studentId: 'student-1', score: 7.25, isOverridden: true }],
  };
  const selected = selectLatestOfficialEvaluation([
    { ...base, _id: 'draft', status: 'DRAFT', updatedAt: '2026-09-04T00:00:00Z' },
    { ...base, _id: 'older', status: 'SUBMITTED', updatedAt: '2026-09-01T00:00:00Z' },
    { ...base, _id: 'newer', status: 'PUBLISHED', updatedAt: '2026-09-03T00:00:00Z' },
  ]);

  assert.equal(selected?._id, 'newer');
  assert.deepEqual(resolveEvaluationMemberScore(selected, 'student-1'), { score: 7.25, isOverridden: true });
  assert.deepEqual(resolveEvaluationMemberScore(selected, 'student-2'), { score: null, isOverridden: false });
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

test('ClassManagement requests five complete rows for its three-column grid', () => {
  assert.equal(CLASS_LIST_PAGE_SIZE, 15);
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
    links: [
      { _id: 'link', versionNumber: 3, name: 'Demo', url: 'https://demo.example.com', submittedAt: '2026-09-03T00:00:00Z', submittedBy: { _id: 'student', name: 'Student' } },
    ],
  }]);

  assert.equal(result.checkpoints.length, 2);
  assert.equal(result.checkpoints[0].icon, 'Users');
  assert.equal(result.checkpoints[1].icon, 'BarChart2');
  assert.deepEqual(result.stats[1], {
    count: 2,
    linkCount: 1,
    latest: {
      _id: 'latest',
      originalName: 'latest.pdf',
      fileType: 'pdf',
      fileSize: 20,
      uploadedAt: '2026-09-02T00:00:00Z',
    },
    latestVersion: 3,
    reqFilled: 1,
    reqTotal: 2,
    status: 'Draft',
    submittedAt: undefined,
  });
  assert.deepEqual(result.stats[2], {
    count: 0,
    linkCount: 0,
    latest: null,
    latestVersion: null,
    reqFilled: 0,
    reqTotal: 1,
    status: 'NotSubmitted',
    submittedAt: null,
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
  const rejectedProfileChange = {
    ...approved,
    reviews: [{ id: 'review-3', toStatus: 'Rejected' }],
  };

  assert.equal(hasProjectDirectionChanged(submitted, { ...submitted }), false);
  assert.equal(hasProjectDirectionChanged(submitted, approved), true);
  assert.equal(getProjectDirectionDecisionNotice(submitted, approved), 'Lecturer approved your project direction.');
  assert.equal(getProjectDirectionDecisionNotice(submitted, needsRevision), 'Lecturer reviewed your project direction and requested changes.');
  assert.equal(getProjectDirectionDecisionNotice(approved, needsRevision), '');
  assert.equal(isProjectProfileAvailable(submitted), false);
  assert.equal(isProjectProfileAvailable(needsRevision), false);
  assert.equal(isProjectProfileAvailable(approved), true);

  const pendingProfileChange = { ...submitted, isProjectProfileChangeProposal: true };
  assert.equal(
    getProjectDirectionDecisionNotice(pendingProfileChange, approved),
    'Lecturer approved your Project Profile changes. The approved profile is now updated.',
  );
  assert.equal(
    getProjectDirectionDecisionNotice(pendingProfileChange, needsRevision),
    'Lecturer requested revisions to your Project Profile changes. The approved profile remains unchanged.',
  );
  assert.equal(
    getProjectDirectionDecisionNotice(pendingProfileChange, rejectedProfileChange),
    'Lecturer rejected your Project Profile changes. The approved profile remains unchanged.',
  );
});

test('imported project metadata seeds a missing Project Direction draft', () => {
  assert.deepEqual(getProjectDirectionDraftSeed(null, {
    projectName: 'SnapPose',
    description: 'An imported project description.',
  }), {
    title: 'SnapPose',
    summary: 'An imported project description.',
  });
  assert.deepEqual(getProjectDirectionDraftSeed({
    title: 'Saved direction',
    summary: 'Saved direction summary.',
  }, {
    projectName: 'Imported project',
    description: 'Imported description.',
  }), {
    title: 'Saved direction',
    summary: 'Saved direction summary.',
  });
});

test('lecturer overview applies a newer Project Profile change request to an approved team', () => {
  const profileChange = {
    id: 'direction-1',
    status: 'Submitted',
    rowVersion: '12',
    isProjectProfileChangeProposal: true,
  };

  assert.equal(shouldApplyProjectDirectionSubmission('APPROVED', '11', profileChange), true);
  assert.equal(shouldApplyProjectDirectionSubmission('APPROVED', '12', profileChange), false);
  assert.equal(shouldApplyProjectDirectionSubmission('APPROVED', '11', {
    ...profileChange,
    isProjectProfileChangeProposal: false,
  }), false);
  assert.equal(shouldApplyProjectDirectionSubmission('CHANGES_REQUESTED', '11', profileChange), true);
  assert.equal(shouldApplyProjectDirectionSubmission('PENDING', '11', profileChange), false);
});

test('approved Project Profile displays the values from the latest realtime decision', () => {
  const staleWorkspaceProject = {
    projectName: 'EHUB',
    description: 'The previously approved description.',
  };
  const approvedDirection = {
    status: 'Approved',
    title: 'SMEP',
    summary: 'The newly approved description.',
  };

  assert.deepEqual(getApprovedProjectProfileDisplay(approvedDirection, staleWorkspaceProject), {
    projectName: 'SMEP',
    description: 'The newly approved description.',
  });
  assert.deepEqual(getApprovedProjectProfileDisplay(
    { ...approvedDirection, status: 'Submitted' },
    staleWorkspaceProject,
  ), staleWorkspaceProject);
});

test('project direction requires requested revisions to be changed and saved before submit', () => {
  const needsRevision = {
    id: 'direction-1',
    title: 'Initial direction',
    summary: 'The initial project direction summary.',
    startupIndustries: ['Healthcare / HealthTech', 'Education / EdTech'],
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

  const revisedIndustries = ['Healthcare / HealthTech'];
  assert.equal(hasUnsavedProjectDirectionChanges(
    needsRevision,
    needsRevision.title,
    needsRevision.summary,
    revisedIndustries,
  ), true);
  assert.equal(canSubmitProjectDirection(
    needsRevision,
    needsRevision.title,
    needsRevision.summary,
    revisedIndustries,
  ), false);

  const savedDraft = { ...needsRevision, summary: revisedSummary, status: 'Draft', rowVersion: '12' };
  assert.equal(canSubmitProjectDirection(savedDraft, savedDraft.title, savedDraft.summary), true);
  assert.equal(getProjectDirectionSubmitGuidance(savedDraft, savedDraft.title, savedDraft.summary), '');

  assert.equal(canSubmitProjectDirection(savedDraft, savedDraft.title, `${savedDraft.summary} Unsaved`), false);
  assert.match(getProjectDirectionSubmitGuidance(savedDraft, savedDraft.title, `${savedDraft.summary} Unsaved`), /Save your changes/);
});

test('project direction recognizes both backend and mock concurrency error codes', () => {
  assert.equal(isProjectDirectionConcurrencyConflict('CLASS_CONCURRENCY_CONFLICT'), true);
  assert.equal(isProjectDirectionConcurrencyConflict('PROJECT_DIRECTION_CONCURRENCY_CONFLICT'), true);
  assert.equal(isProjectDirectionConcurrencyConflict('PROJECT_DIRECTION_STATE_INVALID'), false);
});

test('lecturer review updates only the reviewed team without reloading the overview', () => {
  const firstTeam = { _id: 'team-1', projectDirectionStatus: 'PENDING', projectDirectionRowVersion: '10' };
  const secondTeam = { _id: 'team-2', projectDirectionStatus: 'PENDING', projectDirectionRowVersion: '20' };
  const result = updateProjectDirectionOverviewTeams([firstTeam, secondTeam], 'team-1', {
    title: 'Approved direction',
    summary: 'Approved summary',
    status: 'Approved',
    rowVersion: '11',
    startupIndustries: ['Healthcare / HealthTech', 'Education / EdTech'],
    reviews: [{ id: 'review-1', toStatus: 'Approved', comment: 'Proceed with this scope.' }],
  });

  assert.deepEqual(result[0], {
    _id: 'team-1',
    projectDirectionStatus: 'APPROVED',
    projectDirectionRowVersion: '11',
    projectDirection: 'Approved summary',
    projectDirectionTitle: 'Approved direction',
    projectDirectionIsProfileChangeProposal: false,
    projectDirectionCurrentTitle: '',
    projectDirectionCurrentSummary: '',
    projectDirectionStartupIndustries: ['Healthcare / HealthTech', 'Education / EdTech'],
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

  const profileChangeResult = updateProjectDirectionOverviewTeams(submittedResult, 'team-2', {
    title: 'Proposed project name',
    summary: 'Proposed project description with enough detail.',
    currentTitle: 'Approved project name',
    currentSummary: 'The currently approved project description.',
    isProjectProfileChangeProposal: true,
    status: 'Submitted',
    rowVersion: '22',
    reviews: [],
  });
  assert.equal(profileChangeResult[1].projectDirectionIsProfileChangeProposal, true);
  assert.equal(profileChangeResult[1].projectDirectionCurrentTitle, 'Approved project name');
  assert.equal(profileChangeResult[1].projectDirectionTitle, 'Proposed project name');
});

test('project direction overview defaults to all assigned classes and preserves valid deep-links', () => {
  const classIds = ['class-1', 'class-2'];

  assert.equal(resolveDirectionOverviewClassId(classIds, '', ''), '');
  assert.deepEqual(directionOverviewTargetClassIds(classIds, ''), classIds);
  assert.equal(resolveDirectionOverviewClassId(classIds, 'class-2', ''), 'class-2');
  assert.deepEqual(directionOverviewTargetClassIds(classIds, 'class-2'), ['class-2']);
  assert.equal(resolveDirectionOverviewClassId(classIds, 'missing-class', ''), '');
});
