import type { MentorImportCommitResult, MentorImportPreview } from '../types/mentorAdmin';

export const MASTER_IMPORT_MAX_BYTES = 5 * 1024 * 1024;

export function validateMentorWorkbook(file: { name: string; size: number }): string | null {
  if (!file.name.toLowerCase().endsWith('.xlsx')) return 'Only .xlsx Excel files are accepted.';
  if (file.size === 0) return 'The selected file is empty.';
  if (file.size > MASTER_IMPORT_MAX_BYTES) return 'The file may not exceed 5 MB.';
  return null;
}

/** Rows that change something when the master import is confirmed: accounts, and incomplete mentors saved for later. */
export function countMasterImportChanges(
  preview: Pick<MentorImportPreview, 'createCount' | 'updateCount' | 'needsCompletionCount'>,
): number {
  return preview.createCount + preview.updateCount + preview.needsCompletionCount;
}

function plural(count: number, singular: string, pluralForm = `${singular}s`): string {
  return `${count} ${count === 1 ? singular : pluralForm}`;
}

export function describeMasterImportResult(result: MentorImportCommitResult): string {
  const parts = [plural(result.createdCount, 'new account'), `${result.updatedCount} updated`];
  if (result.draftSavedCount > 0) parts.push(`${result.draftSavedCount} saved as incomplete`);
  if (result.draftCompletedCount > 0) parts.push(`${result.draftCompletedCount} completed`);
  const hint = result.createdCount > 0 ? ' New accounts can use Forgot Password to set their first password.' : '';
  return `Mentor import completed: ${parts.join(', ')}.${hint}`;
}

/** What to show in the Email column of an import row: a neutral hint instead of an error when the file has no email. */
export function emailCellText(email: string | null | undefined): { text: string; provided: boolean } {
  const value = email?.trim();
  return value ? { text: value, provided: true } : { text: 'No email yet', provided: false };
}

/** Short summary of the missing columns for a table cell: the first few names and how many are hidden. */
export function summarizeMissingFields(fields: readonly string[], previewSize = 3): { count: number; preview: string[]; hiddenCount: number } {
  return { count: fields.length, preview: fields.slice(0, previewSize), hiddenCount: Math.max(0, fields.length - previewSize) };
}
