import {
  PROJECT_ACHIEVEMENTS,
  PROJECT_DATA_PAGE_SIZES,
  PROJECT_DATA_SEMESTER_TERMS,
  PROJECT_DATA_SORT_FIELDS,
} from '../types/projectData.ts';
import type {
  ProjectAchievement,
  ProjectDataItem,
  ProjectDataMentor,
  ProjectDataPageSize,
  ProjectDataQuery,
  ProjectDataSemesterTerm,
  ProjectDataSortField,
} from '../types/projectData.ts';

export const PROJECT_DATA_SEARCH_MAX_LENGTH = 100;
export const PROJECT_DATA_SEARCH_DEBOUNCE_MS = 300;
export const PROJECT_DATA_EMPTY_VALUE = '-';

export const DEFAULT_PROJECT_DATA_QUERY: ProjectDataQuery = {
  search: '',
  subjectCode: '',
  semester: '',
  year: '',
  group: '',
  startupIndustry: '',
  lecturerId: '',
  mentorId: '',
  achievement: '',
  pageIndex: 1,
  pageSize: 20,
  sortBy: 'classCode',
  isDescending: false,
};

type FilterKey = 'subjectCode' | 'semester' | 'year' | 'group' | 'startupIndustry' | 'lecturerId' | 'mentorId' | 'achievement';

const FILTER_KEYS: readonly FilterKey[] = [
  'subjectCode', 'semester', 'year', 'group', 'startupIndustry', 'lecturerId', 'mentorId', 'achievement',
];

export const FILTER_LABELS: Record<FilterKey, string> = {
  subjectCode: 'Subject',
  semester: 'Semester',
  year: 'Year',
  group: 'Group',
  startupIndustry: 'Startup industry',
  lecturerId: 'Lecturer',
  mentorId: 'Mentor',
  achievement: 'Achievement',
};

const toPositiveInteger = (value: string | null): number | null => {
  if (!value || !/^\d+$/.test(value)) return null;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= 1 ? parsed : null;
};

const isOneOf = <T extends string | number>(allowed: readonly T[], value: T): boolean => allowed.includes(value);

/** Reads the screen state from the URL; anything invalid falls back to the default instead of failing the request. */
export function parseProjectDataQuery(params: URLSearchParams): ProjectDataQuery {
  const text = (key: string, max = 100) => (params.get(key) ?? '').trim().slice(0, max);
  const pageSize = toPositiveInteger(params.get('pageSize'));
  const sortBy = params.get('sortBy') ?? '';
  const achievement = params.get('achievement') ?? '';
  const semester = (params.get('semester') ?? '').trim().toUpperCase();
  const year = (params.get('year') ?? '').trim();

  return {
    search: text('search', PROJECT_DATA_SEARCH_MAX_LENGTH),
    subjectCode: text('subjectCode', 50),
    semester: isOneOf(PROJECT_DATA_SEMESTER_TERMS, semester as ProjectDataSemesterTerm) ? (semester as ProjectDataSemesterTerm) : '',
    year: /^\d{4}$/.test(year) ? year : '',
    group: text('group'),
    startupIndustry: text('startupIndustry'),
    lecturerId: text('lecturerId', 64),
    mentorId: text('mentorId', 64),
    achievement: isOneOf(PROJECT_ACHIEVEMENTS, achievement as ProjectAchievement) ? (achievement as ProjectAchievement) : '',
    pageIndex: toPositiveInteger(params.get('page')) ?? DEFAULT_PROJECT_DATA_QUERY.pageIndex,
    pageSize: pageSize !== null && isOneOf(PROJECT_DATA_PAGE_SIZES, pageSize as ProjectDataPageSize)
      ? (pageSize as ProjectDataPageSize)
      : DEFAULT_PROJECT_DATA_QUERY.pageSize,
    sortBy: isOneOf(PROJECT_DATA_SORT_FIELDS, sortBy as ProjectDataSortField)
      ? (sortBy as ProjectDataSortField)
      : DEFAULT_PROJECT_DATA_QUERY.sortBy,
    isDescending: params.get('desc') === '1',
  };
}

/** Serialises the screen state to the URL, leaving out defaults so shared links stay short. */
export function toProjectDataSearchParams(query: ProjectDataQuery): URLSearchParams {
  const params = new URLSearchParams();
  if (query.search) params.set('search', query.search);
  for (const key of FILTER_KEYS) {
    if (query[key]) params.set(key, query[key]);
  }
  if (query.pageIndex !== DEFAULT_PROJECT_DATA_QUERY.pageIndex) params.set('page', String(query.pageIndex));
  if (query.pageSize !== DEFAULT_PROJECT_DATA_QUERY.pageSize) params.set('pageSize', String(query.pageSize));
  if (query.sortBy !== DEFAULT_PROJECT_DATA_QUERY.sortBy) params.set('sortBy', query.sortBy);
  if (query.isDescending) params.set('desc', '1');
  return params;
}

