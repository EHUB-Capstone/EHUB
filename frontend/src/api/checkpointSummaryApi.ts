import axiosClient from './axiosClient.ts';
import type { ApiEnvelope } from '../types/workspaceTools';
import type { CheckpointFigureHistory, CheckpointSummary, CheckpointVersionChanges, SummaryDiagnostics, SummaryFeedback, SummaryFeedbackRequest } from '../types/checkpointSummary';

export const checkpointSummaryPath = (teamId: string, checkpointNumber: number) =>
  `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/ai-summary`;

// Admin, lecturer and mentor only. A request only queues the chosen files; poll get() for the results.
export const checkpointSummaryApi = {
  get: (teamId: string, checkpointNumber: number, signal?: AbortSignal) =>
    axiosClient.get(checkpointSummaryPath(teamId, checkpointNumber), { signal }) as Promise<ApiEnvelope<CheckpointSummary>>,
  request: (teamId: string, checkpointNumber: number, fileIds: string[], signal?: AbortSignal) =>
    axiosClient.post(checkpointSummaryPath(teamId, checkpointNumber), { fileIds }, { signal }) as Promise<ApiEnvelope<CheckpointSummary>>,
  /** Compares each re-uploaded file with its previous version. Read on request; not available while AI summaries are off. */
  changes: (teamId: string, checkpointNumber: number, signal?: AbortSignal) =>
    axiosClient.get(`${checkpointSummaryPath(teamId, checkpointNumber)}/changes`, { signal }) as Promise<ApiEnvelope<CheckpointVersionChanges>>,
  /** Figures this checkpoint states differently from the team's earlier checkpoints. Uses figures already taken from the files; not available while AI summaries are off. */
  figureHistory: (teamId: string, checkpointNumber: number, signal?: AbortSignal) =>
    axiosClient.get(`${checkpointSummaryPath(teamId, checkpointNumber)}/figure-history`, { signal }) as Promise<ApiEnvelope<CheckpointFigureHistory>>,
  /** The reviewer's own verdict on a finished summary. Giving it again replaces the earlier one. */
  setFeedback: (teamId: string, checkpointNumber: number, fileId: string, feedback: SummaryFeedbackRequest, signal?: AbortSignal) =>
    axiosClient.put(`${checkpointSummaryPath(teamId, checkpointNumber)}/files/${fileId}/feedback`, feedback, { signal }) as Promise<ApiEnvelope<SummaryFeedback>>,
  clearFeedback: (teamId: string, checkpointNumber: number, fileId: string, generationId?: string | null, signal?: AbortSignal) =>
    axiosClient.delete(`${checkpointSummaryPath(teamId, checkpointNumber)}/files/${fileId}/feedback`,
      { signal, params: generationId ? { generationId } : undefined }) as Promise<unknown>,
  /** Stops queued and running files; finished summaries stay. */
  cancel: (teamId: string, checkpointNumber: number, signal?: AbortSignal) =>
    axiosClient.post(`${checkpointSummaryPath(teamId, checkpointNumber)}/cancel`, null, { signal }) as Promise<ApiEnvelope<CheckpointSummary>>,
  /** Administrators only: model server, circuit, queue and error codes. Not available while AI summaries are off. */
  diagnostics: (signal?: AbortSignal) =>
    axiosClient.get('/workspace/checkpoints/ai-summary/diagnostics', { signal }) as Promise<ApiEnvelope<SummaryDiagnostics>>,
};
