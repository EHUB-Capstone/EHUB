import assert from 'node:assert/strict';
import test from 'node:test';
import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../src/api/axiosClient.ts';
import { mentorAdminApi } from '../src/api/mentorAdminApi.ts';
import type {
  MentorAllocationEdit,
  MentorAllocationMentorLoad,
  MentorAllocationPreview,
  MentorAllocationRowPreview,
} from '../src/types/mentorAdmin.ts';
import type { TeamGridRow } from '../src/utils/mentorAllocationPreview.ts';
import {
  ALL_CLASSES,
  buildTeamGrid,
  filterTeamGrid,
  getAiReviewAssignments,
  resolveClassFilter,
  teamClassOptions,
  findEdit,
  getMentorCandidates,
  getSubjectColumns,
  isReplaceReasonValid,
  keepAppliedEdits,
  removeEdit,
  summarizePreview,
  upsertEdit,
} from '../src/utils/mentorAllocationPreview.ts';

const row = (overrides: Partial<MentorAllocationRowPreview>): MentorAllocationRowPreview => ({
  teamId: 't1',
  teamCode: 'T1',
  teamName: 'Team 1',
  classId: 'c1',
  classCode: 'EXE201_1',
  mentorType: 'Enterprise',
  mentorProfileId: 'm1',
  mentorName: 'Mentor 1',
  mentorEmail: 'm1@example.com',
  resultingSemesterLoad: 1,
  source: 'Allocated',
  ...overrides,
});

const load = (overrides: Partial<MentorAllocationMentorLoad>): MentorAllocationMentorLoad => ({
  mentorProfileId: 'm1',
  mentorName: 'Mentor 1',
  mentorEmail: 'm1@example.com',
  mentorType: 'Enterprise',
  contractType: 'Khoán',
  subjects: [],
  totalBefore: 0,
  totalAfter: 0,
  ...overrides,
});

const preview = (overrides: Partial<MentorAllocationPreview> = {}): MentorAllocationPreview => ({
  sessionId: 's1',
  semesterId: 'sem',
  seed: 1,
  teamCount: 2,
  missingEnterpriseCount: 0,
  missingAcademicCount: 0,
  canCommit: true,
  warnings: [],
  assignments: [],
  ...overrides,
});

test('buildTeamGrid shows existing, retained, allocated, manual and unfilled slots per team', () => {
  const grid = buildTeamGrid(preview({
    existingAssignments: [
      { assignmentId: 'a1', teamId: 't1', teamCode: 'T1', teamName: 'Team 1', classId: 'c1', classCode: 'EXE201_1', subjectCode: 'EXE201', mentorType: 'Academic', mentorProfileId: 'm9', mentorName: 'Existing Academic', replaced: false },
    ],
    assignments: [
      row({ teamId: 't1', mentorType: 'Enterprise', source: 'Retained', mentorProfileId: 'm1', mentorName: 'Kept' }),
      row({ teamId: 't2', teamCode: 'T2', teamName: 'Team 2', mentorType: 'Enterprise', source: 'Manual', mentorProfileId: 'm2', mentorName: 'By hand' }),
      row({ teamId: 't2', teamCode: 'T2', teamName: 'Team 2', mentorType: 'Academic', source: 'Allocated', mentorProfileId: 'm3', mentorName: 'Chosen' }),
    ],
  }));

  assert.equal(grid.length, 2);
  const [first, second] = grid;
  assert.equal(first.teamId, 't1');
  assert.equal(first.subjectCode, 'EXE201');
  assert.equal(first.enterprise.state, 'retained');
  assert.equal(first.academic.state, 'existing');
  assert.equal(first.academic.mentorName, 'Existing Academic');
  assert.equal(first.academic.currentAssignmentId, 'a1');
  assert.equal(second.enterprise.state, 'manual');
  assert.equal(second.academic.state, 'allocated');
});

test('buildTeamGrid marks a slot whose mentor is replaced and keeps the mentor being replaced', () => {
  const grid = buildTeamGrid(preview({
    existingAssignments: [
      { assignmentId: 'a1', teamId: 't1', teamCode: 'T1', teamName: 'Team 1', classId: 'c1', classCode: 'EXE201_1', subjectCode: 'EXE201', mentorType: 'Enterprise', mentorProfileId: 'old', mentorName: 'Old Mentor', replaced: true },
    ],
    assignments: [
      row({ teamId: 't1', mentorType: 'Enterprise', source: 'Manual', mentorProfileId: 'new', mentorName: 'New Mentor', replacesAssignmentId: 'a1', replacesMentorProfileId: 'old', replacesMentorName: 'Old Mentor' }),
    ],
  }));

  const slot = grid[0].enterprise;
  assert.equal(slot.state, 'manual');
  assert.equal(slot.mentorName, 'New Mentor');
  assert.equal(slot.replacesCurrent, true);
  assert.equal(slot.currentMentorName, 'Old Mentor');
  assert.equal(slot.currentAssignmentId, 'a1');
});

