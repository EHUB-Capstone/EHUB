// @ts-nocheck
import axiosClient from './axiosClient';

export const teamWorkspaceApi = {
  getCurrentWorkspace: () => axiosClient.get('/team-workspaces/current'),
  getWorkspaceContext: (teamId) => axiosClient.get(`/team-workspaces/team/${teamId}/context`),
  getLineageWorkspaces: (lineageId) => axiosClient.get(`/team-workspaces/lineage/${lineageId}`),
  linkWorkspaces: (payload) => axiosClient.post('/team-workspaces/link', payload),
  pivotWorkspace: (payload) => axiosClient.post('/team-workspaces/pivot', payload),
};
