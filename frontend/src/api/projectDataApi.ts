import axiosClient from './axiosClient.ts';
import type { ApiEnvelope } from '../types/workspaceTools';
import type {
  ProjectAchievementHistory,
  ProjectAchievementsResult,
  ProjectDataFilterOptions,
  ProjectDataPage,
  ProjectDataQuery,
  ProjectDataSummary,
  UpdateProjectAchievementsPayload,
} from '../types/projectData';
import { toProjectDataRequestParams, toProjectDataSummaryParams } from '../utils/projectData.ts';

export const projectDataApi = {
  list: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataPage>> =>
    axiosClient.get('/project-data', { params: toProjectDataRequestParams(query), signal }),

  getSummary: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataSummary>> =>
    axiosClient.get('/project-data/summary', { params: toProjectDataSummaryParams(query), signal }),

  getAchievementHistory: (projectId: string, signal?: AbortSignal): Promise<ApiEnvelope<ProjectAchievementHistory>> =>
    axiosClient.get(`/project-data/${projectId}/achievements/history`, { signal }),

  getFilterOptions: (signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataFilterOptions>> =>
    axiosClient.get('/project-data/filter-options', { signal }),

  updateAchievements: (
    projectId: string,
    payload: UpdateProjectAchievementsPayload,
  ): Promise<ApiEnvelope<ProjectAchievementsResult>> =>
    axiosClient.put(`/project-data/${projectId}/achievements`, payload),
};