test('buildTeamGrid lists teams that only appear as unfilled and sorts classes and teams naturally', () => {
  const grid = buildTeamGrid(preview({
    assignments: [row({ teamId: 't10', teamCode: 'T10', classCode: 'EXE201_1' })],
    unfilled: [
      { teamId: 't2', teamCode: 'T2', teamName: 'Team 2', classId: 'c1', classCode: 'EXE201_1', subjectCode: 'EXE201', mentorType: 'Academic' },
      { teamId: 't1', teamCode: 'T1', teamName: 'Team 1', classId: 'c0', classCode: 'EXE101_1', subjectCode: 'EXE101', mentorType: 'Enterprise' },
    ],
  }));

  assert.deepEqual(grid.map(item => item.teamCode), ['T1', 'T2', 'T10']);
  assert.equal(grid[0].enterprise.state, 'unfilled');
  assert.equal(grid[1].academic.state, 'unfilled');
});

test('summarizePreview counts rows by source and reads the server counters', () => {
  const summary = summarizePreview(preview({
    teamCount: 5,
    replacementCount: 1,
    unfilledEnterpriseCount: 2,
    unfilledAcademicCount: 3,
    assignments: [
      row({ source: 'Retained' }), row({ source: 'Retained' }), row({ source: 'Manual' }), row({ source: 'Allocated' }), row({ source: undefined }),
    ],
  }));

  assert.deepEqual(summary, { teams: 5, retained: 2, allocated: 2, manual: 1, replacements: 1, unfilledEnterprise: 2, unfilledAcademic: 3 });
});

test('getMentorCandidates keeps the matching mentor type and puts the lightest load first', () => {
  const candidates = getMentorCandidates(preview({
    mentorLoads: [
      load({ mentorProfileId: 'a', mentorName: 'Busy', totalAfter: 4 }),
      load({ mentorProfileId: 'b', mentorName: 'Light', totalAfter: 1 }),
      load({ mentorProfileId: 'c', mentorName: 'Academic', mentorType: 'Academic', totalAfter: 0 }),
    ],
  }), 'Enterprise');

  assert.deepEqual(candidates.map(item => item.mentorProfileId), ['b', 'a']);
});

test('getSubjectColumns returns each subject once in natural order', () => {
  const columns = getSubjectColumns(preview({
    mentorLoads: [
      load({ subjects: [{ subjectCode: 'EXE201', before: 1, added: 0 }, { subjectCode: 'EXE101', before: 0, added: 1 }] }),
      load({ mentorProfileId: 'b', subjects: [{ subjectCode: 'EXE201', before: 2, added: 0 }] }),
    ],
  }));

  assert.deepEqual(columns, ['EXE101', 'EXE201']);
});

test('isReplaceReasonValid enforces the 3 to 1000 character rule after trimming', () => {
  assert.equal(isReplaceReasonValid(undefined), false);
  assert.equal(isReplaceReasonValid('  ab  '), false);
  assert.equal(isReplaceReasonValid('abc'), true);
  assert.equal(isReplaceReasonValid('x'.repeat(1000)), true);
  assert.equal(isReplaceReasonValid('x'.repeat(1001)), false);
});

test('upsertEdit replaces the earlier edit of the same slot and removeEdit drops it', () => {
  const first: MentorAllocationEdit = { teamId: 't1', mentorType: 'Enterprise', mentorProfileId: 'm1' };
  const second: MentorAllocationEdit = { teamId: 't1', mentorType: 'Enterprise', mentorProfileId: 'm2', replace: true, reason: 'Swap' };
  const other: MentorAllocationEdit = { teamId: 't1', mentorType: 'Academic', mentorProfileId: 'm3' };

  const edits = upsertEdit(upsertEdit([first], other), second);

  assert.equal(edits.length, 2);
  assert.equal(findEdit(edits, 't1', 'Enterprise')?.mentorProfileId, 'm2');
  assert.equal(findEdit(edits, 't1', 'Academic')?.mentorProfileId, 'm3');
  assert.deepEqual(removeEdit(edits, 't1', 'Enterprise'), [other]);
});

