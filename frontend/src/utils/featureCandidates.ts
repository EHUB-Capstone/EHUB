import type { FeaturedCandidateDto, ShowcaseStage } from '../types/showcase';

export interface CandidateFilter {
  search?: string;
  stage?: ShowcaseStage;
  /** Admin only: restrict to one lecturer. */
  lecturer?: string;
  classCode?: string;
}

export interface CandidateGroup {
  classCode: string;
  lecturerName: string;
  items: FeaturedCandidateDto[];
  featuredCount: number;
}

export function filterCandidates(items: FeaturedCandidateDto[], filter: CandidateFilter): FeaturedCandidateDto[] {
  const term = filter.search?.trim().toLowerCase() ?? '';
  return items.filter(item =>
    (!filter.stage || item.developmentStage === filter.stage)
    && (!filter.lecturer || item.lecturerName === filter.lecturer)
    && (!filter.classCode || item.classCode === filter.classCode)
    && (!term || item.projectName.toLowerCase().includes(term) || item.teamName.toLowerCase().includes(term)));
}

/** Groups candidates by class, keeping the order the server returned (class code, then project name). */
export function groupByClass(items: FeaturedCandidateDto[]): CandidateGroup[] {
  const groups = new Map<string, CandidateGroup>();
  for (const item of items) {
    let group = groups.get(item.classCode);
    if (!group) {
      group = { classCode: item.classCode, lecturerName: item.lecturerName, items: [], featuredCount: 0 };
      groups.set(item.classCode, group);
    }
    group.items.push(item);
    if (item.isFeatured) group.featuredCount += 1;
  }
  return [...groups.values()];
}

/** How a selection splits into projects that will be featured and projects that will be unfeatured. */
export function splitSelection(items: FeaturedCandidateDto[], selected: ReadonlySet<string>): { toFeature: string[]; toUnfeature: string[] } {
  const toFeature: string[] = [];
  const toUnfeature: string[] = [];
  for (const item of items) {
    if (!selected.has(item.id)) continue;
    (item.isFeatured ? toUnfeature : toFeature).push(item.id);
  }
  return { toFeature, toUnfeature };
}
