import { createContext } from 'react';
import type { SummaryFeedback } from '../../../../types/checkpointSummary';

export interface SummaryActions {
  /** Opens a submitted file, at a page or slide when one is given (the page of a quoted sentence). */
  onOpenFile?: (file: { id: string; name: string }, page?: number) => void;
  /** Puts a passage into the comment box of the checkpoint. */
  onQuote?: (text: string) => void;
  /** Gives, changes or (with null) takes back the reviewer's verdict on a file's summary. */
  onRate?: (fileId: string, feedback: SummaryFeedback | null) => void;
  /** The file whose verdict is being saved, so its buttons wait. */
  ratingFileId?: string | null;
}

// Quotes appear deep inside the lists; the actions reach them through context instead of through every list.
export const SummaryActionsContext = createContext<SummaryActions>({});
