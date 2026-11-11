import axiosClient from './axiosClient.ts';
import type { ApiEnvelope } from '../types/workspaceTools';
import type {
  ProjectAchievementHistory,
  ProjectAchievementsResult,
  ProjectCompetitionsResult,
  ProjectDataDistribution,
  ProjectDataFilterOptions,
  ProjectDataPage,
  ProjectDataQuery,
  ProjectDataSummary,
  ProjectDataTrends,
  ProjectStartupIndustriesResult,
  ProjectStartupIndustryHistory,
  UpdateProjectAchievementsPayload,
  UpdateProjectCompetitionsPayload,
  UpdateProjectStartupIndustriesPayload,
} from '../types/projectData';
import { toProjectDataRequestParams, toProjectDataSummaryParams, toProjectDataTrendParams } from '../utils/projectData.ts';

// Multi-select filters are arrays. ASP.NET binds a repeated key (group=A&group=B), not axios' default group[]=A&group[]=B.
const repeatedKeys = { indexes: null } as const;

export const projectDataApi = {
  /** List rows include nullable showcaseMetrics with current lineage-wide counters for staff reference. */
  list: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataPage>> =>
    axiosClient.get('/project-data', { params: toProjectDataRequestParams(query), paramsSerializer: repeatedKeys, signal }),

  getSummary: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataSummary>> =>
    axiosClient.get('/project-data/summary', { params: toProjectDataSummaryParams(query), paramsSerializer: repeatedKeys, signal }),

  getDistribution: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataDistribution>> =>
    axiosClient.get('/project-data/distribution', { params: toProjectDataSummaryParams(query), paramsSerializer: repeatedKeys, signal }),

  getTrends: (query: ProjectDataQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectDataTrends>> =>
    axiosClient.get('/project-data/trends', { params: toProjectDataTrendParams(query), paramsSerializer: repeatedKeys, signal }),

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

  updateCompetitions: (
    projectId: string,
    payload: UpdateProjectCompetitionsPayload,
  ): Promise<ApiEnvelope<ProjectCompetitionsResult>> =>
    axiosClient.put(`/project-data/${projectId}/competitions`, payload),

  /** The project data report (.xlsx) for the current search and filters. The client interceptor hands back the Blob. */
  exportProjectData: (query: ProjectDataQuery, signal?: AbortSignal): Promise<Blob> =>
    axiosClient.get('/project-data/export', {
      params: toProjectDataSummaryParams(query),
      paramsSerializer: repeatedKeys,
      responseType: 'blob',
      signal,
    }),

  updateStartupIndustries: (
    projectId: string,
    payload: UpdateProjectStartupIndustriesPayload,
  ): Promise<ApiEnvelope<ProjectStartupIndustriesResult>> =>
    axiosClient.put(`/project-data/${projectId}/startup-industries`, payload),
};
