// @ts-nocheck
// frontend/src/api/evaluationApi.js
import axiosClient from './axiosClient';

export const evaluationApi = {
  // Legacy Module 1 Methods
  getByStartup: async (startupIdeaId) => {
    return axiosClient.get(`/evaluations/startup/${startupIdeaId}`);
  },
  create: async (evaluationData) => {
    return axiosClient.post('/evaluations', evaluationData);
  },

  // Module 4 Methods
  getTeamEvaluations: async (teamId) => {
    return axiosClient.get(`/evaluations/team/${teamId}`);
  },
  createTeamEvaluation: async (teamId, evaluationData) => {
    return axiosClient.post(`/evaluations/team/${teamId}`, evaluationData);
  },
  updateTeamEvaluation: async (evaluationId, evaluationData) => {
    return axiosClient.put(`/evaluations/team/${evaluationId}`, evaluationData);
  },
  submitTeamEvaluation: async (evaluationId) => {
    return axiosClient.put(`/evaluations/team/${evaluationId}/submit`, {});
  },

  // Checkpoint-specific Methods
  getCheckpointEvaluations: async (teamId, checkpointNumber) => {
    return axiosClient.get(`/evaluations/team/${teamId}/checkpoints/${checkpointNumber}`);
  },
  getCheckpointSummary: async (teamId, checkpointNumber) => {
    return axiosClient.get(`/evaluations/team/${teamId}/checkpoints/${checkpointNumber}/summary`);
  },
  getCheckpointHistory: async (teamId, checkpointNumber) => {
    return axiosClient.get(`/evaluations/team/${teamId}/checkpoints/${checkpointNumber}/history`);
  },
  createCheckpointEvaluation: async (teamId, checkpointNumber, evaluationData) => {
    return axiosClient.post(`/evaluations/team/${teamId}/checkpoints/${checkpointNumber}`, evaluationData);
  },
  updateCheckpointEvaluation: async (evaluationId, evaluationData) => {
    return axiosClient.put(`/evaluations/team/${evaluationId}`, evaluationData);
  },
  submitCheckpointEvaluation: async (evaluationId) => {
    return axiosClient.put(`/evaluations/team/${evaluationId}/submit`, {});
  },
};

export const {
  getByStartup,
  create,
  getTeamEvaluations,
  createTeamEvaluation,
  updateTeamEvaluation,
  submitTeamEvaluation,
  getCheckpointEvaluations,
  getCheckpointSummary,
  getCheckpointHistory,
  createCheckpointEvaluation,
  updateCheckpointEvaluation,
  submitCheckpointEvaluation,
} = evaluationApi;
