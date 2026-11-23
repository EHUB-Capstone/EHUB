import type { SummaryDiagnostics, SummaryFeedbackCount } from '../types/checkpointSummary';
import i18n from '../i18n/index.ts';

type Tone = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'brand';

export interface DiagnosticsHealth {
  tone: Tone;
  label: string;
  /** What to look at first, most serious first. Empty when all is well. */
  problems: string[];
}

/** Files waiting for the model, of either kind. */
export const waitingFiles = (diagnostics: SummaryDiagnostics) => diagnostics.pendingRequested + diagnostics.pendingAutomatic;

/**
 * One verdict for the page header, so an administrator does not have to read every number: the model server first (nothing works
 * without it), then the circuit that pauses the worker, then the queue.
 */
export function diagnosticsHealth(diagnostics: SummaryDiagnostics): DiagnosticsHealth {
  const broken: string[] = [];
  const attention: string[] = [];
  if (!diagnostics.modelReachable) broken.push(i18n.t('utils:summaryDiagnostics.theAiModelServerIsNot'));
  else if (!diagnostics.modelAvailable) broken.push(i18n.t('utils:summaryDiagnostics.theModelHasNotBeenPulled', { model: diagnostics.model }));
  if (diagnostics.circuit === 'Open') attention.push(i18n.t('utils:summaryDiagnostics.theWorkerIsPausedBecauseThe'));
  if (diagnostics.circuit === 'HalfOpen') attention.push(i18n.t('utils:summaryDiagnostics.theWorkerIsTestingTheModel'));
  if (diagnostics.expiredLeases > 0)
    attention.push(`${diagnostics.expiredLeases} file(s) were being processed when the worker stopped; they are picked up again on its next check.`);
  if (diagnostics.failed > 0) attention.push(`${diagnostics.failed} file(s) failed for good. See the reasons below.`);
  if (diagnostics.pendingRequested >= diagnostics.maxRequestedQueue)
    attention.push(i18n.t('utils:summaryDiagnostics.theQueueOfRequestedSummariesIs'));
  const problems = [...broken, ...attention];
  if (broken.length > 0) return { tone: 'danger', label: i18n.t('utils:summaryDiagnostics.notWorking'), problems };
  if (attention.length > 0) return { tone: 'warning', label: i18n.t('utils:summaryDiagnostics.needsAttention'), problems };
  return { tone: 'success', label: i18n.t('utils:summaryDiagnostics.healthy'), problems };
}

/** "just now", "3 min", "2 h 5 min", "1 d 3 h": how long something has been waiting. */
export function ageText(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) return '—';
  const minutes = Math.floor(Math.max(0, seconds) / 60);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return minutes % 60 === 0 ? `${hours} h` : i18n.t('utils:summaryDiagnostics.hMin', { hours, v2: minutes % 60 });
  const days = Math.floor(hours / 24);
  return hours % 24 === 0 ? `${days} d` : i18n.t('utils:summaryDiagnostics.dH', { days, v2: hours % 24 });
}

const FAILURE_LABELS: Record<string, string> = {
  WORKSPACE_SUMMARY_UNREADABLE: 'The file has no readable text (scan, image or password-protected)',
  WORKSPACE_SUMMARY_NO_CONTENT: 'The file was removed or has no content',
  WORKSPACE_SUMMARY_MODEL_UNREACHABLE: 'The AI model could not be reached',
  WORKSPACE_SUMMARY_MODEL_TIMEOUT: 'The AI model took too long',
  WORKSPACE_SUMMARY_MODEL_BAD_ANSWER: 'The AI model returned an answer that could not be used',
  WORKSPACE_SUMMARY_STORAGE_UNAVAILABLE: 'The file storage was unavailable',
  WORKSPACE_SUMMARY_UNAVAILABLE: 'The summary failed unexpectedly',
  UNKNOWN: 'No reason was recorded',
};

/** A reason a file failed, in plain words; an unknown code is shown as it is so it can still be looked up. */
export const failureLabel = (code: string) => FAILURE_LABELS[code] ?? code;

/** Share of reviewers' verdicts that said "useful", as a whole percent; null while nobody has rated this prompt version. */
export function helpfulShare(row: SummaryFeedbackCount): number | null {
  const total = row.helpful + row.notHelpful;
  return total === 0 ? null : Math.round((row.helpful / total) * 100);
}

/** A share from few verdicts says little; the page says so instead of letting it look like a measurement. */
export const FEW_VERDICTS = 20;
export const hasFewVerdicts = (row: SummaryFeedbackCount) => row.helpful + row.notHelpful < FEW_VERDICTS;

export const circuitView: Record<SummaryDiagnostics['circuit'], { label: string; tone: Tone }> = {
  Closed: { get label() { return i18n.t('utils:summaryDiagnostics.running'); }, tone: 'success' },
  Open: { get label() { return i18n.t('utils:summaryDiagnostics.paused'); }, tone: 'warning' },
  HalfOpen: { get label() { return i18n.t('utils:summaryDiagnostics.testing'); }, tone: 'info' },
};
