import { useCallback, useEffect, useState } from 'react';
import axios from 'axios';
import { reviewDeskApi } from '../api/reviewDeskApi';
import { useAiSummaryEnabled } from './useAiSummaryEnabled';
import type { ReviewDeskInboxItem } from '../types/reviewDesk';

/**
 * What needs a reviewer's look today. Nothing is requested while AI summaries are off, so production never calls the
 * review desk; `enabled` lets the caller render nothing at all.
 */
export function useReviewInbox() {
  const enabled = useAiSummaryEnabled();
  const [items, setItems] = useState<ReviewDeskInboxItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!enabled) return undefined;
    const controller = new AbortController();
    setLoading(true);
    reviewDeskApi.getDesk(controller.signal)
      .then((response) => { setItems(response.data.inbox); setFailed(false); })
      .catch((error) => { if (!axios.isCancel(error)) setFailed(true); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [enabled, reloadKey]);

  const reload = useCallback(() => setReloadKey((value) => value + 1), []);
  return { enabled, items, loading, failed, reload };
}
