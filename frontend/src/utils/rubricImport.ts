import * as XLSX from 'xlsx';
import type {
  SubjectCheckpointDraft,
  SubjectRubricCriterion,
  SubjectRubricLevel,
} from '../types/subjects.ts';
import { generateCriterionKey } from './rubricKey.ts';

export const RUBRIC_IMPORT_ACCEPT = '.xlsx,.xls';
export const RUBRIC_IMPORT_MAX_SIZE = 5 * 1024 * 1024;

const MAX_CHECKPOINTS = 10;
const MAX_CRITERIA = 100;
const MAX_ROWS = 500;

const HEADER_ALIASES = {
  checkpoint: new Set(['checkpoint', 'checkpointno', 'checkpointnumber', 'cp']),
  criterion: new Set(['standard', 'standards', 'stardard', 'stardards', 'criterion', 'criteria', 'rubriccriterion']),
  weight: new Set(['weight', 'weightpercent', 'percentage', 'percent']),
};

export interface RubricImportCheckpoint {
  number: number;
  criteria: SubjectRubricCriterion[];
}

export interface RubricImportPreview {
  fileName: string;
  sheetName: string;
  checkpoints: RubricImportCheckpoint[];
  warnings: string[];
}

export interface ParsedRubricRows {
  checkpoints: RubricImportCheckpoint[];
  errors: string[];
}

export interface RubricImportMergeResult {
  checkpoints: SubjectCheckpointDraft[];
  createdCheckpointNumbers: number[];
}

export class RubricImportValidationError extends Error {
  readonly issues: string[];

  constructor(issues: string[]) {
    super(issues[0] ?? 'The rubric workbook is invalid.');
    this.name = 'RubricImportValidationError';
    this.issues = issues;
  }
}

const cellText = (value: unknown): string => String(value ?? '').trim();

const normalizeHeader = (value: unknown): string => cellText(value)
  .normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '')
  .replace(/đ/gi, 'd')
  .replace(/[^a-z0-9]/gi, '')
  .toLowerCase();

const normalizeSubjectCode = (value: string): string => value.replace(/[^a-z0-9]/gi, '').toUpperCase();
const normalizeLabel = (value: string): string => value.trim().replace(/\s+/g, ' ').toLocaleLowerCase();

const parseCheckpointNumber = (value: unknown): number | null => {
  if (typeof value === 'number' && Number.isInteger(value)) return value;
  const match = cellText(value).match(/(?:checkpoint\s*)?(\d+)/i);
  return match ? Number.parseInt(match[1], 10) : null;
};

const parseWeight = (value: unknown): number | null => {
  if (typeof value === 'number' && Number.isFinite(value)) {
    const percentage = value > 0 && value <= 1 ? value * 100 : value;
    return Math.round(percentage * 1000) / 1000;
  }

  const text = cellText(value).replace(/\s/g, '');
  if (!text) return null;
  const percentage = text.endsWith('%');
  const parsed = Number.parseFloat(text.replace('%', '').replace(',', '.'));
  if (!Number.isFinite(parsed)) return null;
  const valueAsPercent = percentage ? parsed : parsed > 0 && parsed <= 1 ? parsed * 100 : parsed;
  return Math.round(valueAsPercent * 1000) / 1000;
};

const parseLevelHeader = (value: unknown, index: number): Omit<SubjectRubricLevel, 'description'> => {
  const text = cellText(value);
  const match = text.match(/^(.+?)\s*\((.+)\)\s*$/);
  const label = match?.[1]?.trim() || text || `Level ${index + 1}`;
  const range = match?.[2]?.trim() || '';
  return {
    key: generateCriterionKey(label) || `level${index + 1}`,
    label,
    range,
  };
};

const uniqueKey = (candidate: string, usedKeys: Set<string>): string => {
  const base = candidate || 'criterion';
  let key = base;
  let suffix = 2;
  while (usedKeys.has(key.toLocaleLowerCase())) {
    key = `${base}${suffix}`;
    suffix += 1;
  }
  usedKeys.add(key.toLocaleLowerCase());
  return key;
};

export function hasSupportedRubricWorkbookSignature(bytes: Uint8Array, extension: string): boolean {
  if (extension === '.xlsx') {
    return bytes.length >= 4 && bytes[0] === 0x50 && bytes[1] === 0x4B;
  }
  if (extension === '.xls') {
    const oleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    return bytes.length >= oleSignature.length && oleSignature.every((value, index) => bytes[index] === value);
  }
  return false;
}

