import { useCallback, useEffect, useMemo, useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { projectDataApi } from '../../../api/projectDataApi';
import { useAuth } from '../../../hooks/useAuth';
import type {
  ProjectAchievementsResult,
  ProjectDataQuery,
  UpdateProjectAchievementsPayload,
} from '../../../types/projectData';
import {
  PROJECT_DATA_SEARCH_DEBOUNCE_MS,
  applyProjectDataChange,
  clearProjectDataFilters,
  parseProjectDataQuery,
  projectDataOptionsKey,
  projectDataQueryKey,
  projectDataScopeKey,
  toProjectDataSearchParams,
} from '../../../utils/projectData';

export function useProjectData() {
  const { user } = useAuth();
  const userId = user?.id;
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useMemo(() => parseProjectDataQuery(searchParams), [searchParams]);

  // The typed text lives apart from the URL until the pause ends; null means "show what the URL says".
  const [searchDraft, setSearchDraft] = useState<string | null>(null);
  const searchInput = searchDraft ?? query.search;

  const updateQuery = useCallback((patch: Partial<ProjectDataQuery>, options?: { replace?: boolean }) => {
    if ('search' in patch) setSearchDraft(null);
    setSearchParams(
      current => toProjectDataSearchParams(applyProjectDataChange(parseProjectDataQuery(current), patch)),
      { replace: options?.replace ?? false },
    );
  }, [setSearchParams]);

  const clearFilters = useCallback(() => {
    setSearchDraft(null);
    setSearchParams(current => toProjectDataSearchParams(clearProjectDataFilters(parseProjectDataQuery(current))));
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

  return {
    query,
    updateQuery,
    clearFilters,
    searchInput,
    setSearchInput: setSearchDraft,
    list,
    options,
    updateAchievements,
  };
}
