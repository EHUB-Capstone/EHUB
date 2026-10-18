import type { ProjectDataDistributionBucket } from '../types/projectData';

/** Share of `total` as a whole percentage; 0 when there is nothing to divide by. */
export function distributionPercent(count: number, total: number): number {
  if (total <= 0 || count <= 0) return 0;
  return Math.round((count / total) * 100);
}

/** Bar length relative to the largest bucket of the same chart, so the biggest bar always fills the track. */
export function distributionBarWidth(count: number, max: number): number {
  if (max <= 0 || count <= 0) return 0;
  return Math.max(2, Math.round((count / max) * 100));
}

/**
 * Keeps the `limit` largest buckets in their given order and sums the rest, so a long industry list stays
 * readable. `otherCount` is a sum of bucket counts, which can exceed the number of projects when buckets overlap.
 */
export function limitDistributionBuckets(
  buckets: readonly ProjectDataDistributionBucket[],
  limit: number,
): { items: ProjectDataDistributionBucket[]; otherCount: number; otherBuckets: number } {
  if (buckets.length <= limit) return { items: [...buckets], otherCount: 0, otherBuckets: 0 };
  const rest = buckets.slice(limit);
  return {
    items: buckets.slice(0, limit),
    otherCount: rest.reduce((sum, bucket) => sum + bucket.count, 0),
    otherBuckets: rest.length,
  };
}

export const UNSPECIFIED_MAJOR = 'UNSPECIFIED';

export const majorLabel = (code: string): string => (code === UNSPECIFIED_MAJOR ? 'Unspecified' : code.replace(/_/g, ' '));
