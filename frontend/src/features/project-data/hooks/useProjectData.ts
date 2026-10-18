import { useCallback, useEffect, useMemo, useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { projectDataApi } from '../../../api/projectDataApi';
import { useAuth } from '../../../hooks/useAuth';
import type {
  ProjectAchievementsResult,
  ProjectDataQuery,
  ProjectDataTab,
  ProjectStartupIndustriesResult,
  UpdateProjectAchievementsPayload,
  UpdateProjectStartupIndustriesPayload,
} from '../../../types/projectData';
import {
  PROJECT_DATA_SEARCH_DEBOUNCE_MS,
  applyProjectDataChange,
  clearProjectDataFilters,
  parseProjectDataQuery,
  parseProjectDataTab,
  projectDataDistributionKey,
  projectDataOptionsKey,
  projectDataQueryKey,
  projectDataScopeKey,
  projectDataSummaryKey,
  projectDataTrendsKey,
  toProjectDataSearchParams,
} from '../../../utils/projectData';

export function useProjectData() {
  const { user } = useAuth();
  const userId = user?.id;
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useMemo(() => parseProjectDataQuery(searchParams), [searchParams]);
  const tab = parseProjectDataTab(searchParams);

  // The typed text lives apart from the URL until the pause ends; null means "show what the URL says".
  const [searchDraft, setSearchDraft] = useState<string | null>(null);
  const searchInput = searchDraft ?? query.search;

  const updateQuery = useCallback((patch: Partial<ProjectDataQuery>, options?: { replace?: boolean }) => {
    if ('search' in patch) setSearchDraft(null);
    setSearchParams(
      current => toProjectDataSearchParams(
        applyProjectDataChange(parseProjectDataQuery(current), patch),
        parseProjectDataTab(current),
      ),
      { replace: options?.replace ?? false },
    );
  }, [setSearchParams]);

  const clearFilters = useCallback(() => {
    setSearchDraft(null);
    setSearchParams(current => toProjectDataSearchParams(
      clearProjectDataFilters(parseProjectDataQuery(current)),
      parseProjectDataTab(current),
    ));
  }, [setSearchParams]);

  // Switching tab keeps every filter, so the totals on both tabs describe the same rows.
  const setTab = useCallback((next: ProjectDataTab) => {
    setSearchParams(current => toProjectDataSearchParams(parseProjectDataQuery(current), next));
  }, [setSearchParams]);

  useEffect(() => {
    if (searchDraft === null || searchDraft.trim() === query.search) return undefined;
    const timer = window.setTimeout(
      () => updateQuery({ search: searchDraft.trim() }, { replace: true }),
      PROJECT_DATA_SEARCH_DEBOUNCE_MS,
    );
    return () => window.clearTimeout(timer);
  }, [searchDraft, query.search, updateQuery]);

  const enabled = Boolean(userId);
  const list = useQuery({
    queryKey: projectDataQueryKey(userId, query),
    queryFn: async ({ signal }) => (await projectDataApi.list(query, signal)).data,
    enabled,
    placeholderData: keepPreviousData,
    staleTime: 0,
  });
  const summary = useQuery({
    queryKey: projectDataSummaryKey(userId, query),
    queryFn: async ({ signal }) => (await projectDataApi.getSummary(query, signal)).data,
    enabled,
    placeholderData: keepPreviousData,
    staleTime: 0,
  });
  const distribution = useQuery({
    queryKey: projectDataDistributionKey(userId, query),
    queryFn: async ({ signal }) => (await projectDataApi.getDistribution(query, signal)).data,
    // Only the Analytics tab shows it, so the Overview does not pay for the extra request.
    enabled: enabled && tab === 'analytics',
    placeholderData: keepPreviousData,
    staleTime: 0,
  });
  const trends = useQuery({
    queryKey: projectDataTrendsKey(userId, query),
    queryFn: async ({ signal }) => (await projectDataApi.getTrends(query, signal)).data,
    enabled: enabled && tab === 'analytics',
    placeholderData: keepPreviousData,
    staleTime: 0,
  });
  const options = useQuery({
    queryKey: projectDataOptionsKey(userId),
    queryFn: async ({ signal }) => (await projectDataApi.getFilterOptions(signal)).data,
    enabled,
    staleTime: 60_000,
  });

  const updateAchievements = useMutation<
    ProjectAchievementsResult,
    unknown,
    { projectId: string; payload: UpdateProjectAchievementsPayload }
  >({
    mutationFn: async ({ projectId, payload }) => (await projectDataApi.updateAchievements(projectId, payload)).data,
    // Labels drive filters and totals, so refresh both the list and the options instead of patching rows.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectDataScopeKey(userId) }),
  });

  const updateStartupIndustries = useMutation<
    ProjectStartupIndustriesResult,
    unknown,
    { projectId: string; payload: UpdateProjectStartupIndustriesPayload }
  >({
    mutationFn: async ({ projectId, payload }) => (await projectDataApi.updateStartupIndustries(projectId, payload)).data,
    // Industries drive a filter, the options and the distribution, so refresh everything in scope.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectDataScopeKey(userId) }),
  });

  return {
    query,
    tab,
    setTab,
    updateQuery,
    clearFilters,
    searchInput,
    setSearchInput: setSearchDraft,
    list,
    summary,
    distribution,
    trends,
    options,
    updateAchievements,
    updateStartupIndustries,
  };
}
