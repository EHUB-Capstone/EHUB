// Coordinates overlapping requests for the resource currently on screen (for example team + checkpoint).
// - A response may only be applied while its key is still the active one, so a slow request for the previous
//   checkpoint cannot overwrite the new one.
// - Responses for the same key are applied in arrival order unless a newer request was already applied, so one slow
//   duplicate cannot keep a loading placeholder up once an earlier response has arrived.
// - Callers may share the request that is already running for the same key instead of starting another one.
export interface RequestGate {
  begin: (key: string) => number;
  accepts: (key: string, requestSeq: number) => boolean;
  markApplied: (requestSeq: number) => void;
  isActive: (key: string) => boolean;
  reuse: (key: string) => Promise<unknown> | null;
  track: (key: string, promise: Promise<unknown>) => void;
  release: (promise: Promise<unknown>) => void;
}

export function createRequestGate(): RequestGate {
  let activeKey = '';
  let lastSeq = 0;
  let appliedSeq = 0;
  let inFlight: { key: string; promise: Promise<unknown> } | null = null;

  return {
    begin(key) {
      activeKey = key;
      lastSeq += 1;
      return lastSeq;
    },
    accepts: (key, requestSeq) => activeKey === key && requestSeq >= appliedSeq,
    markApplied(requestSeq) {
      appliedSeq = requestSeq;
    },
    isActive: (key) => activeKey === key,
    reuse: (key) => (inFlight?.key === key ? inFlight.promise : null),
    track(key, promise) {
      inFlight = { key, promise };
    },
    release(promise) {
      if (inFlight?.promise === promise) inFlight = null;
    },
  };
}
