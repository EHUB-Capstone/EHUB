// @ts-nocheck
// src/api/checkpointApi.js
import axiosClient from './axiosClient';

export const checkpointApi = {
  // Subject-configured checkpoints plus the team's latest submission data
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

  // Use axiosClient so its in-memory access token and refresh interceptor apply.
  downloadFile: async (teamId, checkpointNumber, fileId, fileName) => {
    const blob = await axiosClient.get(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/files/${fileId}/download`,
      { responseType: 'blob' },
    );
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
  deleteFeedback: (teamId, checkpointNumber, feedbackId) =>
    axiosClient.delete(`/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/feedback/${feedbackId}`),
};
