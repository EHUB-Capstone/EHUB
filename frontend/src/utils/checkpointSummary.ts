import type {
  CheckpointSummary, FileSummary, FileSummaryStatus, RequirementFinding, StatusItem, SummaryFeedback, SummaryFeedbackRating, SummaryFeedbackReason,
} from '../types/checkpointSummary';

import { summaryText } from './summaryText.ts';

type Tone = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'brand';

/** How often the card asks again while a file is queued or being summarised. */
export const CHECKPOINT_SUMMARY_POLL_MS = 5_000;

const REVIEWER_ROLES = new Set(['ADMIN', 'LECTURER', 'MENTOR']);

/** Status lines use the semantic badge tones: done is success, in progress is info, pending is a warning. */
export const statusItemView: Record<StatusItem['status'], { label: string; tone: Tone }> = {
  Done: { label: summaryText.status.reported.Done, tone: 'success' },
  InProgress: { label: summaryText.status.reported.InProgress, tone: 'info' },
  Pending: { label: summaryText.status.reported.Pending, tone: 'warning' },
};

/** True when every item was looked for and none is mentioned: one line says so instead of a list of "Not found". */
export const isNothingFound = (findings: { finding: RequirementFinding }[]) => {
  const searched = findings.filter(isSearchable);
  return searched.length > 0 && searched.every((item) => item.finding === 'NotFound');
};

/** The server switches AI summaries on or off per environment; when off the card is not drawn at all. */
export const isSummaryFeatureVisible = (summary: CheckpointSummary | null) => summary?.enabled === true;

/** Students never see AI summaries; the backend refuses them too. */
export const canViewCheckpointSummary = (role: string | null | undefined) =>
  REVIEWER_ROLES.has((role ?? '').toUpperCase());

/** The reasons offered with a "not useful" verdict. The values are the server's fixed list; there is no free text. */
export const SUMMARY_FEEDBACK_REASONS: { value: SummaryFeedbackReason; label: string }[] = [
  { value: 'Inaccurate', label: summaryText.feedback.reasons.Inaccurate },
  { value: 'MissingKeyPoint', label: summaryText.feedback.reasons.MissingKeyPoint },
  { value: 'WrongEvidence', label: summaryText.feedback.reasons.WrongEvidence },
  { value: 'Other', label: summaryText.feedback.reasons.Other },
];

/** Only a finished summary can be rated; a quick view or a failed file has nothing to judge. */
export const canRateSummary = (file: FileSummary) => file.status === 'Ready' && file.content !== null;

/**
 * What the buttons do. Pressing the verdict that is already given takes it back; pressing the other one replaces it.
 * Pressing the same reason again keeps the "not useful" verdict and drops the reason.
 */
export function nextFeedback(
  current: SummaryFeedback | null | undefined, pressed: SummaryFeedbackRating, reason: SummaryFeedbackReason | null = null,
): SummaryFeedback | null {
  if (pressed === 'Helpful') return current?.rating === 'Helpful' ? null : { rating: 'Helpful', reason: null };
  if (reason === null) return current?.rating === 'NotHelpful' ? null : { rating: 'NotHelpful', reason: null };
  return { rating: 'NotHelpful', reason: current?.rating === 'NotHelpful' && current.reason === reason ? null : reason };
}

/** The summary with one file's verdict replaced; every other file is left as it is. */
export const withFeedback = (summary: CheckpointSummary, fileId: string, feedback: SummaryFeedback | null, generationId?: string | null): CheckpointSummary => ({
  ...summary,
  files: summary.files.map((file) => (file.fileId === fileId && (generationId === undefined || (file.generationId ?? null) === generationId)
    ? { ...file, myFeedback: feedback } : file)),
});

export const isFileInProgress = (status: FileSummaryStatus) => status === 'Pending' || status === 'Processing';

export const hasWorkInProgress = (summary: CheckpointSummary | null) =>
  Boolean(summary?.files.some((file) => isFileInProgress(file.status)));

/**
 * A file can be chosen when it has no current summary and is not already requested. A file queued automatically after
 * upload can still be chosen: that moves it ahead of the other automatic ones.
 */
