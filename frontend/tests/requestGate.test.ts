import assert from 'node:assert/strict';
import test from 'node:test';
import { createRequestGate } from '../src/utils/requestGate.ts';

test('the first response is applied even while newer duplicate requests are still running', () => {
  const gate = createRequestGate();
  const first = gate.begin('team-1:1');
  const second = gate.begin('team-1:1');
  const third = gate.begin('team-1:1');

  // Responses arrive 2.3s, 4.6s and 6.9s later: the screen must not wait for the last one.
  assert.equal(gate.accepts('team-1:1', first), true);
  gate.markApplied(first);
  assert.equal(gate.accepts('team-1:1', second), true);
  gate.markApplied(second);
  assert.equal(gate.accepts('team-1:1', third), true);
});

test('an older response arriving after a newer one was applied is ignored', () => {
  const gate = createRequestGate();
  const older = gate.begin('team-1:1');
  const newer = gate.begin('team-1:1');
  gate.markApplied(newer);
  assert.equal(gate.accepts('team-1:1', older), false);
});

test('a response for the previous checkpoint cannot overwrite the checkpoint now on screen', () => {
  const gate = createRequestGate();
  const checkpointOne = gate.begin('team-1:1');
  const checkpointTwo = gate.begin('team-1:2');
  assert.equal(gate.accepts('team-1:1', checkpointOne), false);
  assert.equal(gate.isActive('team-1:1'), false);
  assert.equal(gate.accepts('team-1:2', checkpointTwo), true);
  assert.equal(gate.isActive('team-1:2'), true);
});

test('a response for another team is ignored as well', () => {
  const gate = createRequestGate();
  const otherTeam = gate.begin('team-1:1');
  gate.begin('team-2:1');
  assert.equal(gate.accepts('team-1:1', otherTeam), false);
});

test('redundant calls share the request already running for the same key', () => {
  const gate = createRequestGate();
  const promise = Promise.resolve();
  assert.equal(gate.reuse('team-1:1'), null);
  gate.begin('team-1:1');
  gate.track('team-1:1', promise);
  assert.equal(gate.reuse('team-1:1'), promise);
  assert.equal(gate.reuse('team-1:2'), null);
});

test('once a request finishes it is no longer shared', () => {
  const gate = createRequestGate();
  const promise = Promise.resolve();
  gate.begin('team-1:1');
  gate.track('team-1:1', promise);
  gate.release(promise);
  assert.equal(gate.reuse('team-1:1'), null);
});

test('releasing an old request does not drop a newer one that replaced it', () => {
  const gate = createRequestGate();
  const old = Promise.resolve();
  const current = Promise.resolve();
  gate.begin('team-1:1');
  gate.track('team-1:1', old);
  gate.begin('team-1:1');
  gate.track('team-1:1', current);
  gate.release(old);
  assert.equal(gate.reuse('team-1:1'), current);
});

test('request numbers keep increasing so applied order stays meaningful across checkpoints', () => {
  const gate = createRequestGate();
  const first = gate.begin('team-1:1');
  gate.markApplied(first);
  const afterSwitch = gate.begin('team-1:2');
  assert.ok(afterSwitch > first);
  assert.equal(gate.accepts('team-1:2', afterSwitch), true);
});
