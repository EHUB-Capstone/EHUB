import axiosClient from './axiosClient';
import type { ApiEnvelope } from '../types/workspaceTools';
import type { SubmissionAnalyticsFilters, SubmissionAnalyticsResponse } from '../types/submissionAnalytics';

export const submissionAnalyticsApi = {
  get: (params: SubmissionAnalyticsFilters, signal?: AbortSignal): Promise<ApiEnvelope<SubmissionAnalyticsResponse>> =>
    axiosClient.get('/dashboard/submission-analytics', { params, signal }),
};
