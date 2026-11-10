import axiosClient from './axiosClient.ts';
import type { ApiEnvelope } from '../types/workspaceTools';
import type { ReviewDesk } from '../types/reviewDesk';

const base = '/workspace/checkpoints';

// Admin, lecturer and mentor only; the server limits every answer to the classes and teams the signed-in person reviews.
export const reviewDeskApi = {
  /** Whether AI summaries, and with them the review inbox, exist on this server. Answered from the server's own setting. */
  getStatus: (signal?: AbortSignal) =>
    axiosClient.get(`${base}/ai-summary/status`, { signal }) as Promise<ApiEnvelope<{ enabled: boolean }>>,
  getDesk: (signal?: AbortSignal) =>
    axiosClient.get(`${base}/review-desk`, { signal }) as Promise<ApiEnvelope<ReviewDesk>>,
  /** Records that the reviewer opened the team's newest version, so it stops being "new" for them. */
  markSeen: (teamId: string, checkpointNumber: number) =>
    axiosClient.post(`${base}/teams/${teamId}/checkpoints/${checkpointNumber}/review-seen`) as Promise<unknown>,
};
