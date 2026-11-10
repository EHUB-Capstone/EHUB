import { useEffect, useState } from 'react';
import { reviewDeskApi } from '../api/reviewDeskApi';

// The answer belongs to the server, not to the user, so one successful answer is kept for the whole session of the page.
// A failure (not signed in, a student, the network) is never kept, so a later sign-in asks again.
let known: boolean | null = null;
let pending: Promise<boolean> | null = null;

export function loadAiSummaryEnabled(): Promise<boolean> {
  if (known !== null) return Promise.resolve(known);
  pending ??= reviewDeskApi.getStatus()
    .then((response) => { known = response.data.enabled === true; return known; })
    .catch(() => false)
    .finally(() => { pending = null; });
  return pending;
}

/** False until the server says AI summaries are on; everything AI-related stays hidden until then. */
export function useAiSummaryEnabled(): boolean {
  const [enabled, setEnabled] = useState(known === true);
  useEffect(() => {
    let active = true;
    loadAiSummaryEnabled().then((value) => { if (active) setEnabled(value); });
    return () => { active = false; };
  }, []);
  return enabled;
}