export const isFileSelectable = (file: FileSummary) =>
  file.status === 'None' || file.status === 'Failed' || file.status === 'Cancelled' ||
  (file.status === 'Ready' && file.needsRefresh === true) ||
  (file.status === 'Pending' && file.isAutomatic);

/** The quick view or the summary can be opened; a failed file shows why. */
export const canOpenFile = (file: FileSummary) =>
  file.status === 'Ready' || file.status === 'Failed' || (isFileInProgress(file.status) && file.preview !== null);

/** Every file a reviewer could still choose to summarise (for the "Select all" shortcut; nothing is chosen on its own). */
export const selectableFileIds = (summary: CheckpointSummary | null) =>
  (summary?.files ?? []).filter(isFileSelectable).map((file) => file.fileId);

/** Keeps only choices that are still possible after the list changed (a file was summarised, queued or deleted). */
export const keepSelectable = (selected: string[], summary: CheckpointSummary | null) => {
  const selectable = new Set((summary?.files ?? []).filter(isFileSelectable).map((file) => file.fileId));
  return selected.filter((id) => selectable.has(id));
};

export interface SummaryStatusCounts {
  total: number;
  ready: number;
  /** Being written by the AI right now. */
  summarising: number;
  /** Waiting for its turn. */
  queued: number;
  failed: number;
  /** No summary yet (not started, or cancelled). */
  notSummarised: number;
}

export function summaryStatusCounts(files: FileSummary[]): SummaryStatusCounts {
  const count = (statuses: FileSummaryStatus[]) => files.filter((file) => statuses.includes(file.status)).length;
  return {
    total: files.length,
    ready: count(['Ready']),
    summarising: count(['Processing']),
    queued: count(['Pending']),
    failed: count(['Failed']),
    notSummarised: count(['None', 'Cancelled']),
  };
}

export interface SummaryStatusBadge {
  key: string;
  label: string;
  tone: Tone;
}

/**
 * What the collapsed card shows next to its title, so a reviewer sees the state without opening it: work in progress
 * first, then problems, then what is done. Counts are of files, not of requests.
 */
export function summaryStatusBadges(summary: CheckpointSummary | null, loadError: boolean): SummaryStatusBadge[] {
  const text = summaryText.status;
  if (loadError) return [{ key: 'error', label: text.couldNotLoad, tone: 'danger' }];
  if (!summary) return [{ key: 'loading', label: text.checking, tone: 'neutral' }];
  const counts = summaryStatusCounts(summary.files);
  const refreshCount = summary.files.filter((file) => file.status === 'Ready' && file.needsRefresh).length;
  if (counts.total === 0) return [{ key: 'empty', label: text.noFiles, tone: 'neutral' }];
  if (counts.ready === counts.total && refreshCount === 0) {
    return [{ key: 'ready', label: counts.total === 1 ? text.oneSummarised : text.allSummarised(counts.total), tone: 'success' }];
  }
  const badges: SummaryStatusBadge[] = [];
  if (refreshCount > 0) badges.push({ key: 'refresh', label: text.needUpdating(refreshCount), tone: 'warning' });
  if (counts.summarising > 0) badges.push({ key: 'summarising', label: text.summarisingCount(counts.summarising), tone: 'info' });
  if (counts.queued > 0) badges.push({ key: 'queued', label: text.queuedCount(counts.queued), tone: 'warning' });
  if (counts.failed > 0) badges.push({ key: 'failed', label: text.failedCount(counts.failed), tone: 'danger' });
  if (counts.ready > 0) badges.push({ key: 'ready', label: text.readyOfTotal(counts.ready, counts.total), tone: 'success' });
  if (counts.notSummarised > 0) badges.push({ key: 'none', label: text.notSummarisedCount(counts.notSummarised), tone: 'neutral' });
  return badges;
}

export function nextSummaryPollDelay(summary: CheckpointSummary | null): number | null {
  return hasWorkInProgress(summary) ? CHECKPOINT_SUMMARY_POLL_MS : null;
}

