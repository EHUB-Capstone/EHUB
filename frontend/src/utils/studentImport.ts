import * as XLSX from 'xlsx';

export const STUDENT_IMPORT_ACCEPT = '.xlsx,.xls,.csv';
export const STUDENT_IMPORT_MAX_SIZE = 10 * 1024 * 1024;

export interface ExistingStudentRecord {
  rollNumber?: string | null;
  studentCode?: string | null;
  email?: string | null;
}

export interface StudentImportRecord {
  rowNumber: number;
  studentCode: string;
  fullName: string;
  email: string;
  major: string;
}

export interface StudentImportRow extends StudentImportRecord {
  errors: string[];
  isValid: boolean;
}

export interface StudentImportValidation {
  rows: StudentImportRow[];
  validRows: StudentImportRecord[];
  invalidRows: StudentImportRow[];
  fileErrors: string[];
}

export interface StudentWorkbookRecord {
  studentCode: string;
  fullName: string;
  email: string;
  majorCode?: string | null;
}

type SupportedField = 'studentCode' | 'fullName' | 'email' | 'major';

const REQUIRED_FIELDS: SupportedField[] = ['studentCode', 'fullName', 'email'];

const FIELD_LABELS: Record<SupportedField, string> = {
  studentCode: 'StudentCode',
  fullName: 'FullName',
  email: 'Email',
  major: 'Major',
};

const HEADER_ALIASES: Record<SupportedField, string[]> = {
  studentCode: ['studentcode', 'studentid', 'rollnumber', 'rollno', 'masv', 'mssv'],
  fullName: ['fullname', 'studentname', 'name', 'hovaten', 'hoten'],
  email: ['email', 'emailaddress', 'studentemail'],
  major: ['major', 'majorcode', 'specialization', 'chuyennganh'],
};

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const STUDENT_CODE_PATTERN = /^[A-Z0-9_-]{3,20}$/;

const normalizeHeader = (value: unknown) => String(value ?? '')
  .normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '')
  .replace(/đ/gi, 'd')
  .replace(/[^a-z0-9]/gi, '')
  .toLowerCase();

const cellText = (value: unknown) => String(value ?? '').trim();
const normalizeCode = (value: unknown) => cellText(value).toUpperCase();
const normalizeEmail = (value: unknown) => cellText(value).toLowerCase();

const isBlankRow = (row: unknown[]) => row.every((cell) => cellText(cell) === '');

/** Parses CSV locally, including quoted commas, escaped quotes and CRLF rows. */
export function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let value = '';
  let quoted = false;

  for (let index = 0; index < text.length; index += 1) {
    const character = text[index];

    if (character === '"') {
      if (quoted && text[index + 1] === '"') {
        value += '"';
        index += 1;
      } else {
        quoted = !quoted;
      }
      continue;
    }

    if (character === ',' && !quoted) {
      row.push(value);
      value = '';
      continue;
    }

    if ((character === '\n' || character === '\r') && !quoted) {
      if (character === '\r' && text[index + 1] === '\n') index += 1;
      row.push(value);
      rows.push(row);
      row = [];
      value = '';
      continue;
    }

    value += character;
  }

  if (value.length > 0 || row.length > 0) {
    row.push(value);
    rows.push(row);
  }

  if (rows[0]?.[0]) rows[0][0] = rows[0][0].replace(/^\uFEFF/, '');
  return rows;
}

export async function readStudentImportFile(file: File): Promise<unknown[][]> {
  try {
    const arrayBuffer = await file.arrayBuffer();
    const workbook = XLSX.read(arrayBuffer, { type: 'array' });
    const firstSheetName = workbook.SheetNames[0];
    if (!firstSheetName) return [];

    const worksheet = workbook.Sheets[firstSheetName];
    if (!worksheet) return [];

    // Parse worksheet to 2D array
    const rawRows = XLSX.utils.sheet_to_json<unknown[]>(worksheet, { header: 1, defval: '' });
    return rawRows;
  } catch {
    // Fallback for plain CSV/Text files
    try {
      const text = await file.text();
      return parseCsv(text);
    } catch (err: any) {
      throw new Error(err?.message || 'Could not parse Excel/CSV file.');
    }
  }
}

export function buildStudentImportWorkbookBlob(rows: StudentWorkbookRecord[]): Blob {
  const worksheet = XLSX.utils.json_to_sheet(
    rows.map(row => ({
      StudentCode: row.studentCode,
      FullName: row.fullName,
      Email: row.email,
      // A blank legacy major is intentionally preserved. The backend maps it to UNDECLARED.
      MajorCode: row.majorCode || '',
    })),
    { header: ['StudentCode', 'FullName', 'Email', 'MajorCode'] },
  );
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, worksheet, 'Students');
  const bytes = XLSX.write(workbook, { bookType: 'xlsx', type: 'array' });

  return new Blob([bytes], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
}

