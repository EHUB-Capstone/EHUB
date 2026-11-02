import type { CheckpointSummary, FileSummary, FileSummaryStatus, RequirementFinding } from '../types/checkpointSummary';

type Tone = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'brand';

/** How often the card asks again while a file is queued or being summarised. */
export const CHECKPOINT_SUMMARY_POLL_MS = 5_000;

const REVIEWER_ROLES = new Set(['ADMIN', 'LECTURER', 'MENTOR']);

/** Students never see AI summaries; the backend refuses them too. */
export const canViewCheckpointSummary = (role: string | null | undefined) =>
  REVIEWER_ROLES.has((role ?? '').toUpperCase());

export const isFileInProgress = (status: FileSummaryStatus) => status === 'Pending' || status === 'Processing';

export const hasWorkInProgress = (summary: CheckpointSummary | null) =>
  Boolean(summary?.files.some((file) => isFileInProgress(file.status)));

/** A file can be chosen when it has no current summary and is not already queued. */
export const isFileSelectable = (file: FileSummary) =>
  file.status === 'None' || file.status === 'Failed' || file.status === 'Cancelled';

/** By default every file that still needs a summary is chosen, newest version first as listed. */
export const defaultFileSelection = (summary: CheckpointSummary | null) =>
  (summary?.files ?? []).filter(isFileSelectable).map((file) => file.fileId);

/** Keeps only choices that are still possible after the list changed (a file was summarised, queued or deleted). */
export const keepSelectable = (selected: string[], summary: CheckpointSummary | null) => {
  const selectable = new Set((summary?.files ?? []).filter(isFileSelectable).map((file) => file.fileId));
  return selected.filter((id) => selectable.has(id));
};

export function nextSummaryPollDelay(summary: CheckpointSummary | null): number | null {
  return hasWorkInProgress(summary) ? CHECKPOINT_SUMMARY_POLL_MS : null;
}

export function fileStatusView(file: FileSummary): { label: string; tone: Tone } {
  switch (file.status) {
    case 'Pending':
      return { label: file.queueAhead ? `Queued · ${file.queueAhead} ahead` : 'Queued', tone: 'warning' };
    case 'Processing':
      return { label: 'Summarising', tone: 'info' };
    case 'Ready':
      return { label: 'Summarised', tone: 'success' };
    case 'Failed':
      return { label: 'Failed', tone: 'danger' };
    case 'Cancelled':
      return { label: 'Cancelled', tone: 'neutral' };
    default:
      return { label: 'Not summarised', tone: 'neutral' };
  }
}

// Never a pass/fail judgement: the wording says what the AI found, not whether the requirement is met.
export const summaryFindingView: Record<RequirementFinding, { label: string; tone: Tone; hint: string }> = {
  Related: { label: 'Related content found', tone: 'success', hint: 'The quote comes from the submitted file.' },
  Unclear: { label: 'AI not sure', tone: 'warning', hint: 'Check the files yourself.' },
  NotFound: { label: 'Not found in files', tone: 'neutral', hint: 'The AI did not find this in the summarised files.' },
  NotChecked: { label: 'Not checked yet', tone: 'neutral', hint: 'Summarise a file to check this requirement.' },
};

const ERROR_MESSAGES: Record<string, string> = {
  WORKSPACE_SUMMARY_DISABLED: 'AI summaries are not enabled on this server.',
  WORKSPACE_SUMMARY_QUEUE_FULL: 'Many summaries are waiting. Please try again in a few minutes.',
  WORKSPACE_SUMMARY_UNREADABLE: 'No text could be read from this file. Scanned PDFs, images and protected files are not supported.',
  WORKSPACE_SUMMARY_UNAVAILABLE: 'The AI service could not finish this summary. Please try again later.',
  WORKSPACE_SUMMARY_NO_CONTENT: 'This file is no longer part of the checkpoint.',
  WORKSPACE_NOT_FOUND: 'Some files changed while you were choosing. The list has been refreshed; please choose again.',
  WORKSPACE_VALIDATION_ERROR: 'Choose between one and five files to summarise.',
  AUTH_RATE_LIMITED: 'You have requested many summaries in the last hour. Please wait before trying again.',
  WORKSPACE_ACCESS_DENIED: 'You do not have access to AI summaries for this team.',
};

/** Friendly text for an error code from the API or from a failed file. Never shows raw server errors. */
export function summaryErrorMessage(code: string | null | undefined): string {
  return (code && ERROR_MESSAGES[code]) || 'The AI summary could not be loaded. Please try again.';
}

export function formatSummaryTime(value: string | null): string {
  if (!value) return '';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString();
}
