import axiosClient from './axiosClient';
import type { ApiEnvelope } from '../types/classes';
import type { TeamFeedbackOverview } from '../types/teamFeedbackOverview';

export const teamFeedbackOverviewApi = {
  getOverview: (teamId: string, options: { signal?: AbortSignal } = {}): Promise<ApiEnvelope<TeamFeedbackOverview>> =>
    axiosClient.get(`/teams/${teamId}/feedback-overview`, { signal: options.signal }),
};