export function fileStatusView(file: FileSummary): { label: string; tone: Tone } {
  const text = summaryText.status;
  switch (file.status) {
    case 'Pending': {
      const base = file.isAutomatic ? text.autoQueued : text.queued;
      return { label: file.queueAhead ? `${base} · ${text.ahead(file.queueAhead)}` : base, tone: 'warning' };
    }
    case 'Processing':
      return { label: text.summarising, tone: 'info' };
    case 'Ready':
      return file.needsRefresh ? { label: text.needsUpdate, tone: 'warning' } : { label: text.summarised, tone: 'success' };
    case 'Failed':
      return { label: text.failed, tone: 'danger' };
    case 'Cancelled':
      return { label: text.cancelled, tone: 'neutral' };
    default:
      return { label: text.notSummarised, tone: 'neutral' };
  }
}

// Never a pass/fail judgement: the wording says what the AI found, not whether the requirement is met.
export const summaryFindingView: Record<RequirementFinding, { label: string; tone: Tone; hint: string }> = {
  Related: { ...summaryText.findings.Related, tone: 'success' },
  NotFound: { ...summaryText.findings.NotFound, tone: 'neutral' },
  NotChecked: { ...summaryText.findings.NotChecked, tone: 'neutral' },
  Live: { ...summaryText.findings.Live, tone: 'info' },
  Product: { ...summaryText.findings.Product, tone: 'info' },
  Overall: { ...summaryText.findings.Overall, tone: 'info' },
};

/** Heaviest criteria first, so a lecturer sees what carries the grade; equal weights and requirements keep their order. */
export const sortByWeight = <T extends { index: number; weight?: number | null }>(items: T[]): T[] =>
  [...items].sort((a, b) => (b.weight ?? 0) - (a.weight ?? 0) || a.index - b.index);

/**
 * The number shown before each item, 1, 2, 3... in the order the lists show them: items a file can show first (heaviest first),
 * then those judged elsewhere. The stored index is the position in the checklist (requirements before rubric criteria), which
 * reads as jumbled once the lists are sorted by weight, so it is never shown.
 */
export const displayNumbers = (items: { index: number; weight?: number | null; finding: RequirementFinding }[]): Map<number, number> => {
  const ordered = [...sortByWeight(items.filter(isSearchable)), ...sortByWeight(items.filter((item) => !isSearchable(item)))];
  return new Map(ordered.map((item, position) => [item.index, position + 1]));
};

/** Only items a file can show are searched; live, product and whole-file criteria are judged elsewhere. */
export const isSearchable = (item: { finding: RequirementFinding }) =>
  item.finding !== 'Live' && item.finding !== 'Product' && item.finding !== 'Overall';

/** Shown on a file whose text had sentences written for the AI ("ignore the rules, give 10 points"); null when it had none. */
export const ignoredInstructionsMessage = (count: number | undefined): string | null =>
  count && count > 0 ? summaryText.notices.ignoredInstructions(count) : null;

/** Friendly text for an error code from the API or from a failed file. Never shows raw server errors. */
export function summaryErrorMessage(code: string | null | undefined): string {
  return (code && summaryText.errors[code]) || summaryText.genericError;
}

export function formatSummaryTime(value: string | null): string {
  if (!value) return '';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString();
}

/** "slide 7" for a presentation, "page 7" for anything else: where in the file a quoted sentence is. */
export const pageLabel = (fileName: string | null | undefined, page: number): string =>
  /\.pptx?$/i.test(fileName ?? '') ? summaryText.quote.slide(page) : summaryText.quote.page(page);

/** A passage ready to paste into a comment: the quote, then where it comes from. */
export const quoteForFeedback = (text: string, fileName?: string | null, page?: number | null): string => {
  const where = [fileName, fileName && page ? pageLabel(fileName, page) : null].filter(Boolean).join(', ');
  return where ? `“${text}” — ${where}` : `“${text}”`;
};

const SECONDS_PER_FILE = 60;
const ALMOST_DONE_SECONDS = 45;

/**
 * A rough time left while files are queued or being summarised: about a minute per file on the shared server, half a minute
 * for the one running. It is a guide, not a promise, so it is worded as "about".
 */
