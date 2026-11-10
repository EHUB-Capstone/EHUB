import type { SummaryDiagnostics, SummaryFeedbackCount } from '../types/checkpointSummary';

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
  if (!diagnostics.modelReachable) broken.push('The AI model server is not answering. Summaries cannot be made until it is back.');
  else if (!diagnostics.modelAvailable) broken.push(`The model "${diagnostics.model}" has not been pulled into the model server, so every summary fails.`);
  if (diagnostics.circuit === 'Open') attention.push('The worker is paused because the model failed several times in a row. It tries again by itself.');
  if (diagnostics.circuit === 'HalfOpen') attention.push('The worker is testing the model again after a pause.');
  if (diagnostics.expiredLeases > 0)
    attention.push(`${diagnostics.expiredLeases} file(s) were being processed when the worker stopped; they are picked up again on its next check.`);
  if (diagnostics.failed > 0) attention.push(`${diagnostics.failed} file(s) failed for good. See the reasons below.`);
  if (diagnostics.pendingRequested >= diagnostics.maxRequestedQueue)
    attention.push('The queue of requested summaries is full; new requests are refused until it shrinks.');
  const problems = [...broken, ...attention];
  if (broken.length > 0) return { tone: 'danger', label: 'Not working', problems };
  if (attention.length > 0) return { tone: 'warning', label: 'Needs attention', problems };
  return { tone: 'success', label: 'Healthy', problems };
}

/** "just now", "3 min", "2 h 5 min", "1 d 3 h": how long something has been waiting. */
export function ageText(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) return '—';
  const minutes = Math.floor(Math.max(0, seconds) / 60);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return minutes % 60 === 0 ? `${hours} h` : `${hours} h ${minutes % 60} min`;
  const days = Math.floor(hours / 24);
  return hours % 24 === 0 ? `${days} d` : `${days} d ${hours % 24} h`;
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
  Closed: { label: 'Running', tone: 'success' },
  Open: { label: 'Paused', tone: 'warning' },
  HalfOpen: { label: 'Testing', tone: 'info' },
};
