import assert from 'node:assert/strict';
import test from 'node:test';
import { parseCsv, validateStudentRows } from '../src/utils/studentImport.ts';
import {
  importStudentsIntoCreatedClasses,
  parseClassIndex,
} from '../src/utils/bulkClassStudentImport.ts';

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

test('parses the trailing class index from imported class codes', () => {
  assert.equal(parseClassIndex('EXE101_8'), 8);
  assert.equal(parseClassIndex('EXE101-12'), 12);
  assert.equal(parseClassIndex('7'), 7);
  assert.equal(parseClassIndex('EXE101'), null);
  assert.equal(parseClassIndex(''), null);
});

test('reports every rejected bulk roster row instead of treating it as a successful import', async () => {
  let commitCalled = false;
  const summary = await importStudentsIntoCreatedClasses(
    [{ id: 'class-8', classCode: 'EXE101_8', classIndex: 8 }],
    [{
      sourceRowNumber: 11,
      classVal: 'EXE101_8',
      studentCode: 'DE180225',
      fullName: 'Thai Ngoc Linh',
      email: 'linhtnde180225@fpt.edu.vn',
      majorCode: '',
    }],
    {
      previewImportStudents: async (_classId, formData) => {
        assert.ok((formData.get('file') as Blob).size > 0);
        return {
          data: {
            sessionId: '',
            validRowsCount: 0,
            rows: [{
              rowNumber: 2,
              studentCode: 'DE180225',
              isValid: false,
              errorMessage: "Student 'DE180225' is already enrolled in class 'EXE101_101' for the same course and semester.",
            }],
          },
        };
      },
      commitImportStudents: async () => {
        commitCalled = true;
        return {};
      },
    },
  );

  assert.equal(commitCalled, false);
  assert.equal(summary.insertedCount, 0);
  assert.equal(summary.errorCount, 1);
  assert.equal(summary.issues[0].rowNumber, 11);
  assert.match(summary.issues[0].errorMessage, /EXE101_101/);
});

test('commits valid bulk roster rows through the preview session contract', async () => {
  const summary = await importStudentsIntoCreatedClasses(
    [{ id: 'class-8', classCode: 'EXE101_8', classIndex: 8 }],
    [{
      sourceRowNumber: 2,
      classVal: 'EXE101_8',
      studentCode: 'SE199999',
      fullName: 'New Student',
      email: 'new.student@fpt.edu.vn',
      majorCode: '',
    }],
    {
      previewImportStudents: async () => ({
        data: {
          sessionId: 'session-1',
          validRowsCount: 1,
          rows: [{ rowNumber: 2, studentCode: 'SE199999', isValid: true }],
        },
      }),
      commitImportStudents: async (classId, payload) => {
        assert.equal(classId, 'class-8');
        assert.deepEqual(payload, { sessionId: 'session-1' });
        return { data: { insertedCount: 1, updatedCount: 0, errors: [] } };
      },
    },
  );

  assert.equal(summary.insertedCount, 1);
  assert.equal(summary.errorCount, 0);
});
