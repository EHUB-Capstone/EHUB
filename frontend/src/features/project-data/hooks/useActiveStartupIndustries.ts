import { useQuery } from '@tanstack/react-query';
import { startupIndustryApi } from '../../../api/startupIndustryApi';
import type { StartupIndustryDto } from '../../../types/startupIndustries';
import type { ApiEnvelope } from '../../../types/workspaceTools';
import { parseApiError } from '../../../utils/apiError';

/** The active startup industries a project can be given; only requested while `enabled`, then kept for a minute. */
export function useActiveStartupIndustries(enabled: boolean) {
  const query = useQuery({
    queryKey: ['startup-industries', 'active-options'],
    queryFn: async ({ signal }) => {
      const response = await startupIndustryApi.getActiveOptions(signal) as unknown as ApiEnvelope<{ industries: StartupIndustryDto[] }>;
      return response.data.industries ?? [];
    },
    enabled,
    staleTime: 60_000,
  });

  return {
    industries: query.data ?? [],
    isLoading: enabled && query.isPending,
    isLoaded: query.isSuccess,
    errorMessage: query.isError ? parseApiError(query.error, 'Failed to load startup industries.').message : '',
    retry: () => { void query.refetch(); },
  };
}
