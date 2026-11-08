import assert from 'node:assert/strict';
import test from 'node:test';
import type { TeamFeedbackCheckpoint } from '../src/types/teamFeedbackOverview.ts';
import {
  countFeedbackItems,
  defaultTeamFeedbackFilters,
  filterFeedbackCheckpoints,
} from '../src/utils/teamFeedbackOverview.ts';

const author = (name: string, role: string) => ({ id: name, name, role });

const checkpoints = (): TeamFeedbackCheckpoint[] => [
  {
    checkpointId: 'cp-1',
    checkpointNumber: 1,
    title: 'Problem',
    threads: [{
      id: 't1',
      content: 'Please clarify the target customer',
      isTechnicalReview: false,
      resolved: false,
      createdAt: '2026-10-01T00:00:00Z',
      submissionVersion: 1,
      author: author('Lê Giảng', 'Lecturer'),
      replies: [{ id: 'r1', content: 'Done, updated', createdAt: '2026-10-02T00:00:00Z', author: author('Sinh Viên', 'Student') }],
    }],
    evaluations: [{
      id: 'e1',
      status: 'Published',
      evaluator: author('Lê Giảng', 'Lecturer'),
      overallFeedback: 'Solid work',
      strengths: null,
      weaknesses: null,
      suggestions: 'Add market size',
      submittedAt: null,
      publishedAt: '2026-10-03T00:00:00Z',
      criterionComments: [{ criterionName: 'Problem fit', comment: 'Good fit' }],
    }],
  },
  { checkpointId: 'cp-2', checkpointNumber: 2, title: 'Solution', threads: [], evaluations: [] },
];

test('default filters keep every checkpoint, including the ones without feedback', () => {
  const result = filterFeedbackCheckpoints(checkpoints(), defaultTeamFeedbackFilters);
  assert.equal(result.length, 2);
  assert.equal(countFeedbackItems(result), 2);
});

test('source filter keeps only comments or only evaluations', () => {
  const comments = filterFeedbackCheckpoints(checkpoints(), { ...defaultTeamFeedbackFilters, source: 'comments' });
  assert.equal(comments.length, 1);
  assert.equal(comments[0].threads.length, 1);
  assert.equal(comments[0].evaluations.length, 0);

  const evaluations = filterFeedbackCheckpoints(checkpoints(), { ...defaultTeamFeedbackFilters, source: 'evaluations' });
  assert.equal(evaluations[0].threads.length, 0);
  assert.equal(evaluations[0].evaluations.length, 1);
});

test('checkpoint filter keeps the selected checkpoint even when it has no feedback', () => {
  const result = filterFeedbackCheckpoints(checkpoints(), { ...defaultTeamFeedbackFilters, checkpointId: 'cp-2' });
  assert.deepEqual(result.map((item) => item.checkpointId), ['cp-2']);
});

test('search matches replies, authors and criteria comments without Vietnamese accents', () => {
  const byReply = filterFeedbackCheckpoints(checkpoints(), { ...defaultTeamFeedbackFilters, query: 'updated' });
  assert.equal(byReply[0].threads.length, 1);
  assert.equal(byReply[0].evaluations.length, 0);

  const byAuthor = filterFeedbackCheckpoints(checkpoints(), { ...defaultTeamFeedbackFilters, query: 'le giang' });
  assert.equal(countFeedbackItems(byAuthor), 2);

  const byCriterion = filterFeedbackCheckpoints(checkpoints(), { ...defaultTeamFeedbackFilters, query: 'problem fit' });
  assert.equal(byCriterion[0].threads.length, 0);
  assert.equal(byCriterion[0].evaluations.length, 1);
});

test('an active filter drops checkpoints that end up without feedback', () => {
  const result = filterFeedbackCheckpoints(checkpoints(), { ...defaultTeamFeedbackFilters, query: 'nothing matches this' });
  assert.equal(result.length, 0);
});
