import assert from 'node:assert/strict';
import test from 'node:test';
import { parseCsv, validateStudentRows } from '../src/utils/studentImport.ts';

const headers = ['StudentCode', 'FullName', 'Email', 'Major'];

test('accepts valid student records', () => {
  const result = validateStudentRows([
    headers,
    ['SE170001', 'Nguyen Van An', 'an.nguyen@fpt.edu.vn', 'SE'],
    ['SE170002', 'Tran Thi B', 'b.tran@fpt.edu.vn', 'AI'],
  ]);

  assert.equal(result.rows.length, 2);
  assert.equal(result.validRows.length, 2);
  assert.equal(result.invalidRows.length, 0);
  assert.deepEqual(result.fileErrors, []);
});

test('reports missing required values and invalid formats by row', () => {
  const result = validateStudentRows([
    headers,
    ['', '', 'not-an-email', 'SE'],
  ]);

  assert.equal(result.validRows.length, 0);
  assert.equal(result.invalidRows.length, 1);
  assert.deepEqual(result.invalidRows[0].errors, [
    'Student code is required.',
    'Full name is required.',
    'Email format is invalid.',
  ]);
});

test('prevents duplicated student codes and emails inside the file', () => {
  const result = validateStudentRows([
    headers,
    ['SE170001', 'Nguyen Van An', 'shared@fpt.edu.vn', 'SE'],
    ['se170001', 'Tran Thi B', 'SHARED@fpt.edu.vn', 'AI'],
  ]);

  assert.equal(result.validRows.length, 1);
  assert.equal(result.invalidRows.length, 1);
  assert.deepEqual(result.invalidRows[0].errors, [
    'Student code duplicates row 2.',
    'Email duplicates row 2.',
  ]);
});

test('prevents records that duplicate students already in the class', () => {
  const result = validateStudentRows(
    [
      headers,
      ['SE169999', 'Existing Student', 'existing@fpt.edu.vn', 'SE'],
    ],
    [{ rollNumber: 'se169999', email: 'EXISTING@fpt.edu.vn' }],
  );

  assert.equal(result.validRows.length, 0);
  assert.deepEqual(result.invalidRows[0].errors, [
    'Student code already exists in this class.',
    'Email already exists in this class.',
  ]);
});

test('reports missing required template columns', () => {
  const result = validateStudentRows([
    ['StudentCode', 'FullName'],
    ['SE170001', 'Nguyen Van An'],
  ]);

  assert.equal(result.rows.length, 0);
  assert.deepEqual(result.fileErrors, ['Missing required column: Email.']);
});

test('parses quoted CSV values without shifting columns', () => {
  const rows = parseCsv(
    'StudentCode,FullName,Email,Major\r\nSE170001,"Nguyen, Van An",an@fpt.edu.vn,SE',
  );

  assert.deepEqual(rows[1], ['SE170001', 'Nguyen, Van An', 'an@fpt.edu.vn', 'SE']);
});