export function validateStudentRows(
  rawRows: unknown[][],
  existingStudents: ExistingStudentRecord[] = [],
): StudentImportValidation {
  const nonEmptyRows = rawRows
    .map((cells, index) => ({ cells, originalRowNumber: index + 1 }))
    .filter(({ cells }) => !isBlankRow(cells));

  if (nonEmptyRows.length === 0) {
    return { rows: [], validRows: [], invalidRows: [], fileErrors: ['The file is empty.'] };
  }

  // Find header row dynamically
  let headerRowIndex = 0;
  for (let i = 0; i < Math.min(nonEmptyRows.length, 10); i++) {
    const row = nonEmptyRows[i];
    const columnIndexesTemp = new Map<SupportedField, number>();
    row.cells.forEach((header, colIdx) => {
      const normalized = normalizeHeader(header);
      (Object.keys(HEADER_ALIASES) as SupportedField[]).forEach((field) => {
        if (!columnIndexesTemp.has(field) && HEADER_ALIASES[field].includes(normalized)) {
          columnIndexesTemp.set(field, colIdx);
        }
      });
    });
    if (REQUIRED_FIELDS.every((field) => columnIndexesTemp.has(field))) {
      headerRowIndex = i;
      break;
    }
  }

  const headerRow = nonEmptyRows[headerRowIndex];
  const columnIndexes = new Map<SupportedField, number>();

  headerRow.cells.forEach((header, columnIndex) => {
    const normalized = normalizeHeader(header);
    (Object.keys(HEADER_ALIASES) as SupportedField[]).forEach((field) => {
      if (!columnIndexes.has(field) && HEADER_ALIASES[field].includes(normalized)) {
        columnIndexes.set(field, columnIndex);
      }
    });
  });

  const missingHeaders = REQUIRED_FIELDS.filter((field) => !columnIndexes.has(field));
  if (missingHeaders.length > 0) {
    return {
      rows: [],
      validRows: [],
      invalidRows: [],
      fileErrors: [
        `Missing required column${missingHeaders.length > 1 ? 's' : ''}: ${missingHeaders.map((field) => FIELD_LABELS[field]).join(', ')}.`,
      ],
    };
  }

  const existingCodes = new Set(
    existingStudents
      .map((student) => normalizeCode(student.rollNumber || student.studentCode))
      .filter(Boolean),
  );
  const existingEmails = new Set(
    existingStudents.map((student) => normalizeEmail(student.email)).filter(Boolean),
  );
  const firstCodeRows = new Map<string, number>();
  const firstEmailRows = new Map<string, number>();

  const dataRows = nonEmptyRows.slice(headerRowIndex + 1);

  const rows = dataRows.map(({ cells, originalRowNumber }) => {
    const readField = (field: SupportedField) => {
      const index = columnIndexes.get(field);
      return index === undefined ? '' : cellText(cells[index]);
    };

    const studentCode = normalizeCode(readField('studentCode'));
    const fullName = readField('fullName');
    const email = normalizeEmail(readField('email'));
    const major = normalizeCode(readField('major'));
    const errors: string[] = [];

    if (!studentCode) {
      errors.push('Student code is required.');
    } else if (!STUDENT_CODE_PATTERN.test(studentCode)) {
      errors.push('Student code must be 3–20 characters and contain only letters, numbers, “-” or “_”.');
    } else if (existingCodes.has(studentCode)) {
      errors.push('Student code already exists in this class.');
    } else if (firstCodeRows.has(studentCode)) {
      errors.push(`Student code duplicates row ${firstCodeRows.get(studentCode)}.`);
    }

    if (!fullName) errors.push('Full name is required.');

    if (!email) {
      errors.push('Email is required.');
    } else if (!EMAIL_PATTERN.test(email)) {
      errors.push('Email format is invalid.');
    } else if (existingEmails.has(email)) {
      errors.push('Email already exists in this class.');
    } else if (firstEmailRows.has(email)) {
      errors.push(`Email duplicates row ${firstEmailRows.get(email)}.`);
    }

    if (studentCode && !firstCodeRows.has(studentCode)) firstCodeRows.set(studentCode, originalRowNumber);
    if (email && !firstEmailRows.has(email)) firstEmailRows.set(email, originalRowNumber);

    return {
      rowNumber: originalRowNumber,
      studentCode,
      fullName,
      email,
      major,
      errors,
      isValid: errors.length === 0,
    };
  });

  if (rows.length === 0) {
    return { rows: [], validRows: [], invalidRows: [], fileErrors: ['The file has headers but no student records.'] };
  }

  const validRows = rows
    .filter((row) => row.isValid)
    .map(({ errors: _errors, isValid: _isValid, ...record }) => record);
  const invalidRows = rows.filter((row) => !row.isValid);

  return { rows, validRows, invalidRows, fileErrors: [] };
}
