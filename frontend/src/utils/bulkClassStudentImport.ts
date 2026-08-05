import type { ParsedApiError } from './apiError.ts';
import { parseApiError } from './apiError.ts';
import { buildStudentImportWorkbookBlob } from './studentImport.ts';

export interface ParsedBulkClassStudentRow {
  sourceRowNumber: number;
  classVal: string;
  studentCode: string;
  fullName: string;
  email: string;
  majorCode: string;
}

export interface CreatedBulkClass {
  id?: string;
  _id?: string;
  classCode: string;
  classIndex: number;
}

export interface BulkStudentImportIssue {
  classCode: string;
  rowNumber: number;
  studentCode: string;
  errorCode: string;
  errorMessage: string;
}

export interface BulkStudentImportSummary {
  requestedCount: number;
  insertedCount: number;
  updatedCount: number;
  errorCount: number;
  issues: BulkStudentImportIssue[];
}

interface ImportPreviewRow {
  rowNumber: number;
  studentCode: string;
  isValid: boolean;
  errorMessage?: string | null;
}

interface ImportPreviewResponse {
  sessionId?: string;
  validRowsCount: number;
  rows?: ImportPreviewRow[];
}

interface ImportCommitError {
  rowNumber: number;
  studentCode: string;
  errorCode: string;
  errorMessage: string;
}

interface ImportCommitResponse {
  insertedCount: number;
  updatedCount: number;
  errors?: ImportCommitError[];
}

interface BulkStudentImportApi {
  previewImportStudents: (classId: string, formData: FormData) => Promise<unknown>;
  commitImportStudents: (classId: string, payload: { sessionId: string }) => Promise<unknown>;
}

const unwrapData = <T>(response: unknown): T => {
  const wrapped = response as { data?: T } | null | undefined;
  return (wrapped?.data ?? response) as T;
};

export function parseClassIndex(value: unknown): number | null {
  const text = String(value ?? '').trim();
  if (!text) return null;

  const separatedParts = text.split(/[-_]/).filter(Boolean);
  const separatedIndex = Number.parseInt(separatedParts.at(-1) ?? '', 10);
  if (Number.isInteger(separatedIndex) && separatedIndex > 0) return separatedIndex;

  if (/^\d+$/.test(text)) {
    const directIndex = Number.parseInt(text, 10);
    return directIndex > 0 ? directIndex : null;
  }

  return null;
}

function sourceRowNumber(previewRowNumber: number, rows: ParsedBulkClassStudentRow[]): number {
  // The generated workbook always has one header row, so Excel row 2 maps to index 0.
  return rows[previewRowNumber - 2]?.sourceRowNumber ?? previewRowNumber;
}

function rowIssues(
  targetClass: CreatedBulkClass,
  rows: ParsedBulkClassStudentRow[],
  errorCode: string,
  errorMessage: string,
): BulkStudentImportIssue[] {
  return rows.map(row => ({
    classCode: targetClass.classCode,
    rowNumber: row.sourceRowNumber,
    studentCode: row.studentCode,
    errorCode,
    errorMessage,
  }));
}

export async function importStudentsIntoCreatedClasses(
  createdClasses: CreatedBulkClass[],
  rows: ParsedBulkClassStudentRow[],
  api: BulkStudentImportApi,
): Promise<BulkStudentImportSummary> {
  const summary: BulkStudentImportSummary = {
    requestedCount: rows.length,
    insertedCount: 0,
    updatedCount: 0,
    errorCount: 0,
    issues: [],
  };
  const returnedClassIndices = new Set(createdClasses.map(targetClass => targetClass.classIndex));

  for (const row of rows) {
    const classIndex = parseClassIndex(row.classVal);
    if (classIndex !== null && returnedClassIndices.has(classIndex)) continue;
    summary.issues.push({
      classCode: row.classVal,
      rowNumber: row.sourceRowNumber,
      studentCode: row.studentCode,
      errorCode: 'CREATED_CLASS_NOT_RETURNED',
      errorMessage: `No created class was returned for '${row.classVal}'.`,
    });
  }

  for (const targetClass of createdClasses) {
    const classId = targetClass.id ?? targetClass._id;
    const matchingRows = rows.filter(row => parseClassIndex(row.classVal) === targetClass.classIndex);
    if (matchingRows.length === 0) continue;

    if (!classId) {
      summary.issues.push(...rowIssues(
        targetClass,
        matchingRows,
        'CLASS_ID_MISSING',
        `The server did not return an id for ${targetClass.classCode}.`,
      ));
      continue;
    }

    try {
      const workbook = buildStudentImportWorkbookBlob(matchingRows);
      const formData = new FormData();
      formData.append('file', workbook, `${targetClass.classCode}-students.xlsx`);

      const preview = unwrapData<ImportPreviewResponse>(
        await api.previewImportStudents(classId, formData),
      );

      const rejectedPreviewRows = new Set<number>();
      for (const row of preview.rows ?? []) {
        if (row.isValid) continue;
        rejectedPreviewRows.add(row.rowNumber);
        summary.issues.push({
          classCode: targetClass.classCode,
          rowNumber: sourceRowNumber(row.rowNumber, matchingRows),
          studentCode: row.studentCode,
          errorCode: 'IMPORT_PREVIEW_VALIDATION',
          errorMessage: row.errorMessage || 'This row failed import validation.',
        });
      }

      if (preview.validRowsCount <= 0) {
        if (rejectedPreviewRows.size === 0) {
          summary.issues.push(...rowIssues(
            targetClass,
            matchingRows,
            'IMPORT_PREVIEW_EMPTY',
            'The server did not accept any rows and returned no row-level reason.',
          ));
        }
        continue;
      }
      if (!preview.sessionId) {
        const validRows = matchingRows.filter((_, index) => !rejectedPreviewRows.has(index + 2));
        summary.issues.push(...rowIssues(
          targetClass,
          validRows,
          'IMPORT_SESSION_MISSING',
          'The server validated rows but did not create an import session.',
        ));
        continue;
      }

      const committed = unwrapData<ImportCommitResponse>(
        await api.commitImportStudents(classId, { sessionId: preview.sessionId }),
      );
      summary.insertedCount += committed.insertedCount ?? 0;
      summary.updatedCount += committed.updatedCount ?? 0;

      for (const row of committed.errors ?? []) {
        summary.issues.push({
          classCode: targetClass.classCode,
          rowNumber: sourceRowNumber(row.rowNumber, matchingRows),
          studentCode: row.studentCode,
          errorCode: row.errorCode,
          errorMessage: row.errorMessage,
        });
      }
    } catch (error) {
      const parsed: ParsedApiError = parseApiError(error, `Unable to import students into ${targetClass.classCode}.`);
      summary.issues.push(...rowIssues(
        targetClass,
        matchingRows,
        parsed.code || 'IMPORT_REQUEST_FAILED',
        parsed.message,
      ));
    }
  }

  summary.errorCount = summary.issues.length;
  return summary;
}
