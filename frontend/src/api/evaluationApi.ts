// @ts-nocheck
// frontend/src/api/evaluationApi.js
import axiosClient from './axiosClient';
import type { EvaluationReportExportRequest } from '../types/evaluationGrading';

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
    return axiosClient.get(`/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/evaluation-summary`);
  },
  getGradingBatch: async (teamIds: string[]) => {
    return axiosClient.post('/workspace/checkpoints/evaluation-grading', { teamIds });
  },
  exportEvaluationReport: async (data: EvaluationReportExportRequest) => {
    return axiosClient.post('/workspace/checkpoints/evaluation-grading/export', data, { responseType: 'blob' });
  },
  getCheckpointHistory: async (teamId, checkpointNumber) => {
    return axiosClient.get(`/evaluations/team/${teamId}/checkpoints/${checkpointNumber}/history`);
  },
  getCourseAssessments: async (teamId) => {
    return axiosClient.get(`/workspace/checkpoints/teams/${teamId}/course-assessments`);
  },
  saveCourseAssessment: async (teamId, assessmentId, data) => {
    return axiosClient.put(`/workspace/checkpoints/teams/${teamId}/course-assessments/${assessmentId}`, data);
  },
  createCheckpointEvaluation: async (teamId, checkpointNumber, evaluationData) => {
    return axiosClient.post(`/workspace/checkpoints/teams/${teamId}/checkpoints/${checkpointNumber}/evaluations`, evaluationData);
  },
  updateCheckpointEvaluation: async (evaluationId, evaluationData) => {
    return axiosClient.put(`/workspace/checkpoints/evaluations/${evaluationId}`, evaluationData);
  },
  publishEvaluation: async (evaluationId) => {
    return axiosClient.put(`/workspace/checkpoints/evaluations/${evaluationId}/publish`, {});
  },
  unpublishEvaluation: async (evaluationId) => {
    return axiosClient.put(`/workspace/checkpoints/evaluations/${evaluationId}/unpublish`, {});
  },
  updatePublicationBatch: async (data) => {
    return axiosClient.put('/workspace/checkpoints/evaluations/publication/bulk', data);
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
  getGradingBatch,
  exportEvaluationReport,
  getCheckpointHistory,
  getCourseAssessments,
  saveCourseAssessment,
  createCheckpointEvaluation,
  updateCheckpointEvaluation,
  publishEvaluation,
  unpublishEvaluation,
  updatePublicationBatch,
  submitCheckpointEvaluation,
} = evaluationApi;