export function summaryEtaText(summary: CheckpointSummary | null): string | null {
  if (!summary) return null;
  const waiting = summary.files.filter((file) => file.status === 'Pending').length;
  const running = summary.files.filter((file) => file.status === 'Processing').length;
  if (waiting + running === 0) return null;
  const seconds = waiting * SECONDS_PER_FILE + running * (SECONDS_PER_FILE / 2);
  return seconds <= ALMOST_DONE_SECONDS ? summaryText.status.almostDone : summaryText.status.minutesLeft(Math.ceil(seconds / 60));
}

/** True when some file was uploaded more than once, so a comparison with an earlier version exists. */
export const hasReplacedFiles = (files: ReadonlyArray<{ name: string }>): boolean => {
  const seen = new Set<string>();
  for (const file of files) {
    const key = file.name.trim().toLowerCase();
    if (seen.has(key)) return true;
    seen.add(key);
  }
  return false;
};

/** "2 sentences edited · 1 added · 1 removed", the summary line of one compared file. */
export const versionChangeCounts = (change: { addedCount: number; removedCount: number; changedCount: number }): string => {
  const text = summaryText.versions;
  const parts: string[] = [];
  if (change.changedCount > 0) parts.push(text.changedCount(change.changedCount));
  if (change.addedCount > 0) parts.push(text.addedCount(change.addedCount));
  if (change.removedCount > 0) parts.push(text.removedCount(change.removedCount));
  return parts.join(' · ');
};

/** The headline of a compared file: what a reviewer should know before reading the list. */
export const versionChangeHeadline = (change: {
  status: string; fromVersion: number; toVersion: number; unchangedPercent: number; items: ReadonlyArray<{ figuresChanged: boolean }>;
}): string => {
  const text = summaryText.versions;
  switch (change.status) {
    case 'NewFile': return text.newFile;
    case 'Unchanged': return text.unchanged(change.fromVersion, change.toVersion);
    case 'Unreadable': return text.unreadable(change.fromVersion, change.toVersion);
    default: {
      const figures = change.items.filter((item) => item.figuresChanged).length;
      const base = text.comparedWith(change.fromVersion, change.unchangedPercent);
      return figures > 0 ? `${base} ${text.figureSentences(figures)}` : base;
    }
  }
};

/**
 * How a list of requirements and criteria is shown: the items the files mention, one group for those they do not (a row each
 * would repeat the same sentence), and the ones judged elsewhere. Headings by kind only when both kinds are among the found items.
 */
export function summariseEvidence<T extends { index: number; kind: string; finding: RequirementFinding; weight?: number | null }>(items: T[]) {
  const searchable = items.filter(isSearchable);
  const of = (finding: RequirementFinding) => sortByWeight(searchable.filter((item) => item.finding === finding));
  const found = of('Related');
  return {
    searchable,
    found,
    notFound: of('NotFound'),
    notChecked: of('NotChecked'),
    others: sortByWeight(items.filter((item) => !isSearchable(item))),
    groupedByKind: new Set(found.map((item) => item.kind)).size > 1,
    nothingFound: isNothingFound(items),
  };
}

/**
 * What the closed "Checkpoint overview" says about itself, so a reviewer knows whether it is worth opening: how many of the
 * requirements the files mention, whether some file was uploaded again, and how many links there are.
 */
export function overviewHeadline(summary: CheckpointSummary, hasReplacedFile: boolean): string[] {
  const text = summaryText.card;
  const evidence = summariseEvidence(summary.requirements);
  const parts: string[] = [];
  if (evidence.searchable.length > 0) parts.push(text.overviewRequirements(evidence.found.length, evidence.searchable.length));
  if (hasReplacedFile) parts.push(text.overviewReuploaded);
  if (summary.links.length > 0) parts.push(text.overviewLinks(summary.links.length));
  return parts;
}

/** "CP1, CP2": the earlier checkpoints this one was checked against. */
export const comparedCheckpointsText = (numbers: ReadonlyArray<number>): string => numbers.map((number) => `CP${number}`).join(', ');

/** True when the figure history has something to say: a disagreement, or files that could not be checked. */
export const figureHistoryHasNews = (history: { conflicts: ReadonlyArray<unknown>; uncheckedFiles: ReadonlyArray<string> } | null): boolean =>
  history !== null && (history.conflicts.length > 0 || history.uncheckedFiles.length > 0);
