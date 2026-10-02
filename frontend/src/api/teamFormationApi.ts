import axiosClient from './axiosClient';
import type { CreateTeamFormationRequest } from '../types/teamFormation';

export const teamFormationApi = {
  create: (classId: string, request: CreateTeamFormationRequest) =>
    axiosClient.post(`/classes/${classId}/team-formations`, request),
  mine: (classId?: string) =>
    axiosClient.get('/team-formations/mine', { params: classId ? { classId } : undefined }),
  pendingInvitations: () => axiosClient.get('/team-formations/invitations/pending'),
  get: (formationId: string) => axiosClient.get(`/team-formations/${formationId}`),
  accept: (formationId: string) => axiosClient.post(`/team-formations/${formationId}/accept`),
  decline: (formationId: string) => axiosClient.post(`/team-formations/${formationId}/decline`),
  cancel: (formationId: string) => axiosClient.post(`/team-formations/${formationId}/cancel`),
};
