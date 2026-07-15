// @ts-nocheck
// src/api/checkpointApi.js
import axiosClient from './axiosClient';

export const checkpointApi = {
  // Static config — no team id required
  getConfig: () =>
    axiosClient.get('/workspace/checkpoints/config'),

  // All submissions + feedbacks for a team
  getCheckpointData: (teamId) =>
    axiosClient.get(`/workspace/checkpoints/teams/${teamId}`),

  getFeedbackHistory: (teamId, checkpointNumber) =>
    axiosClient.get(`/workspace/checkpoints/teams/${teamId}/history`, {
      params: checkpointNumber ? { checkpointNumber } : {},
    }),

  // Upload a file (Student only)
  uploadFile: (teamId, checkpointNumber, formData) =>
    axiosClient.post(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/upload`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    ),

  // Delete a submitted file
  deleteFile: (teamId, checkpointNumber, fileId) =>
    axiosClient.delete(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/files/${fileId}`
    ),

  // Download with JWT (browser tab links cannot send Authorization header)
  downloadFile: async (teamId, checkpointNumber, fileId, fileName) => {
    const token = localStorage.getItem('token');
    const base = axiosClient.defaults.baseURL || '/api';
    const url = `${base}/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/files/${fileId}/download`;

    const res = await fetch(url, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });

    if (!res.ok) {
      let message = 'Download failed';
      try {
        const err = await res.json();
        message = err.message || err.error || message;
      } catch {
        /* non-JSON error body */
      }
      throw new Error(message);
    }

    const blob = await res.blob();
    const objectUrl = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = fileName || 'checkpoint-file';
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(objectUrl);
  },

  // Update requirement text content (Student only)
  updateRequirements: (teamId, checkpointNumber, contents) =>
    axiosClient.put(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/requirements`,
      { contents }
    ),

  // Post a new comment or a reply (parentFeedbackId optional)
  addFeedback: (teamId, checkpointNumber, payload) =>
    axiosClient.post(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/feedback`,
      payload
    ),
};
