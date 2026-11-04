import axiosClient from './axiosClient.ts';
import type { ApiEnvelope } from '../types/workspaceTools';
import type { CheckpointSummary } from '../types/checkpointSummary';

export const checkpointSummaryPath = (teamId: string, checkpointNumber: number) =>
  `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/ai-summary`;

// Admin, lecturer and mentor only. A request only queues the chosen files; poll get() for the results.
export const checkpointSummaryApi = {
  get: (teamId: string, checkpointNumber: number, signal?: AbortSignal) =>
    axiosClient.get(checkpointSummaryPath(teamId, checkpointNumber), { signal }) as Promise<ApiEnvelope<CheckpointSummary>>,
  request: (teamId: string, checkpointNumber: number, fileIds: string[], signal?: AbortSignal) =>
    axiosClient.post(checkpointSummaryPath(teamId, checkpointNumber), { fileIds }, { signal }) as Promise<ApiEnvelope<CheckpointSummary>>,
  /** Stops queued and running files; finished summaries stay. */
  cancel: (teamId: string, checkpointNumber: number, signal?: AbortSignal) =>
    axiosClient.post(`${checkpointSummaryPath(teamId, checkpointNumber)}/cancel`, null, { signal }) as Promise<ApiEnvelope<CheckpointSummary>>,
};
