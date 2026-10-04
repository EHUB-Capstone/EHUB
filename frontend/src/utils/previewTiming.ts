// Measures click -> first rendered page of the document preview so the effect of every
// optimisation can be compared. Only the file id is used in marks (never the file name).

export interface PreviewTimingPerformance {
  mark(name: string): unknown;
  measure(name: string, startMark: string, endMark?: string): { duration: number } | undefined;
  clearMarks(name?: string): void;
  clearMeasures(name?: string): void;
}

const openMark = (fileId: string) => `preview:open:${fileId}`;
const renderedMark = (fileId: string) => `preview:first-page:${fileId}`;

function defaultPerformance(): PreviewTimingPerformance | null {
  return typeof performance !== 'undefined' && typeof performance.mark === 'function'
    ? performance
    : null;
}

/** Call when the user opens the preview (modal mounts with a file). */
export function markPreviewOpen(fileId: string, perf: PreviewTimingPerformance | null = defaultPerformance()): void {
  perf?.clearMarks(openMark(fileId));
  perf?.mark(openMark(fileId));
}

/** Call when page 1 has been painted. Returns the duration in ms, or null when no open mark exists. */
export function measurePreviewFirstPage(
  fileId: string,
  source: 'cache' | 'network' | 'proxy' | 'unknown' = 'unknown',
  perf: PreviewTimingPerformance | null = defaultPerformance(),
): number | null {
  if (!perf) return null;
  try {
    perf.mark(renderedMark(fileId));
    const measure = perf.measure(`preview:first-page:${source}`, openMark(fileId), renderedMark(fileId));
    perf.clearMarks(openMark(fileId));
    perf.clearMarks(renderedMark(fileId));
    if (!measure) return null;
    if (import.meta.env?.DEV) {
      console.info(`[E-HUB] Preview first page in ${Math.round(measure.duration)} ms (${source})`);
    }
    return measure.duration;
  } catch {
    // No matching open mark (for example the modal was reopened): nothing to report.
    return null;
  }
}
