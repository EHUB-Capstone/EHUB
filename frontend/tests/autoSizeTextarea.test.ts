import assert from 'node:assert/strict';
import test from 'node:test';
import { autoSizeHeight, shouldSubmitOnEnter } from '../src/utils/autoSizeTextarea.ts';

// 20px lines, 12px vertical padding, 2px border.
const base = { lineHeight: 20, verticalPadding: 12, verticalBorder: 2, minRows: 2, maxRows: 5 };

test('empty or short content keeps the minimum height without a scrollbar', () => {
  // scrollHeight of one line = 20 + 12 padding
  assert.deepEqual(autoSizeHeight({ ...base, scrollHeight: 32 }), { height: 54, overflowY: 'hidden' });
  assert.deepEqual(autoSizeHeight({ ...base, scrollHeight: 0 }), { height: 54, overflowY: 'hidden' });
});

test('the box grows with the content between min and max rows', () => {
  // three lines = 60 + 12 padding, plus the 2px border
  assert.deepEqual(autoSizeHeight({ ...base, scrollHeight: 72 }), { height: 74, overflowY: 'hidden' });
});

test('content exactly at the maximum still shows everything without scrolling', () => {
  // five lines = 100 + 12 padding; max height = 100 + 12 + 2
  assert.deepEqual(autoSizeHeight({ ...base, scrollHeight: 112 }), { height: 114, overflowY: 'hidden' });
});

test('past the maximum the height stops growing and the box scrolls', () => {
  assert.deepEqual(autoSizeHeight({ ...base, scrollHeight: 400 }), { height: 114, overflowY: 'auto' });
});

test('a single-row input works and invalid row settings are corrected', () => {
  assert.deepEqual(autoSizeHeight({ ...base, minRows: 1, maxRows: 6, scrollHeight: 32 }), { height: 34, overflowY: 'hidden' });
  // maxRows lower than minRows must never shrink below the minimum
  assert.deepEqual(autoSizeHeight({ ...base, minRows: 3, maxRows: 1, scrollHeight: 400 }), { height: 74, overflowY: 'auto' });
  assert.equal(autoSizeHeight({ ...base, minRows: 0, maxRows: 4, scrollHeight: 0 }).height, 34);
});

test('Enter sends, Shift+Enter adds a line', () => {
  assert.equal(shouldSubmitOnEnter({ key: 'Enter', shiftKey: false, isComposing: false }), true);
  assert.equal(shouldSubmitOnEnter({ key: 'Enter', shiftKey: true, isComposing: false }), false);
});

test('Enter never sends while an input method is composing, and other keys never send', () => {
  assert.equal(shouldSubmitOnEnter({ key: 'Enter', shiftKey: false, isComposing: true }), false);
  assert.equal(shouldSubmitOnEnter({ key: 'a', shiftKey: false, isComposing: false }), false);
  assert.equal(shouldSubmitOnEnter({ key: 'Tab', shiftKey: false, isComposing: false }), false);
});
