import type { MentorImportCommitResult, MentorImportPreview } from '../types/mentorAdmin';
import i18n from '../i18n/index.ts';

export const MASTER_IMPORT_MAX_BYTES = 5 * 1024 * 1024;

export function validateMentorWorkbook(file: { name: string; size: number }): string | null {
  if (!file.name.toLowerCase().endsWith('.xlsx')) return i18n.t('utils:mentorMasterImport.onlyXlsxExcelFilesAreAccepted');
  if (file.size === 0) return i18n.t('utils:mentorMasterImport.theSelectedFileIsEmpty');
  if (file.size > MASTER_IMPORT_MAX_BYTES) return i18n.t('utils:mentorMasterImport.theFileMayNotExceed5');
  return null;
}

type ImportCounts = Pick<MentorImportPreview, 'createCount' | 'updateCount' | 'needsCompletionCount'> & Partial<Pick<MentorImportPreview, 'completeDraftCount'>>;

/**
 * Rows that change something when the master import is confirmed: new accounts, updated mentors, mentors who now get their
 * account because the email arrived ("Complete record"), and incomplete mentors saved for later.
 */
export function countMasterImportChanges(preview: ImportCounts): number {
  return preview.createCount + preview.updateCount + (preview.completeDraftCount ?? 0) + preview.needsCompletionCount;
}

function plural(count: number, singular: string, pluralForm = `${singular}s`): string {
  return `${count} ${count === 1 ? singular : pluralForm}`;
}

/** The label of the confirm button and a one-line breakdown, so the button says what the import will really do. */
export function describeImportAction(preview: ImportCounts): { label: string; breakdown: string } {
  const created = preview.createCount;
  const completed = preview.completeDraftCount ?? 0;
  const updated = preview.updateCount;
  const incomplete = preview.needsCompletionCount;
  const total = countMasterImportChanges(preview);
  const parts = [
    created > 0 ? `${created} new` : '',
    completed > 0 ? `${completed} completed with an email` : '',
    updated > 0 ? `${updated} updated` : '',
    incomplete > 0 ? `${incomplete} saved as incomplete` : '',
  ].filter(Boolean);
  const breakdown = parts.join(' · ');

  if (total === 0) return { label: i18n.t('utils:mentorMasterImport.nothingToImport'), breakdown };
  if (created + completed === 0 && incomplete === 0) return { label: i18n.t('utils:mentorMasterImport.update', { v1: plural(total, 'mentor') }), breakdown };
  if (updated === 0 && incomplete === 0) return { label: i18n.t('utils:mentorMasterImport.create', { v1: plural(total, 'account') }), breakdown };
  if (created + completed + updated === 0) return { label: i18n.t('utils:mentorMasterImport.saveAsIncomplete', { v1: plural(total, 'mentor') }), breakdown };
  return { label: i18n.t('utils:mentorMasterImport.apply', { v1: plural(total, 'change') }), breakdown };
}

export function describeMasterImportResult(result: MentorImportCommitResult): string {
  const parts = [plural(result.createdCount, 'new account'), `${result.updatedCount} updated`];
  if (result.draftSavedCount > 0) parts.push(`${result.draftSavedCount} saved as incomplete`);
  if (result.draftCompletedCount > 0) parts.push(`${result.draftCompletedCount} completed`);
  const converted = result.temporaryAssignmentsConverted ?? 0;
  if (converted > 0) parts.push(`${plural(converted, 'team')} switched from a temporary mentor to the new account`);
  const semesters = result.temporarySemestersConverted ?? 0;
  if (semesters > 0) parts.push(`${plural(semesters, 'semester entry', 'semester entries')} moved onto the account, no need to add the mentor again`);
  const hint = result.createdCount > 0 ? ' New accounts can use Forgot Password to set their first password.' : '';
  return i18n.t('utils:mentorMasterImport.mentorImportCompleted', { v1: parts.join(', '), hint });
}

/** What to show in the Email column of an import row: a neutral hint instead of an error when the file has no email. */
export function emailCellText(email: string | null | undefined): { text: string; provided: boolean } {
  const value = email?.trim();
  return value ? { text: value, provided: true } : { text: i18n.t('utils:mentorMasterImport.noEmailYet'), provided: false };
}

/** Short summary of the missing columns for a table cell: the first few names and how many are hidden. */
export function summarizeMissingFields(fields: readonly string[], previewSize = 3): { count: number; preview: string[]; hiddenCount: number } {
  return { count: fields.length, preview: fields.slice(0, previewSize), hiddenCount: Math.max(0, fields.length - previewSize) };
}
