import { useQuery } from '@tanstack/react-query';
import { showcaseApi } from '../api/showcaseApi';

export function useShowcaseViolationCategories(enabled = true) {
  return useQuery({
    queryKey: ['showcase', 'violation-categories'],
    queryFn: ({ signal }) => showcaseApi.getViolationCategories(signal),
    enabled,
    staleTime: 5 * 60_000,
  });
}
