import type { InfiniteData } from '@tanstack/react-query';
import type { GetShowcasesRequest, ShowcaseCardDto, ShowcaseDiscoveryPageDto, ShowcaseLaunchPeriod, ShowcaseStage, ShowcaseUpvoteDto } from '../types/showcase.ts';

export const DISCOVERY_PAGE_SIZE = 8;
export const SHOWCASE_LAUNCH_PERIODS: ReadonlyArray<{ value: ShowcaseLaunchPeriod; label: string }> = [
  { value: 'topThisWeek', label: 'Top launches this week' },
  { value: 'thisWeek', label: 'This week' },
  { value: 'lastWeek', label: 'Last week' },
];
/** How many projects the "Newly published" rail shows before "View more". */
export const NEWLY_PUBLISHED_COUNT = 5;
/** Upper bound of projects requested for the rail; the API allows at most 50 per page. */
export const NEWLY_PUBLISHED_FETCH_LIMIT = 50;
const NEWLY_PUBLISHED_WINDOW_MS = 7 * 24 * 60 * 60 * 1000;
const stages: ShowcaseStage[] = ['Idea', 'Mvp', 'Beta', 'Launched'];
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** True when a published showcase belongs in the seven-day "Newly published" rail. */
export function isNewlyPublished(publishedAt: string | null, now = Date.now()): boolean {
  if (!publishedAt) return false;
  const publishedTime = Date.parse(publishedAt);
  return Number.isFinite(publishedTime) && publishedTime <= now && now - publishedTime <= NEWLY_PUBLISHED_WINDOW_MS;
}

export function discoveryRequest(params: URLSearchParams): GetShowcasesRequest {
  const stage = stages.find(value => value.toLowerCase() === params.get('stage')?.toLowerCase());
  const semesterId = params.get('semesterId') ?? '';
  const page = Number(params.get('page'));
  const featured = params.get('featured');
  const sort = params.get('sort');
  const period = SHOWCASE_LAUNCH_PERIODS.find(item => item.value === params.get('period'))?.value;
  return {
    search: params.get('q')?.trim().slice(0, 100) || undefined,
    stage,
    industry: params.get('industry')?.trim().slice(0, 100) || undefined,
    semesterId: guid.test(semesterId) && !/^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(semesterId) ? semesterId : undefined,
    sort: params.get('tab') !== 'new' && (sort === 'popular' || sort === 'trending') ? 'trending' : 'newest',
    page: Number.isSafeInteger(page) && page >= 1 && page <= Math.floor(2147483647 / 50) ? page : 1,
    pageSize: DISCOVERY_PAGE_SIZE,
    ...(featured === 'true' || featured === 'false' ? { featured: featured === 'true' } : {}),
    ...(period ? { period } : {}),
  };
}

/** Keep the original position of each product while accepting fresher counters from later pages. */
export function mergeShowcasePages(pages: readonly ShowcaseDiscoveryPageDto[]): ShowcaseCardDto[] {
  const projects = new Map<string, ShowcaseCardDto>();
  for (const page of pages) {
    for (const project of page.items) {
      projects.set(project.id, project);
    }
  }
  return Array.from(projects.values());
}

/** Supports both existing page caches and the gallery's infinite feed without changing paging metadata. */
export function updateShowcaseDiscoveryUpvote(
  data: ShowcaseDiscoveryPageDto | InfiniteData<ShowcaseDiscoveryPageDto> | undefined,
  showcaseId: string,
  vote: ShowcaseUpvoteDto,
): typeof data {
  if (!data) return data;
  const updatePage = (page: ShowcaseDiscoveryPageDto): ShowcaseDiscoveryPageDto => ({
    ...page, items: page.items.map(item => item.id === showcaseId ? { ...item, ...vote } : item),
  });
  return 'pages' in data ? { ...data, pages: data.pages.map(updatePage) } : updatePage(data);
}

export function discoveryPages(page: number, totalPages: number): number[] {
  const start = Math.max(1, Math.min(page - 2, totalPages - 4));
  return Array.from({ length: Math.min(5, totalPages) }, (_, index) => start + index);
}
