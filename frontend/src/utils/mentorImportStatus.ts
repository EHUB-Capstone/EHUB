import type { MentorImportRowPreview } from '../types/mentorAdmin';

/** What the import will do with one row of the file. Every row falls in exactly one category. */
export type ImportRowCategory = 'create' | 'complete' | 'update' | 'incomplete' | 'invalid';
export type ImportStatusFilter = 'all' | ImportRowCategory;

/** The same tone is used by the summary tile, the filter chip and the status of the row, so one status always looks the same. */
export type ImportStatusTone = 'success' | 'info' | 'warning' | 'danger';

export const IMPORT_STATUS_META: Record<ImportRowCategory, { label: string; tone: ImportStatusTone }> = {
  create: { label: 'Create', tone: 'success' },
  complete: { label: 'Complete record', tone: 'success' },
  update: { label: 'Update', tone: 'info' },
  incomplete: { label: 'Incomplete', tone: 'warning' },
  invalid: { label: 'Invalid', tone: 'danger' },
};

/** Order of the filter chips, the same as the summary tiles. */
export const IMPORT_FILTER_ORDER: ImportRowCategory[] = ['create', 'complete', 'update', 'incomplete', 'invalid'];

export function importRowCategory(row: Pick<MentorImportRowPreview, 'status' | 'isValid'>): ImportRowCategory {
  if (!row.isValid) return 'invalid';
  if (row.status === 'NeedsCompletion' || row.status === 'UpdateIncomplete') return 'incomplete';
  if (row.status === 'CompleteIncomplete') return 'complete';
  if (row.status === 'Create') return 'create';
  return 'update';
}

export function countImportRows(rows: readonly Pick<MentorImportRowPreview, 'status' | 'isValid'>[]): Record<ImportStatusFilter, number> {
  const counts: Record<ImportStatusFilter, number> = { all: rows.length, create: 0, complete: 0, update: 0, incomplete: 0, invalid: 0 };
  for (const row of rows) counts[importRowCategory(row)]++;
  return counts;
}

export function filterImportRows<T extends Pick<MentorImportRowPreview, 'status' | 'isValid'>>(rows: readonly T[], filter: ImportStatusFilter): T[] {
  return filter === 'all' ? [...rows] : rows.filter(row => importRowCategory(row) === filter);
}
