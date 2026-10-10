import axiosClient from './axiosClient';

export interface CheckpointDeadlineExtensionRequestPayload {
  reason: string;
}

export interface CheckpointDeadlineExtensionRequestResponse {
  id: string;
  teamId: string;
  teamName: string;
  classId: string;
  classCode: string;
  checkpointId: string;
  checkpointNumber: number;
  checkpointTitle: string;
  deadlineUtc: string;
  requestedAtUtc: string;
  reason: string;
}

export const checkpointDeadlineExtensionApi = {
  getMine: (teamId: string, checkpointNumber: number, deadlineUtc: string) =>
    axiosClient.get(`/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/deadline-extension-requests/mine`, {
      params: { deadlineUtc },
    }),
  create: (teamId: string, checkpointNumber: number, payload: CheckpointDeadlineExtensionRequestPayload) =>
    axiosClient.post(`/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/deadline-extension-requests`, payload),
};