export function parseRubricSheetRows(rows: unknown[][]): ParsedRubricRows {
  const errors: string[] = [];
  if (rows.length === 0) return { checkpoints: [], errors: ['The selected worksheet is empty.'] };
  if (rows.length > MAX_ROWS) return { checkpoints: [], errors: [`The worksheet has more than ${MAX_ROWS} rows.`] };

  let headerRowIndex = -1;
  let checkpointColumn = -1;
  let criterionColumn = -1;
  let weightColumn = -1;

  for (let rowIndex = 0; rowIndex < Math.min(rows.length, 20); rowIndex += 1) {
    const row = rows[rowIndex] ?? [];
    const normalized = row.map(normalizeHeader);
    const checkpoint = normalized.findIndex(value => HEADER_ALIASES.checkpoint.has(value));
    const criterion = normalized.findIndex(value => HEADER_ALIASES.criterion.has(value));
    const weight = normalized.findIndex(value => HEADER_ALIASES.weight.has(value));
    if (checkpoint >= 0 && criterion >= 0 && weight > criterion) {
      headerRowIndex = rowIndex;
      checkpointColumn = checkpoint;
      criterionColumn = criterion;
      weightColumn = weight;
      break;
    }
  }

  if (headerRowIndex < 0) {
    return {
      checkpoints: [],
      errors: ['Could not find the Checkpoint, Standards/Criteria, and Weight columns.'],
    };
  }

  const levelHeaderRow = rows[headerRowIndex + 1] ?? [];
  const levelHeaders = Array.from(
    { length: Math.max(0, weightColumn - criterionColumn - 1) },
    (_, index) => parseLevelHeader(levelHeaderRow[criterionColumn + 1 + index], index),
  );
  if (levelHeaders.length === 0) errors.push('At least one performance level column is required.');

  const checkpointMap = new Map<number, SubjectRubricCriterion[]>();
  const usedKeysByCheckpoint = new Map<number, Set<string>>();
  let currentCheckpoint: number | null = null;
  let criterionCount = 0;

  for (let rowIndex = headerRowIndex + 2; rowIndex < rows.length; rowIndex += 1) {
    const row = rows[rowIndex] ?? [];
    const checkpointCell = cellText(row[checkpointColumn]);
    const criterionLabel = cellText(row[criterionColumn]);

    if (checkpointCell) {
      const parsedCheckpoint = parseCheckpointNumber(row[checkpointColumn]);
      if (parsedCheckpoint === null || parsedCheckpoint < 1 || parsedCheckpoint > MAX_CHECKPOINTS) {
        errors.push(`Row ${rowIndex + 1}: checkpoint must be a number from 1 to ${MAX_CHECKPOINTS}.`);
        currentCheckpoint = null;
      } else {
        currentCheckpoint = parsedCheckpoint;
      }
    }

    if (!criterionLabel || normalizeHeader(criterionLabel) === 'total') continue;
    if (currentCheckpoint === null) {
      errors.push(`Row ${rowIndex + 1}: criterion "${criterionLabel}" does not have a valid checkpoint.`);
      continue;
    }

    criterionCount += 1;
    if (criterionCount > MAX_CRITERIA) {
      errors.push(`The worksheet contains more than ${MAX_CRITERIA} criteria.`);
      break;
    }
    if (criterionLabel.length > 200) {
      errors.push(`Row ${rowIndex + 1}: criterion name must be 200 characters or fewer.`);
      continue;
    }

    const weight = parseWeight(row[weightColumn]);
    if (weight === null || weight <= 0 || weight > 100) {
      errors.push(`Row ${rowIndex + 1}: criterion weight must be greater than 0 and no more than 100%.`);
      continue;
    }

    const criteria = checkpointMap.get(currentCheckpoint) ?? [];
    if (criteria.some(item => normalizeLabel(item.label) === normalizeLabel(criterionLabel))) {
      errors.push(`Row ${rowIndex + 1}: criterion "${criterionLabel}" is duplicated in checkpoint ${currentCheckpoint}.`);
      continue;
    }

    const usedKeys = usedKeysByCheckpoint.get(currentCheckpoint) ?? new Set<string>();
    usedKeysByCheckpoint.set(currentCheckpoint, usedKeys);
    const levels = levelHeaders.map((header, levelIndex) => ({
      ...header,
      description: cellText(row[criterionColumn + 1 + levelIndex]),
    }));
    criteria.push({
      key: uniqueKey(generateCriterionKey(criterionLabel), usedKeys),
      label: criterionLabel,
      description: '',
      weight,
      levels,
    });
    checkpointMap.set(currentCheckpoint, criteria);
  }

  const checkpoints = [...checkpointMap.entries()]
    .sort(([left], [right]) => left - right)
    .map(([number, criteria]) => ({ number, criteria }));

  if (checkpoints.length === 0 && errors.length === 0) errors.push('The worksheet does not contain any rubric criteria.');
  if (checkpoints.length > MAX_CHECKPOINTS) errors.push(`A rubric can contain at most ${MAX_CHECKPOINTS} checkpoints.`);

  for (const checkpoint of checkpoints) {
    const total = checkpoint.criteria.reduce((sum, criterion) => sum + Number(criterion.weight), 0);
    if (Math.abs(total - 100) > 0.001) {
      errors.push(`Checkpoint ${checkpoint.number} criterion weights total ${total.toFixed(1)}%, not 100.0%.`);
    }
  }

  return { checkpoints, errors };
}

