import assert from 'node:assert/strict';
import test from 'node:test';
import { IMPORT_STATUS_META, countImportRows, filterImportRows, importRowCategory } from '../src/utils/mentorImportStatus.ts';

const row = (status: string, isValid = true) => ({ status, isValid });

test('every kind of row falls in one category', () => {
  assert.equal(importRowCategory(row('Create')), 'create');
  assert.equal(importRowCategory(row('Update')), 'update');
  assert.equal(importRowCategory(row('CompleteIncomplete')), 'complete');
  assert.equal(importRowCategory(row('NeedsCompletion')), 'incomplete');
  assert.equal(importRowCategory(row('UpdateIncomplete')), 'incomplete');
  assert.equal(importRowCategory(row('Conflict', false)), 'invalid');
  assert.equal(importRowCategory(row('Create', false)), 'invalid', 'an invalid row is an error whatever its status was');
});

test('rows are counted per category and the filter keeps only the chosen one', () => {
  const rows = [row('Create'), row('Update'), row('Update'), row('NeedsCompletion'), row('Conflict', false), row('CompleteIncomplete')];
  assert.deepEqual(countImportRows(rows), { all: 6, create: 1, complete: 1, update: 2, incomplete: 1, invalid: 1 });
  assert.equal(filterImportRows(rows, 'update').length, 2);
  assert.equal(filterImportRows(rows, 'invalid').length, 1);
  assert.equal(filterImportRows(rows, 'all').length, 6);
  assert.notEqual(filterImportRows(rows, 'all'), rows, 'a copy is returned');
});

test('statuses that look alike in the file do not share a tone unless they mean the same thing', () => {
  assert.equal(IMPORT_STATUS_META.create.tone, IMPORT_STATUS_META.complete.tone);
  assert.notEqual(IMPORT_STATUS_META.update.tone, IMPORT_STATUS_META.incomplete.tone);
  assert.equal(IMPORT_STATUS_META.invalid.tone, 'danger');
});
