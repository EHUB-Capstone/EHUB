// @ts-nocheck
// src/api/checkpointApi.js
import axiosClient from './axiosClient.ts';
import storageClient from './storageClient.ts';
import { CHECKPOINT_UPLOAD_TIMEOUT_MS, checkpointPutTimeoutMs } from '../utils/checkpointUpload.ts';

function triggerBrowserDownload(href, fileName) {
  const link = document.createElement('a');
  link.href = href;
  link.download = fileName || 'checkpoint-file';
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();
}

export const checkpointApi = {
  // Subject-configured checkpoints plus the team's submission history
  getCheckpointData: (teamId) =>
    axiosClient.get(`/workspace/checkpoints/teams/${teamId}`),

  getFeedbackHistory: (teamId, checkpointNumber) =>
    axiosClient.get(`/workspace/checkpoints/teams/${teamId}/history`, {
      params: checkpointNumber ? { checkpointNumber } : {},
    }),

  // Direct upload (Student only): 1) ask the API for a presigned URL, 2) PUT the file straight to
  // object storage, 3) tell the API it is done. The file never passes through the API.
  initiateUpload: (teamId, checkpointNumber, { fileName, contentType, size }, options = {}) =>
    axiosClient.post(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/uploads`,
      { fileName, contentType, size },
      { timeout: CHECKPOINT_UPLOAD_TIMEOUT_MS, ...options },
    ),

  // Not an API call: storageClient sends no cookies or Authorization header to the storage host.
  putToPresignedUrl: (session, file, options = {}) =>
    storageClient.put(session.uploadUrl, file, {
      headers: session.headers,
      timeout: checkpointPutTimeoutMs(file.size),
      maxBodyLength: Infinity,
      ...options,
    }),

  // Safe to call again after a network failure: the API returns the file it already created.
  completeUpload: (teamId, checkpointNumber, uploadId, options = {}) =>
    axiosClient.post(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/uploads/${uploadId}/complete`,
      null,
      { timeout: CHECKPOINT_UPLOAD_TIMEOUT_MS, ...options },
    ),

  // Delete a submitted file
  deleteFile: (teamId, checkpointNumber, fileId) =>
    axiosClient.delete(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/files/${fileId}`
    ),

  createLink: (teamId, checkpointNumber, payload) =>
    axiosClient.post(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/links`,
      payload,
    ),

  updateLink: (teamId, checkpointNumber, linkId, payload) =>
    axiosClient.put(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/links/${linkId}`,
      payload,
    ),

  deleteLink: (teamId, checkpointNumber, linkId) =>
    axiosClient.delete(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/links/${linkId}`,
    ),

  // Small and legacy files stream through the API (axiosClient carries the in-memory access token).
  // Large R2 files (canDirectDownload) get a short-lived presigned URL and download straight from storage.
  downloadFile: async (teamId, checkpointNumber, fileId, fileName, { canDirectDownload = false } = {}) => {
    const base = `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/files/${fileId}`;
    if (canDirectDownload) {
      const response = await axiosClient.get(`${base}/download-url`);
      const url = response?.data?.url;
      if (!url) throw new Error('The download link could not be created.');
      triggerBrowserDownload(url, fileName);
      return;
    }

    const blob = await axiosClient.get(`${base}/download`, { responseType: 'blob' });
    const objectUrl = URL.createObjectURL(blob);
    triggerBrowserDownload(objectUrl, fileName);
    URL.revokeObjectURL(objectUrl);
  },

  // Where to read the preview PDF: a short-lived storage URL, "Preparing" while it is being generated, or "Proxy".
  getPreviewSource: (teamId, checkpointNumber, fileId, options = {}) => {
    const { retry = false, ...requestOptions } = options;
    return axiosClient.get(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/files/${fileId}/preview-source`,
      { params: retry ? { retry: true } : undefined, ...requestOptions },
    );
  },

  previewFile: (teamId, checkpointNumber, fileId, options = {}) =>
    axiosClient.get(
      `/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/files/${fileId}/preview`,
      {
        responseType: 'blob',
        timeout: 180_000,
        ...options,
      },
    ),

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
