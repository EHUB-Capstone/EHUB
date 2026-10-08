import assert from 'node:assert/strict';
import test from 'node:test';
import {
  MASTER_IMPORT_MAX_BYTES,
  countMasterImportChanges,
  describeMasterImportResult,
  emailCellText,
  summarizeMissingFields,
  validateMentorWorkbook,
} from '../src/utils/mentorMasterImport.ts';

const emptyResult = { createdCount: 0, updatedCount: 0, draftSavedCount: 0, draftCompletedCount: 0 };

test('validateMentorWorkbook accepts only non-empty .xlsx files within the size limit', () => {
  assert.equal(validateMentorWorkbook({ name: 'mentors.XLSX', size: 10 }), null);
  assert.match(validateMentorWorkbook({ name: 'mentors.xls', size: 10 })!, /xlsx/);
  assert.match(validateMentorWorkbook({ name: 'mentors.xlsx', size: 0 })!, /empty/);
  assert.match(validateMentorWorkbook({ name: 'mentors.xlsx', size: MASTER_IMPORT_MAX_BYTES + 1 })!, /5 MB/);
});

test('countMasterImportChanges counts new, updated and incomplete mentors', () => {
  assert.equal(countMasterImportChanges({ createCount: 3, updateCount: 2, needsCompletionCount: 4 }), 9);
});

test('describeMasterImportResult mentions Forgot Password only when accounts were created', () => {
  assert.match(describeMasterImportResult({ ...emptyResult, createdCount: 1 }), /1 new account,.*Forgot Password/);
  assert.doesNotMatch(describeMasterImportResult({ ...emptyResult, updatedCount: 4 }), /Forgot Password/);
});

test('describeMasterImportResult reports incomplete and completed mentors only when present', () => {
  assert.doesNotMatch(describeMasterImportResult(emptyResult), /incomplete|1 completed/);
  const text = describeMasterImportResult({ ...emptyResult, draftSavedCount: 2, draftCompletedCount: 1 });
  assert.match(text, /2 saved as incomplete/);
  assert.match(text, /1 completed/);
});

test('emailCellText shows a neutral hint when the file has no email', () => {
  assert.deepEqual(emailCellText(' a@b.vn '), { text: 'a@b.vn', provided: true });
  assert.deepEqual(emailCellText(''), { text: 'No email yet', provided: false });
  assert.deepEqual(emailCellText(null), { text: 'No email yet', provided: false });
});

test('summarizeMissingFields keeps the first few names and counts the rest', () => {
  assert.deepEqual(summarizeMissingFields(['Email', 'SDT', 'Công ty', 'Loại HĐ', 'Địa chỉ']), { count: 5, preview: ['Email', 'SDT', 'Công ty'], hiddenCount: 2 });
  assert.deepEqual(summarizeMissingFields([]), { count: 0, preview: [], hiddenCount: 0 });
});
