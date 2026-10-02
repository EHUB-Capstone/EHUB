import assert from 'node:assert/strict';
import test from 'node:test';
import { includeTeamsWithoutWorkspace, matchesSubmissionStatus, submissionKey } from '../src/utils/submissionAnalytics.ts';
import type { SubmissionAnalyticsItem } from '../src/types/submissionAnalytics.ts';
import type { EvaluationGradingRecord } from '../src/types/evaluationGrading.ts';
import { filterEvaluationRecords } from '../src/utils/evaluationGrading.ts';

const item: SubmissionAnalyticsItem = {
  teamId: 'team-1', teamName: 'Team one', classId: 'class-1', classCode: 'EXE201_8', courseCode: 'EXE201',
  semesterCode: 'FA2026', hasWorkspace: false, checkpointId: 'cp-1', checkpointNumber: 1, checkpointTitle: 'Idea',
  courseWeight: 50, deadlineUtc: '2026-10-01T00:00:00Z', submittedAtUtc: null, status: 'Missing',
};

test('not submitted includes missing while the missing filter selects only overdue pairs', () => {
  assert.equal(matchesSubmissionStatus(item, 'Missing'), true);
  assert.equal(matchesSubmissionStatus(item, 'NotSubmitted'), true);
  assert.equal(matchesSubmissionStatus({ ...item, status: 'NotSubmitted' }, 'Missing'), false);
  assert.equal(matchesSubmissionStatus({ ...item, status: 'Submitted' }, 'NotSubmitted'), false);
  assert.equal(matchesSubmissionStatus(undefined, 'Missing'), false);
  assert.equal(matchesSubmissionStatus(undefined, ''), true);
});

test('results include a missing team even when it has no project workspace', () => {
  const records = includeTeamsWithoutWorkspace([], [item], []);
  assert.equal(records.length, 1);
  assert.equal(records[0].team.hasWorkspace, false);
  assert.equal(records[0].team.teamName, 'Team one');
  assert.equal(records[0].evaluation, null);
  assert.equal(records[0].status, 'NOT_GRADED');
  assert.equal(records[0].checkpoint.courseWeight, 50);
});

test('submission filtering is independent from grading status and keeps class/checkpoint scope', () => {
  const records = includeTeamsWithoutWorkspace([], [item, { ...item, teamId: 'team-2', classId: 'class-2', checkpointNumber: 2 }], []);
  const scoped = filterEvaluationRecords(records, { classId: 'class-1', checkpoint: '1', status: 'NOT_GRADED' });
  assert.equal(scoped.length, 1);
  assert.equal(matchesSubmissionStatus(item, 'Missing'), true);
  assert.notEqual(submissionKey('team-1', 1), submissionKey('team-1', 2));
});

test('adding workspace placeholders does not duplicate records or hide evaluation load failures', () => {
  const record = includeTeamsWithoutWorkspace([], [item], [])[0];
  assert.deepEqual(includeTeamsWithoutWorkspace([record], [item], []), [record]);
  assert.deepEqual(includeTeamsWithoutWorkspace([], [{ ...item, hasWorkspace: true }], []), []);
  const evaluated: EvaluationGradingRecord = { ...record, team: { ...record.team, hasWorkspace: true }, status: 'SUBMITTED',
    evaluation: { _id: 'grade-1', lecturerId: { _id: 'lecturer', name: 'Lecturer' }, evaluatorRole: 'LECTURER',
      status: 'SUBMITTED', updatedAt: '2026-10-02T00:00:00Z', rubricScores: [] } };
  assert.equal(includeTeamsWithoutWorkspace([evaluated], [], [])[0], evaluated);
});
