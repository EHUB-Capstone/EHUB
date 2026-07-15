// @ts-nocheck
// frontend/src/api/evaluationApi.js
import axios from 'axios';

const getBaseUrl = () => {
  const envUrl = import.meta.env.VITE_API_URL;
  if (!envUrl) return 'http://localhost:5000/api';
  return envUrl.endsWith('/api') ? envUrl : `${envUrl}/api`;
};
const API_URL = getBaseUrl();

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  return {
    headers: {
      Authorization: `Bearer ${token}`
    }
  };
};

export const evaluationApi = {
  // Legacy Module 1 Methods
  getByStartup: async (startupIdeaId) => {
    const res = await axios.get(`${API_URL}/evaluations/startup/${startupIdeaId}`, getAuthHeaders());
    return res.data;
  },
  create: async (evaluationData) => {
    const response = await axios.post(`${API_URL}/evaluations`, evaluationData, getAuthHeaders());
    return response.data;
  },

  // Module 4 Methods
  getTeamEvaluations: async (teamId) => {
    const response = await axios.get(`${API_URL}/evaluations/team/${teamId}`, getAuthHeaders());
    return response.data;
  },
  createTeamEvaluation: async (teamId, evaluationData) => {
    const response = await axios.post(`${API_URL}/evaluations/team/${teamId}`, evaluationData, getAuthHeaders());
    return response.data;
  },
  updateTeamEvaluation: async (evaluationId, evaluationData) => {
    const response = await axios.put(`${API_URL}/evaluations/team/${evaluationId}`, evaluationData, getAuthHeaders());
    return response.data;
  },
  submitTeamEvaluation: async (evaluationId) => {
    const response = await axios.put(`${API_URL}/evaluations/team/${evaluationId}/submit`, {}, getAuthHeaders());
    return response.data;
  },

  // Checkpoint-specific Methods
  getCheckpointEvaluations: async (teamId, checkpointNumber) => {
    const response = await axios.get(
      `${API_URL}/evaluations/team/${teamId}/checkpoints/${checkpointNumber}`,
      getAuthHeaders()
    );
    return response.data;
  },
  getCheckpointSummary: async (teamId, checkpointNumber) => {
    const response = await axios.get(
      `${API_URL}/evaluations/team/${teamId}/checkpoints/${checkpointNumber}/summary`,
      getAuthHeaders()
    );
    return response.data;
  },
  getCheckpointHistory: async (teamId, checkpointNumber) => {
    const response = await axios.get(
      `${API_URL}/evaluations/team/${teamId}/checkpoints/${checkpointNumber}/history`,
      getAuthHeaders()
    );
    return response.data;
  },
  createCheckpointEvaluation: async (teamId, checkpointNumber, evaluationData) => {
    const response = await axios.post(
      `${API_URL}/evaluations/team/${teamId}/checkpoints/${checkpointNumber}`,
      evaluationData,
      getAuthHeaders()
    );
    return response.data;
  },
  updateCheckpointEvaluation: async (evaluationId, evaluationData) => {
    const response = await axios.put(`${API_URL}/evaluations/team/${evaluationId}`, evaluationData, getAuthHeaders());
    return response.data;
  },
  submitCheckpointEvaluation: async (evaluationId) => {
    const response = await axios.put(`${API_URL}/evaluations/team/${evaluationId}/submit`, {}, getAuthHeaders());
    return response.data;
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
