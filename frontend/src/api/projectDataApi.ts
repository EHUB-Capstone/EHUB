import axiosClient from './axiosClient.ts';
import type { ApiEnvelope } from '../types/workspaceTools';
import type {
  ProjectAchievementHistory,
  ProjectAchievementsResult,
  ProjectDataDistribution,
  ProjectDataFilterOptions,
  ProjectDataPage,
  ProjectDataQuery,
  ProjectDataSummary,
  ProjectDataTrends,
  ProjectStartupIndustriesResult,
  ProjectStartupIndustryHistory,
  UpdateProjectAchievementsPayload,
  UpdateProjectStartupIndustriesPayload,
} from '../types/projectData';
import { toProjectDataRequestParams, toProjectDataSummaryParams, toProjectDataTrendParams } from '../utils/projectData.ts';

export const projectDataApi = {
  list: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataPage>> =>
    axiosClient.get('/project-data', { params: toProjectDataRequestParams(query), signal }),

  getSummary: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataSummary>> =>
    axiosClient.get('/project-data/summary', { params: toProjectDataSummaryParams(query), signal }),

  getDistribution: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataDistribution>> =>
    axiosClient.get('/project-data/distribution', { params: toProjectDataSummaryParams(query), signal }),

  getTrends: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataTrends>> =>
    axiosClient.get('/project-data/trends', { params: toProjectDataTrendParams(query), signal }),

  getAchievementHistory: (projectId: string, signal?: AbortSignal): Promise<ApiEnvelope<ProjectAchievementHistory>> =>
    axiosClient.get(`/project-data/${projectId}/achievements/history`, { signal }),

  getStartupIndustryHistory: (projectId: string, signal?: AbortSignal): Promise<ApiEnvelope<ProjectStartupIndustryHistory>> =>
    axiosClient.get(`/project-data/${projectId}/startup-industries/history`, { signal }),

  getFilterOptions: (signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataFilterOptions>> =>
    axiosClient.get('/project-data/filter-options', { signal }),

  updateAchievements: (
    projectId: string,
    payload: UpdateProjectAchievementsPayload,
  ): Promise<ApiEnvelope<ProjectAchievementsResult>> =>
    axiosClient.put(`/project-data/${projectId}/achievements`, payload),

  updateStartupIndustries: (
    projectId: string,
    payload: UpdateProjectStartupIndustriesPayload,
  ): Promise<ApiEnvelope<ProjectStartupIndustriesResult>> =>
    axiosClient.put(`/project-data/${projectId}/startup-industries`, payload),
};
