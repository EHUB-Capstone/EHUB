import assert from 'node:assert/strict';
import test from 'node:test';
import {
  assignMentorToTeams,
  getSelectionState,
  keepEligibleSelection,
  setVisibleSelection,
  summarizeBatch,
  toggleSelection,
} from '../src/utils/mentorBatchAssignment.ts';

const teams = [
  { id: 't1', name: 'Team 1' },
  { id: 't2', name: 'Team 2' },
  { id: 't3', name: 'Team 3' },
];

test('assignMentorToTeams processes teams one at a time in order', async () => {
  const calls: string[] = [];
  let running = 0;
  let maxRunning = 0;

  const results = await assignMentorToTeams(teams, async (teamId) => {
    running += 1;
    maxRunning = Math.max(maxRunning, running);
    await new Promise(resolve => setTimeout(resolve, 1));
    calls.push(teamId);
    running -= 1;
  }, () => 'unused');

  assert.deepEqual(calls, ['t1', 't2', 't3']);
  assert.equal(maxRunning, 1);
  assert.ok(results.every(item => item.ok));
});

test('assignMentorToTeams keeps going after a team fails and reports the reason', async () => {
  const results = await assignMentorToTeams(teams, async (teamId) => {
    if (teamId === 't2') throw new Error('Team already has a mentor');
  }, error => (error as Error).message);

  assert.deepEqual(results.map(item => item.ok), [true, false, true]);
  assert.equal(results[1].error, 'Team already has a mentor');
  assert.equal(results[1].teamName, 'Team 2');
});

test('summarizeBatch reports success, partial success and total failure', () => {
  const ok = (id: string) => ({ teamId: id, teamName: id, ok: true });
  const bad = (id: string) => ({ teamId: id, teamName: id, ok: false, error: 'x' });

  assert.deepEqual(
    (({ tone, message }) => ({ tone, message }))(summarizeBatch([ok('a')])),
    { tone: 'success', message: 'Assigned to 1 team.' },
  );
  assert.deepEqual(
    (({ tone, message }) => ({ tone, message }))(summarizeBatch([ok('a'), ok('b'), bad('c')])),
    { tone: 'partial', message: 'Assigned to 2 teams, 1 failed.' },
  );
  const failure = summarizeBatch([bad('a'), bad('b')]);
  assert.equal(failure.tone, 'error');
  assert.equal(failure.message, 'Could not assign 2 teams.');
  assert.equal(failure.failed.length, 2);
});

test('toggleSelection adds and removes an id without mutating the input', () => {
  const selected = ['a'];
  assert.deepEqual(toggleSelection(selected, 'b'), ['a', 'b']);
  assert.deepEqual(toggleSelection(['a', 'b'], 'a'), ['b']);
  assert.deepEqual(selected, ['a']);
});

test('setVisibleSelection only touches the teams currently shown by the search filter', () => {
  assert.deepEqual(setVisibleSelection(['x'], ['a', 'b'], true), ['x', 'a', 'b']);
  assert.deepEqual(setVisibleSelection(['x', 'a', 'b'], ['a', 'b'], false), ['x']);
});

test('getSelectionState describes none, some and all of the visible teams', () => {
  assert.equal(getSelectionState([], ['a', 'b']), 'none');
  assert.equal(getSelectionState(['a'], ['a', 'b']), 'some');
  assert.equal(getSelectionState(['a', 'b', 'z'], ['a', 'b']), 'all');
  assert.equal(getSelectionState(['a'], []), 'none');
});

test('keepEligibleSelection drops teams that can no longer take the mentor', () => {
  assert.deepEqual(keepEligibleSelection(['a', 'b', 'c'], ['b', 'c', 'd']), ['b', 'c']);
});