test('keepAppliedEdits keeps applied and leave-empty edits and drops rejected or unresolved ones', () => {
  const applied: MentorAllocationEdit = { teamId: 't1', mentorType: 'Enterprise', mentorProfileId: 'm1' };
  const leaveEmpty: MentorAllocationEdit = { teamId: 't1', mentorType: 'Academic', mentorProfileId: null };
  const rejected: MentorAllocationEdit = { teamId: 't2', mentorType: 'Enterprise', mentorProfileId: 'm9' };
  const unknownTeam: MentorAllocationEdit = { teamId: 'gone', mentorType: 'Enterprise', mentorProfileId: 'm1' };

  const kept = keepAppliedEdits([applied, leaveEmpty, rejected, unknownTeam], preview({
    assignments: [
      row({ teamId: 't1', mentorType: 'Enterprise', source: 'Manual', mentorProfileId: 'm1' }),
      row({ teamId: 't2', teamCode: 'T2', mentorType: 'Enterprise', source: 'Allocated', mentorProfileId: 'm5' }),
    ],
    unfilled: [{ teamId: 't1', teamCode: 'T1', teamName: 'Team 1', classId: 'c1', classCode: 'EXE201_1', subjectCode: 'EXE201', mentorType: 'Academic' }],
  }));

  assert.deepEqual(kept, [applied, leaveEmpty]);
});

const gridRow = (teamId: string, classCode: string, enterprise: string, academic: string): TeamGridRow => ({
  teamId, teamCode: teamId, teamName: `Team ${teamId}`, classCode, subjectCode: 'EXE201',
  enterprise: { state: enterprise, mentorName: enterprise === 'unfilled' ? null : 'Mentor E' },
  academic: { state: academic, mentorName: academic === 'unfilled' ? null : 'Mentor A' },
} as unknown as TeamGridRow);

test('the class filter lists each class once with its team count, in natural order', () => {
  const grid = [gridRow('1', 'EXE201_10', 'existing', 'existing'), gridRow('2', 'EXE201_2', 'existing', 'existing'), gridRow('3', 'EXE201_2', 'existing', 'existing')];
  assert.deepEqual(teamClassOptions(grid), [{ classCode: 'EXE201_2', teamCount: 2 }, { classCode: 'EXE201_10', teamCount: 1 }]);
});

test('filtering by class combines with the other filters and the search', () => {
  const grid = [
    gridRow('1', 'EXE101_1', 'unfilled', 'existing'),
    gridRow('2', 'EXE201_1', 'allocated', 'existing'),
    gridRow('3', 'EXE201_1', 'unfilled', 'unfilled'),
  ];
  const ids = (classCode: string, filter: 'all' | 'attention' | 'changed' = 'all', search = '') =>
    filterTeamGrid(grid, { classCode, filter, search }).map(item => item.teamId);
  assert.deepEqual(ids(ALL_CLASSES), ['1', '2', '3']);
  assert.deepEqual(ids('EXE201_1'), ['2', '3']);
  assert.deepEqual(ids('EXE201_1', 'attention'), ['3']);
  assert.deepEqual(ids('EXE201_1', 'changed'), ['2', '3']);
  assert.deepEqual(ids('EXE101_1', 'changed'), ['1']);
  assert.deepEqual(ids('EXE201_1', 'all', 'team 2'), ['2']);
  assert.deepEqual(ids('EXE999'), []);
});

test('a class that disappeared after regenerating falls back to every class', () => {
  const options = [{ classCode: 'EXE201_1' }];
  assert.equal(resolveClassFilter('EXE201_1', options), 'EXE201_1');
  assert.equal(resolveClassFilter('EXE101_1', options), ALL_CLASSES);
  assert.equal(resolveClassFilter(ALL_CLASSES, options), ALL_CLASSES);
});

test('AI evidence stays attached to its proposed mentor slot, including a score of zero', () => {
  const reasons = ['Previously guided two teams using React', 'Current semester load: 2 teams'];
  const grid = buildTeamGrid(preview({
    strategy: 'Ai',
    assignments: [
      row({ fitScore: 0, reasons }),
      row({ mentorType: 'Academic', mentorProfileId: 'academic', source: 'Manual' }),
    ],
  }));
  assert.equal(grid[0].enterprise.fitScore, 0);
  assert.deepEqual(grid[0].enterprise.reasons, reasons);
  assert.equal(grid[0].academic.fitScore, undefined);
  assert.equal(grid[0].academic.reasons, undefined);
});

test('only new zero-score AI suggestions need evidence review', () => {
  const assignments = [
    row({ teamId: 'zero', fitScore: 0 }),
    row({ teamId: 'default-source', source: undefined, fitScore: 0 }),
    row({ teamId: 'manual', source: 'Manual', fitScore: 0 }),
    row({ teamId: 'retained', source: 'Retained', fitScore: 0 }),
    row({ teamId: 'positive', fitScore: 0.1 }),
    row({ teamId: 'missing-score' }),
    row({ teamId: 'null-score', fitScore: null }),
  ];
  const suggested = preview({ strategy: 'Ai', assignments });

  assert.deepEqual(getAiReviewAssignments(suggested).map(item => item.teamId), ['zero', 'default-source']);
  const grid = buildTeamGrid(suggested);
  assert.equal(grid.find(item => item.teamId === 'zero')?.enterprise.needsReview, true);
  assert.equal(grid.find(item => item.teamId === 'default-source')?.enterprise.needsReview, true);
  for (const teamId of ['manual', 'retained', 'positive', 'missing-score', 'null-score']) {
    assert.equal(grid.find(item => item.teamId === teamId)?.enterprise.needsReview, false);
  }

  for (const strategy of ['Balanced', 'Random', undefined] as const) {
    const otherStrategy = preview({ strategy, assignments });
    assert.deepEqual(getAiReviewAssignments(otherStrategy), []);
    assert.equal(buildTeamGrid(otherStrategy).some(item => item.enterprise.needsReview), false);
  }
});

