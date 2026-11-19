import { useEffect, useState } from 'react';
import { checkpointSummaryApi } from '../../../../api/checkpointSummaryApi';
import type { CheckpointFigureHistory } from '../../../../types/checkpointSummary';

/**
 * Figures of this checkpoint against the team's earlier checkpoints. Uses figures already taken from each file (nothing is
 * downloaded or read again), so it loads by itself; a failure only hides it, since the rest of the card does not depend on it.
 * The card reads it, rather than the block that shows it, so it can tell whether "Needs attention" has anything to say.
 */
export function useFigureHistory(teamId: string, checkpointNumber: number, enabled: boolean): CheckpointFigureHistory | null {
  const [history, setHistory] = useState<CheckpointFigureHistory | null>(null);
  useEffect(() => {
    setHistory(null);
    if (!enabled || checkpointNumber <= 1) return undefined;
    const controller = new AbortController();
    checkpointSummaryApi.figureHistory(teamId, checkpointNumber, controller.signal)
      .then((response) => setHistory(response.data))
      .catch(() => setHistory(null));
    return () => controller.abort();
  }, [teamId, checkpointNumber, enabled]);
  return history;
}
