import assert from 'node:assert/strict';
import test from 'node:test';
import {
  MASTER_IMPORT_MAX_BYTES,
  countMasterImportChanges,
  describeImportAction,
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

test('describeMasterImportResult says how many teams switched from a temporary mentor to the new account', () => {
  assert.match(describeMasterImportResult({ ...emptyResult, createdCount: 1, temporaryAssignmentsConverted: 2 }), /2 teams switched from a temporary mentor/);
  assert.match(describeMasterImportResult({ ...emptyResult, temporaryAssignmentsConverted: 1 }), /1 team switched/);
  assert.doesNotMatch(describeMasterImportResult({ ...emptyResult, createdCount: 1 }), /switched/);
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

test('describeMasterImportResult says when semester entries moved onto the new account', () => {
  assert.match(describeMasterImportResult({ ...emptyResult, temporarySemestersConverted: 2 }), /2 semester entries moved onto the account/);
  assert.match(describeMasterImportResult({ ...emptyResult, temporarySemestersConverted: 1 }), /1 semester entry moved/);
  assert.doesNotMatch(describeMasterImportResult(emptyResult), /semester entr/);
});

test('countMasterImportChanges also counts mentors who get their account because the email arrived', () => {
  assert.equal(countMasterImportChanges({ createCount: 0, updateCount: 7, needsCompletionCount: 0, completeDraftCount: 48 }), 55);
});

test('the confirm button says what the import will do', () => {
  const counts = { createCount: 0, updateCount: 0, needsCompletionCount: 0, completeDraftCount: 0 };
  assert.equal(describeImportAction({ ...counts, updateCount: 54 }).label, 'Update 54 mentors');
  assert.equal(describeImportAction({ ...counts, updateCount: 1 }).label, 'Update 1 mentor');
  assert.equal(describeImportAction({ ...counts, createCount: 3 }).label, 'Create 3 accounts');
  assert.equal(describeImportAction({ ...counts, completeDraftCount: 2 }).label, 'Create 2 accounts');
  assert.equal(describeImportAction({ ...counts, needsCompletionCount: 4 }).label, 'Save 4 mentors as incomplete');
  assert.equal(describeImportAction({ ...counts, updateCount: 7, completeDraftCount: 48 }).label, 'Apply 55 changes');
  assert.equal(describeImportAction({ ...counts, createCount: 1, needsCompletionCount: 1 }).label, 'Apply 2 changes');
  assert.equal(describeImportAction(counts).label, 'Nothing to import');
});

test('the breakdown lists only the kinds of change that are present', () => {
  const counts = { createCount: 0, updateCount: 0, needsCompletionCount: 0, completeDraftCount: 0 };
  assert.equal(describeImportAction({ ...counts, updateCount: 7, completeDraftCount: 48 }).breakdown, '48 completed with an email · 7 updated');
  assert.equal(describeImportAction({ ...counts, createCount: 2, needsCompletionCount: 1 }).breakdown, '2 new · 1 saved as incomplete');
  assert.equal(describeImportAction(counts).breakdown, '');
});