export async function readRubricImportFile(file: File, subjectCode: string): Promise<RubricImportPreview> {
  const extension = file.name.slice(file.name.lastIndexOf('.')).toLocaleLowerCase();
  if (!['.xlsx', '.xls'].includes(extension)) {
    throw new RubricImportValidationError(['Only .xlsx and .xls rubric files are supported.']);
  }
  if (file.size === 0) throw new RubricImportValidationError(['The selected file is empty.']);
  if (file.size > RUBRIC_IMPORT_MAX_SIZE) {
    throw new RubricImportValidationError(['The rubric file must be 5 MB or smaller.']);
  }

  const bytes = new Uint8Array(await file.arrayBuffer());
  if (!hasSupportedRubricWorkbookSignature(bytes, extension)) {
    throw new RubricImportValidationError(['The file content does not match its Excel extension.']);
  }

  let workbook: XLSX.WorkBook;
  try {
    workbook = XLSX.read(bytes, { type: 'array', cellDates: false });
  } catch {
    throw new RubricImportValidationError(['The Excel workbook could not be read.']);
  }

  const subjectKey = normalizeSubjectCode(subjectCode);
  const matchedSheet = workbook.SheetNames.find(name => normalizeSubjectCode(name) === subjectKey);
  const warnings: string[] = [];
  const sheetName = matchedSheet ?? (workbook.SheetNames.length === 1 ? workbook.SheetNames[0] : undefined);
  if (!sheetName) {
    throw new RubricImportValidationError([
      `The workbook does not contain a worksheet for ${subjectCode}. Available worksheets: ${workbook.SheetNames.join(', ')}.`,
    ]);
  }
  if (!matchedSheet) warnings.push(`Worksheet "${sheetName}" was used because the workbook has only one worksheet.`);

  const worksheet = workbook.Sheets[sheetName];
  if (!worksheet) throw new RubricImportValidationError([`Worksheet "${sheetName}" could not be read.`]);
  // Some workbooks format entire columns, which expands !ref to Excel's last row.
  // Bound the read to the supported import area so styling-only rows do not exhaust memory.
  const rows = XLSX.utils.sheet_to_json<unknown[]>(worksheet, {
    header: 1,
    defval: '',
    raw: true,
    range: { s: { r: 0, c: 0 }, e: { r: MAX_ROWS - 1, c: 49 } },
  });
  const parsed = parseRubricSheetRows(rows);
  if (parsed.errors.length > 0) throw new RubricImportValidationError(parsed.errors);

  return {
    fileName: file.name,
    sheetName,
    checkpoints: parsed.checkpoints,
    warnings,
  };
}

export function mergeRubricImport(
  existingCheckpoints: readonly SubjectCheckpointDraft[],
  importedCheckpoints: readonly RubricImportCheckpoint[],
): RubricImportMergeResult {
  const importedByNumber = new Map(importedCheckpoints.map(item => [item.number, item]));
  const createdCheckpointNumbers: number[] = [];

  const checkpoints = existingCheckpoints.map(checkpoint => {
    const imported = importedByNumber.get(checkpoint.number);
    if (!imported) return checkpoint;
    importedByNumber.delete(checkpoint.number);

    const existingByLabel = new Map(checkpoint.rubrics.map(criterion => [normalizeLabel(criterion.label), criterion]));
    const usedKeys = new Set<string>();
    const rubrics = imported.criteria.map(criterion => {
      const existing = existingByLabel.get(normalizeLabel(criterion.label));
      return {
        ...criterion,
        key: uniqueKey(existing?.key || criterion.key, usedKeys),
        description: existing?.description || criterion.description,
      };
    });
    return { ...checkpoint, rubrics };
  });

  for (const imported of importedByNumber.values()) {
    createdCheckpointNumbers.push(imported.number);
    checkpoints.push({
      number: imported.number,
      title: `Checkpoint ${imported.number}`,
      shortDescription: '',
      courseWeight: 0,
      requirements: [],
      rubrics: imported.criteria.map(criterion => ({ ...criterion })),
    });
  }

  return {
    checkpoints: checkpoints.sort((left, right) => left.number - right.number),
    createdCheckpointNumbers: createdCheckpointNumbers.sort((left, right) => left - right),
  };
}
