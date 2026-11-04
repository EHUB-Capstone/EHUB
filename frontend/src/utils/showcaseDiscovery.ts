import type { GetShowcasesRequest, ShowcaseStage } from '../types/showcase.ts';

export const DISCOVERY_PAGE_SIZE = 8;
const stages: ShowcaseStage[] = ['Idea', 'Mvp', 'Beta', 'Launched'];
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function discoveryRequest(params: URLSearchParams): GetShowcasesRequest {
  const stage = stages.find(value => value.toLowerCase() === params.get('stage')?.toLowerCase());
  const semesterId = params.get('semesterId') ?? '';
  const page = Number(params.get('page'));
  return {
    search: params.get('q')?.trim().slice(0, 100) || undefined,
    stage,
    industry: params.get('industry')?.trim().slice(0, 100) || undefined,
    semesterId: guid.test(semesterId) && !/^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(semesterId) ? semesterId : undefined,
    sort: params.get('tab') !== 'new' && params.get('sort') === 'popular' ? 'popular' : 'newest',
    page: Number.isSafeInteger(page) && page >= 1 && page <= Math.floor(2147483647 / 50) ? page : 1,
    pageSize: DISCOVERY_PAGE_SIZE,
  };
}

export function discoveryPages(page: number, totalPages: number): number[] {
  const start = Math.max(1, Math.min(page - 2, totalPages - 4));
  return Array.from({ length: Math.min(5, totalPages) }, (_, index) => start + index);
}