/** Axios params for GET /project-data. "All" filters are simply not sent. */
export function toProjectDataRequestParams(query: ProjectDataQuery): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {
    pageIndex: query.pageIndex,
    pageSize: query.pageSize,
    sortBy: query.sortBy,
    isDescending: query.isDescending,
  };
  if (query.search) params.search = query.search;
  for (const key of FILTER_KEYS) {
    if (query[key]) params[key] = query[key];
  }
  return params;
}

/** Any change other than paging itself returns the user to the first page. */
export function applyProjectDataChange(
  current: ProjectDataQuery,
  patch: Partial<ProjectDataQuery>,
): ProjectDataQuery {
  const next = { ...current, ...patch };
  const changesPageOnly = Object.keys(patch).every(key => key === 'pageIndex');
  return changesPageOnly ? next : { ...next, pageIndex: patch.pageIndex ?? 1 };
}

export function clearProjectDataFilters(current: ProjectDataQuery): ProjectDataQuery {
  return {
    ...DEFAULT_PROJECT_DATA_QUERY,
    pageSize: current.pageSize,
    sortBy: current.sortBy,
    isDescending: current.isDescending,
  };
}

export function activeFilterKeys(query: ProjectDataQuery): FilterKey[] {
  return FILTER_KEYS.filter(key => Boolean(query[key]));
}

export function hasActiveSearchOrFilters(query: ProjectDataQuery): boolean {
  return Boolean(query.search) || activeFilterKeys(query).length > 0;
}

export function nextProjectDataSort(
  current: ProjectDataQuery,
  field: ProjectDataSortField,
): Pick<ProjectDataQuery, 'sortBy' | 'isDescending'> {
  return current.sortBy === field
    ? { sortBy: field, isDescending: !current.isDescending }
    : { sortBy: field, isDescending: false };
}

export function projectDataQueryKey(userId: string | undefined, query: ProjectDataQuery) {
  return ['project-data', userId ?? 'anonymous', 'list', query] as const;
}

export function projectDataOptionsKey(userId: string | undefined) {
  return ['project-data', userId ?? 'anonymous', 'options'] as const;
}

export const projectDataScopeKey = (userId: string | undefined) => ['project-data', userId ?? 'anonymous'] as const;

export function orderAchievements(values: readonly ProjectAchievement[]): ProjectAchievement[] {
  return PROJECT_ACHIEVEMENTS.filter(name => values.includes(name));
}

export function toggleAchievement(
  current: readonly ProjectAchievement[],
  name: ProjectAchievement,
): ProjectAchievement[] {
  return orderAchievements(current.includes(name) ? current.filter(item => item !== name) : [...current, name]);
}

export function sameAchievements(a: readonly ProjectAchievement[], b: readonly ProjectAchievement[]): boolean {
  const left = orderAchievements(a);
  const right = orderAchievements(b);
  return left.length === right.length && left.every((name, index) => name === right[index]);
}

/** Labels currently on the project that the draft would take away. */
export function removedAchievements(
  saved: readonly ProjectAchievement[],
  draft: readonly ProjectAchievement[],
): ProjectAchievement[] {
  return orderAchievements(saved.filter(name => !draft.includes(name)));
}

export function displayList(values: readonly string[] | null | undefined): string {
  const items = (values ?? []).map(value => value.trim()).filter(Boolean);
  return items.length > 0 ? items.join(', ') : PROJECT_DATA_EMPTY_VALUE;
}

export function displayText(value: string | null | undefined): string {
  const text = value?.trim();
  return text ? text : PROJECT_DATA_EMPTY_VALUE;
}

export function mentorName(mentor: ProjectDataMentor | null | undefined): string {
  return displayText(mentor?.fullName);
}

/** Splits a long value list into the part shown in a table cell and the count hidden behind "+N". */
export function summarizeList(values: readonly string[] | null | undefined, visible = 2): { shown: string[]; hidden: number } {
  const items = (values ?? []).map(value => value.trim()).filter(Boolean);
  return { shown: items.slice(0, visible), hidden: Math.max(0, items.length - visible) };
}

export function pageSummary(page: { pageIndex: number; pageSize: number; totalItems: number }): string {
  if (page.totalItems === 0) return 'No projects';
  const first = (page.pageIndex - 1) * page.pageSize + 1;
  const last = Math.min(page.totalItems, page.pageIndex * page.pageSize);
  return first > page.totalItems ? `${page.totalItems} projects` : `${first}–${last} of ${page.totalItems} projects`;
}

export function findItem(items: readonly ProjectDataItem[], projectId: string | null): ProjectDataItem | null {
  return projectId ? items.find(item => item.projectId === projectId) ?? null : null;
}
