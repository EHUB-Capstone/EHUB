import axiosClient from './axiosClient';
import type { CreateTeamFormationRequest, FinalizeTeamFormationRequest } from '../types/teamFormation';

export const teamFormationApi = {
  create: (classId: string, request: CreateTeamFormationRequest) =>
    axiosClient.post(`/classes/${classId}/team-formations`, request),
  invite: (formationId: string, studentIds: string[]) =>
    axiosClient.post(`/team-formations/${formationId}/invitations`, { studentIds }),
  mine: (classId?: string) =>
    axiosClient.get('/team-formations/mine', { params: classId ? { classId } : undefined }),
  pendingInvitations: () => axiosClient.get('/team-formations/invitations/pending'),
  get: (formationId: string) => axiosClient.get(`/team-formations/${formationId}`),
  accept: (formationId: string) => axiosClient.post(`/team-formations/${formationId}/accept`),
  decline: (formationId: string) => axiosClient.post(`/team-formations/${formationId}/decline`),
  leave: (formationId: string) => axiosClient.post(`/team-formations/${formationId}/leave`),
  finalize: (formationId: string, request: FinalizeTeamFormationRequest) =>
    axiosClient.post(`/team-formations/${formationId}/finalize`, request),
  cancel: (formationId: string) => axiosClient.post(`/team-formations/${formationId}/cancel`),
};
