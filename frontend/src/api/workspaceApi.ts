// @ts-nocheck
// frontend/src/api/workspaceApi.js
import axiosClient from './axiosClient';
import { classFeatureFlags, runClassFeatureRequest } from '../config/classFeatureFlags';

export const workspaceApi = {
  getMyWorkspace: () => axiosClient.get('/workspace/my-team'),
  getAccessibleTeams: () => axiosClient.get('/workspace/accessible-teams'),
  getTeamWorkspace: (teamId) => axiosClient.get(`/workspace/teams/${teamId}`),
  updateProjectDirection: (teamId, projectDirection) => runClassFeatureRequest(
    classFeatureFlags.projectDirection,
    'Project direction',
    () => axiosClient.put(`/workspace/teams/${teamId}/project-direction`, { projectDirection }),
  ),
  getClassProjectDirections: (classId) => runClassFeatureRequest(
    classFeatureFlags.projectDirection,
    'Project direction',
    () => axiosClient.get(`/workspace/classes/${classId}/project-directions`),
  ),
  reviewProjectDirection: (teamId, decision, comment) => runClassFeatureRequest(
    classFeatureFlags.projectDirection,
    'Project direction',
    () => axiosClient.put(`/workspace/teams/${teamId}/project-direction/review`, { decision, comment }),
  ),
  
  createProposal: (teamId, payload) => axiosClient.post(`/workspace/teams/${teamId}/proposal`, payload),
  getProposal: (teamId) => axiosClient.get(`/workspace/teams/${teamId}/proposal`),
  updateProposal: (proposalId, payload) => axiosClient.put(`/workspace/proposals/${proposalId}`, payload),
  submitProposal: (proposalId, payload = {}) => axiosClient.put(`/workspace/proposals/${proposalId}/submit`, payload),
  
  getProposalVersions: (proposalId) => axiosClient.get(`/workspace/proposals/${proposalId}/versions`),
  getProposalVersion: (proposalId, versionId) => axiosClient.get(`/workspace/proposals/${proposalId}/versions/${versionId}`),
  restoreProposalVersion: (proposalId, versionId) => axiosClient.post(`/workspace/proposals/${proposalId}/restore/${versionId}`),
  
  uploadPitchDeck: (teamId, formData) => axiosClient.post(`/workspace/teams/${teamId}/decks/upload`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' }
  }),
  getPitchDecks: (teamId) => axiosClient.get(`/workspace/teams/${teamId}/decks`),
  deletePitchDeck: (deckId) => axiosClient.delete(`/workspace/decks/${deckId}`),
  downloadPitchDeckUrl: (deckId) => `${axiosClient.defaults.baseURL || '/api'}/workspace/decks/${deckId}/download`
};
