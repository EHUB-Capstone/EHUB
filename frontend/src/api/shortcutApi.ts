import axiosClient from './axiosClient';
import type { ApiEnvelope, ProjectShortcut, SaveShortcutPayload } from '../types/workspaceTools';

const base = (teamId: string) => `/teams/${teamId}/shortcuts`;

export const shortcutApi = {
  getAll: (teamId: string): Promise<ApiEnvelope<ProjectShortcut[]>> => axiosClient.get(base(teamId)),
  create: (teamId: string, payload: SaveShortcutPayload): Promise<ApiEnvelope<ProjectShortcut>> => axiosClient.post(base(teamId), payload),
  update: (teamId: string, shortcutId: string, payload: SaveShortcutPayload): Promise<ApiEnvelope<ProjectShortcut>> => axiosClient.put(`${base(teamId)}/${shortcutId}`, payload),
  remove: (teamId: string, shortcutId: string): Promise<ApiEnvelope<null>> => axiosClient.delete(`${base(teamId)}/${shortcutId}`),
};
