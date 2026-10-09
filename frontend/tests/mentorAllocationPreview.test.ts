import assert from 'node:assert/strict';
import test from 'node:test';
import type {
  MentorAllocationEdit,
  MentorAllocationMentorLoad,
  MentorAllocationPreview,
  MentorAllocationRowPreview,
} from '../src/types/mentorAdmin.ts';
import {
  buildTeamGrid,
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
