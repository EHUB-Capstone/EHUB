// @ts-nocheck
import axiosClient from '../../../api/axiosClient';

const base = (teamId) => `/teams/${teamId}/shortcuts`;

export const shortcutApi = {
  getAll: (teamId) => axiosClient.get(base(teamId)),
  create: (teamId, payload) => axiosClient.post(base(teamId), payload),
  update: (teamId, shortcutId, payload) => axiosClient.put(`${base(teamId)}/${shortcutId}`, payload),
  remove: (teamId, shortcutId) => axiosClient.delete(`${base(teamId)}/${shortcutId}`),
};
