import assert from 'node:assert/strict';
import test from 'node:test';
import { MENTOR_CHANGE_REASONS, buildChangeReason, isChangeReasonValid } from '../src/utils/mentorChangeReasons.ts';

test('every ready-made reason is valid on its own and nothing is valid without a choice', () => {
  for (const item of MENTOR_CHANGE_REASONS.filter(entry => entry.value !== 'other')) {
    assert.equal(isChangeReasonValid(item.value, ''), true, item.value);
  }
  assert.equal(isChangeReasonValid('', 'a long enough note'), false);
  assert.equal(isChangeReasonValid('unknown', ''), false);
});

test('"Other" needs a written note of at least 3 characters', () => {
  assert.equal(isChangeReasonValid('other', ''), false);
  assert.equal(isChangeReasonValid('other', '  ab '), false);
  assert.equal(isChangeReasonValid('other', 'Moved abroad'), true);
});

test('a note longer than 500 characters is rejected', () => {
  assert.equal(isChangeReasonValid('overloaded', 'x'.repeat(500)), true);
  assert.equal(isChangeReasonValid('overloaded', 'x'.repeat(501)), false);
});

test('the text sent to the server is the label, with the note appended when there is one', () => {
  assert.equal(buildChangeReason('overloaded', ''), 'Mentor is overloaded');
  assert.equal(buildChangeReason('overloaded', ' 6 teams '), 'Mentor is overloaded: 6 teams');
  assert.equal(buildChangeReason('other', ' Moved abroad '), 'Moved abroad');
});

test('every text built from a valid choice satisfies the server length rule of 3 to 1000 characters', () => {
  for (const item of MENTOR_CHANGE_REASONS) {
    const note = item.value === 'other' ? 'abc' : 'x'.repeat(500);
    const text = buildChangeReason(item.value, note);
    assert.ok(text.length >= 3 && text.length <= 1000, item.value);
  }
});