test('attention filter includes filled AI suggestions that need evidence review', () => {
  const grid = buildTeamGrid(preview({
    strategy: 'Ai',
    assignments: [
      row({ teamId: 'review', fitScore: 0 }),
      row({ teamId: 'review', mentorType: 'Academic', source: 'Manual' }),
      row({ teamId: 'supported', fitScore: 5 }),
      row({ teamId: 'supported', mentorType: 'Academic', source: 'Manual' }),
    ],
  }));

  assert.equal(grid.every(item => item.enterprise.state !== 'unfilled' && item.academic.state !== 'unfilled'), true);
  assert.deepEqual(filterTeamGrid(grid, { filter: 'attention', classCode: ALL_CLASSES, search: '' }).map(item => item.teamId), ['review']);
  assert.equal(filterTeamGrid(grid, { filter: 'attention', classCode: 'EXE101_1', search: '' }).length, 0);
});

test('the AI preview keeps the history and achievement score breakdown on the correct mentor slot', () => {
  const history = { baseFitScore: 80, completedSessionCount: 10, mentoredTeamCount: 2,
    achievementCount: 2, historyBonus: 7, achievementBonus: 4 };
  const grid = buildTeamGrid(preview({ strategy: 'Ai', assignments: [
    row({ fitScore: 91, history }),
    row({ mentorType: 'Academic', mentorProfileId: 'other', fitScore: 0,
      history: { ...history, baseFitScore: 0, historyBonus: 0, achievementBonus: 0 } }),
  ] }));
  assert.equal(grid[0].enterprise.fitScore, 91);
  assert.deepEqual(grid[0].enterprise.history, history);
  assert.equal(grid[0].academic.history?.achievementBonus, 0);
  assert.equal(grid[0].academic.needsReview, true);
});

test('AI client keeps class scope, seed and manual edits, and saves only on explicit acceptance', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    const suggested = preview({ sessionId: 'ai-session', strategy: 'Ai' });
    mock.onPost('/admin/mentors/allocations/preview').reply(200, { success: true, data: suggested });
    mock.onPost('/admin/mentors/allocations/commit').reply(200, { success: true, data: { createdCount: 1, skippedCount: 0 } });
    const edit: MentorAllocationEdit = { teamId: 't1', mentorType: 'Enterprise', mentorProfileId: 'm2' };
    const response = await mentorAdminApi.previewAllocation('sem', 42, 'Ai', [edit], false, ['class-1']);

    assert.equal(response.data.sessionId, 'ai-session');
    assert.equal(mock.history.post.length, 1);
    assert.equal(mock.history.post[0].timeout, 60_000);
    assert.deepEqual(JSON.parse(mock.history.post[0].data), {
      semesterId: 'sem', classIds: ['class-1'], seed: 42, strategy: 'Ai', edits: [edit], includeTemporaryMentors: false,
    });

    await mentorAdminApi.commitAllocation(response.data.sessionId);
    assert.equal(mock.history.post.length, 2);
    assert.equal(mock.history.post[1].url, '/admin/mentors/allocations/commit');
    assert.deepEqual(JSON.parse(mock.history.post[1].data), { sessionId: 'ai-session' });
  } finally {
    mock.restore();
  }
});

test('semester AI client uses an empty class scope and supports cancelling a pending preview', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    mock.onPost('/admin/mentors/allocations/preview').reply(200, { success: true, data: preview({ strategy: 'Ai' }) });
    await mentorAdminApi.previewAllocation('sem', undefined, 'Ai');
    const request = JSON.parse(mock.history.post[0].data);
    assert.deepEqual(request.classIds, []);
    assert.equal(request.seed, undefined);
    assert.equal(request.strategy, 'Ai');

    const controller = new AbortController();
    controller.abort();
    await assert.rejects(
      mentorAdminApi.previewAllocation('sem', undefined, 'Ai', [], true, ['class-1'], controller.signal),
      (error: unknown) => (error as { code?: string }).code === 'ERR_CANCELED',
    );
    assert.equal(mock.history.post.length, 1);
  } finally {
    mock.restore();
  }
});
