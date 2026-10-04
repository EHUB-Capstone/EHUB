import axiosClient from './axiosClient';
import type { ApiEnvelope } from '../types/classes';
import type {
  TeamContinuityReport,
  TeamLineage,
  TeamLineageSubmission,
} from '../types/teamLineage';

export const teamLineageApi = {
  getLineage: (teamId: string, options: { signal?: AbortSignal } = {}): Promise<ApiEnvelope<TeamLineage>> =>
    axiosClient.get(`/teams/${teamId}/lineage`, { signal: options.signal }),

  getTermSubmissions: (
    teamId: string,
    termTeamId: string,
    options: { signal?: AbortSignal } = {},
  ): Promise<ApiEnvelope<TeamLineageSubmission[]>> =>
    axiosClient.get(`/teams/${teamId}/lineage/${termTeamId}/submissions`, { signal: options.signal }),

  getContinuityReport: (
    semesterId: string,
    options: { signal?: AbortSignal } = {},
  ): Promise<ApiEnvelope<TeamContinuityReport>> =>
    axiosClient.get('/admin/team-continuity', { params: { semesterId }, signal: options.signal }),
};
