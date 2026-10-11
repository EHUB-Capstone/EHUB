import { useEffect, useRef, useState } from 'react';
import { clockSkewMs, remainingMs } from '../utils/teamFormation';

/**
 * Milliseconds left until `expiresAtUtc`, ticking every second. The server clock snapshot
 * (`serverTimeUtc`) corrects for a wrong device clock. Returns null when there is no deadline.
 * `onExpire` fires once when the countdown is observed reaching zero.
 */
export function useCountdown(
  expiresAtUtc: string | null | undefined,
  serverTimeUtc?: string,
  onExpire?: () => void,
): number | null {
  const [clock, setClock] = useState(() => {
    const now = Date.now();
    return { nowMs: now, skewMs: clockSkewMs(serverTimeUtc, now) };
  });
  const onExpireRef = useRef(onExpire);
  const previousRef = useRef<number | null>(null);

  useEffect(() => {
    onExpireRef.current = onExpire;
  });

  useEffect(() => {
    if (!expiresAtUtc) return undefined;
    const skewMs = clockSkewMs(serverTimeUtc, Date.now());
    const tick = () => setClock({ nowMs: Date.now(), skewMs });
    const first = window.setTimeout(tick, 0);
    const interval = window.setInterval(tick, 1000);
    return () => {
      window.clearTimeout(first);
      window.clearInterval(interval);
    };
  }, [expiresAtUtc, serverTimeUtc]);

  const remaining = remainingMs(expiresAtUtc, clock.nowMs, clock.skewMs);

  useEffect(() => {
    if (remaining === null) {
      previousRef.current = null;
      return;
    }
    if (remaining === 0 && previousRef.current !== null && previousRef.current > 0) onExpireRef.current?.();
    previousRef.current = remaining;
  }, [remaining]);

  return remaining;
}
