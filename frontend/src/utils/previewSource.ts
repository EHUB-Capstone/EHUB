// Resolves "where can the browser read the PDF preview?" from the preview-source endpoint.
// The API never converts inside the request: while a DOCX/PPTX is still being turned into a PDF in
// the background it answers "Preparing", and the client simply asks again with a short back-off.

export type PreviewSourceStatus = 'Ready' | 'Preparing' | 'Proxy' | 'Failed' | 'TooLarge' | 'Unsupported';

export interface PreviewSourceResponse {
  status: PreviewSourceStatus;
  url?: string | null;
  expiresAt?: string | null;
  message?: string | null;
  retryAfterSeconds?: number | null;
}

/** Wait before poll 1, 2, 3 ... The last delay repeats until the timeout. */
export const PREVIEW_POLL_DELAYS_MS = [1_000, 2_000, 4_000] as const;
/** Slightly above the worst conversion time (60 s converter limit), so a slow file still resolves. */
export const PREVIEW_PREPARE_TIMEOUT_MS = 90_000;

export class PreviewPreparationTimeoutError extends Error {
  constructor() {
    super('The preview is taking longer than expected. You can retry or download the original file.');
    this.name = 'PreviewPreparationTimeoutError';
  }
}

/** The API says the preview cannot be shown (failed, too large, unsupported); the message is safe to display. */
export class PreviewUnavailableError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'PreviewUnavailableError';
  }
}

export function previewPollDelayMs(pollNumber: number): number {
  return PREVIEW_POLL_DELAYS_MS[Math.min(pollNumber, PREVIEW_POLL_DELAYS_MS.length - 1)];
}

function abortError(): Error {
  const error = new Error('Aborted');
  error.name = 'AbortError';
  return error;
}

function defaultSleep(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(abortError());
      return;
    }
    const timer = setTimeout(() => {
      signal?.removeEventListener('abort', onAbort);
      resolve();
    }, ms);
    const onAbort = () => {
      clearTimeout(timer);
      reject(abortError());
    };
    signal?.addEventListener('abort', onAbort, { once: true });
  });
}

export interface ResolvePreviewSourceOptions {
  signal?: AbortSignal;
  /** Asks the API to queue a Failed file again; only sent with the first request. */
  retry?: boolean;
  timeoutMs?: number;
  /** Called every time the answer is "Preparing" (elapsed time in ms). */
  onPreparing?: (elapsedMs: number) => void;
  sleep?: (ms: number, signal?: AbortSignal) => Promise<void>;
  now?: () => number;
}

/**
 * Asks for the preview source until it is no longer "Preparing".
 * Resolves with Ready or Proxy; throws {@link PreviewUnavailableError} for Failed/TooLarge/Unsupported and
 * {@link PreviewPreparationTimeoutError} when the preview is still not ready after the timeout.
 */
export async function resolvePreviewSource(
  fetchSource: (retry: boolean) => Promise<PreviewSourceResponse>,
  options: ResolvePreviewSourceOptions = {},
): Promise<PreviewSourceResponse> {
  const {
    signal,
    timeoutMs = PREVIEW_PREPARE_TIMEOUT_MS,
    onPreparing,
    sleep = defaultSleep,
    now = () => Date.now(),
  } = options;
  const startedAt = now();

  for (let poll = 0; ; poll += 1) {
    if (signal?.aborted) throw abortError();
    const source = await fetchSource(poll === 0 && options.retry === true);
    if (signal?.aborted) throw abortError();

    if (source.status === 'Ready' || source.status === 'Proxy') return source;
    if (source.status !== 'Preparing') {
      throw new PreviewUnavailableError(
        source.message || 'The preview could not be prepared. You can still download the original file.',
      );
    }

    const elapsed = now() - startedAt;
    if (elapsed >= timeoutMs) throw new PreviewPreparationTimeoutError();
    onPreparing?.(elapsed);
    await sleep(previewPollDelayMs(poll), signal);
  }
}
