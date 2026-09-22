import axiosClient from './axiosClient';
import type { ApiResponse } from '../types/auth';
import type {
  CreateProjectProposalRequest,
  ProjectProposal,
  ProjectProposalVersion,
  ProjectProposalVersionSummary,
  RestoreProjectProposalVersionRequest,
  ReviewProjectProposalRequest,
  SubmitProjectProposalRequest,
  UpdateProjectProposalRequest,
} from '../types/projectProposal';
import type { ProjectWorkspaceDetail, ProjectWorkspaceProfile } from '../types/projectWorkspace';
import type { ApiEnvelope, WorkspaceOption } from '../types/workspaceTools';

export const workspaceApi = {
  getMyWorkspace: () => axiosClient.get('/workspace/my-team'),
  getAccessibleTeams: (): Promise<ApiEnvelope<WorkspaceOption[]>> => axiosClient.get('/workspace/accessible-teams'),
  getTeamWorkspace: (teamId: string): Promise<ApiResponse<ProjectWorkspaceDetail>> => axiosClient.get(`/workspace/teams/${teamId}`),
  createWorkspace: (teamId: string, payload: unknown): Promise<ApiResponse<ProjectWorkspaceProfile>> => axiosClient.post(`/workspace/teams/${teamId}`, payload),
  updateWorkspaceProfile: (teamId: string, payload: unknown): Promise<ApiResponse<ProjectWorkspaceProfile>> => axiosClient.put(`/workspace/teams/${teamId}/profile`, payload),
  createProposal: (teamId: string, payload: CreateProjectProposalRequest): Promise<ApiResponse<ProjectProposal>> => axiosClient.post(`/workspace/teams/${teamId}/proposal`, payload),
  getProposal: (teamId: string): Promise<ApiResponse<ProjectProposal>> => axiosClient.get(`/workspace/teams/${teamId}/proposal`),
  updateProposal: (proposalId: string, payload: UpdateProjectProposalRequest): Promise<ApiResponse<ProjectProposal>> => axiosClient.put(`/workspace/proposals/${proposalId}`, payload),
  submitProposal: (proposalId: string, payload: SubmitProjectProposalRequest): Promise<ApiResponse<ProjectProposal>> => axiosClient.post(`/workspace/proposals/${proposalId}/submit`, payload),
  
  getProposalVersions: (proposalId: string): Promise<ApiResponse<ProjectProposalVersionSummary[]>> => axiosClient.get(`/workspace/proposals/${proposalId}/versions`),
  getProposalVersion: (proposalId: string, versionId: string): Promise<ApiResponse<ProjectProposalVersion>> => axiosClient.get(`/workspace/proposals/${proposalId}/versions/${versionId}`),
  restoreProposalVersion: (proposalId: string, versionId: string, payload: RestoreProjectProposalVersionRequest): Promise<ApiResponse<ProjectProposal>> => axiosClient.post(`/workspace/proposals/${proposalId}/versions/${versionId}/restore`, payload),
  reviewProposal: (proposalId: string, payload: ReviewProjectProposalRequest): Promise<ApiResponse<ProjectProposal>> => axiosClient.post(`/workspace/proposals/${proposalId}/review`, payload),
  
  uploadPitchDeck: (teamId: string, formData: FormData) => axiosClient.post(`/workspace/teams/${teamId}/decks/upload`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' }
  }),
  getPitchDecks: (teamId: string) => axiosClient.get(`/workspace/teams/${teamId}/decks`),
  deletePitchDeck: (deckId: string) => axiosClient.delete(`/workspace/decks/${deckId}`),
  downloadPitchDeckUrl: (deckId: string) => `${axiosClient.defaults.baseURL || '/api'}/workspace/decks/${deckId}/download`
};
