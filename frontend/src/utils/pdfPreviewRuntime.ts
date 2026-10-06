// PDF.js runtime shared by every document preview: one worker that stays alive between openings.
// (The cache of downloaded documents lives in previewDocumentCache.ts.) Warm it up from a Preview button's hover/focus
// so the worker script is downloaded and started before the user finishes clicking.
import { GlobalWorkerOptions, PDFWorker } from 'pdfjs-dist';
import pdfWorkerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';

GlobalWorkerOptions.workerSrc = pdfWorkerUrl;

let sharedWorker: PDFWorker | null = null;

/** The long-lived PDF.js worker; created on first use and recreated if it was destroyed. */
export function getSharedPdfWorker(): PDFWorker {
  if (!sharedWorker || sharedWorker.destroyed) {
    sharedWorker = new PDFWorker();
  }
  return sharedWorker;
}

/** Starts the worker ahead of the click. Safe to call repeatedly; never throws. */
export function warmUpPdfPreview(): void {
  try {
    void getSharedPdfWorker().promise.catch(() => undefined);
  } catch {
    // Preview still works: getDocument falls back to creating its own worker.
  }
}
