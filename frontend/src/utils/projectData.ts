import {
  COMPETITION_OTHERS,
  COMPETITION_OTHER_NAME_MAX_LENGTH,
  COMPETITION_MAX_ENTRIES,
  COMPETITION_STAGE_LABELS,
  COMPETITION_YEAR_MAX,
  COMPETITION_YEAR_MIN,
  DEFAULT_PROJECT_DATA_TAB,
  PROJECT_ACHIEVEMENTS,
  PROJECT_COMPETITIONS,
  PROJECT_COMPETITION_STAGES,
  PROJECT_DATA_PAGE_SIZES,
  PROJECT_DATA_SEMESTER_TERMS,
  PROJECT_DATA_SORT_FIELDS,
  PROJECT_DATA_TABS,
} from '../types/projectData.ts';
import type {
  ProjectAchievement,
  ProjectAchievementHistoryItem,
  ProjectCompetition,
  ProjectCompetitionEntryPayload,
  ProjectCompetitionStage,
  ProjectDataItem,
  ProjectDataMentor,
  ProjectDataPageSize,
  ProjectDataQuery,
  ProjectDataSemesterTerm,
  ProjectDataSortField,
  ProjectDataTab,
} from '../types/projectData.ts';
import { haveSameIndustrySelection, resolveIndustryIds, validateIndustrySelection } from './projectWorkspace.ts';

export const PROJECT_DATA_SEARCH_MAX_LENGTH = 100;
export const PROJECT_ACHIEVEMENT_NOTE_MAX_LENGTH = 500;
export const PROJECT_DATA_SEARCH_DEBOUNCE_MS = 300;
export const PROJECT_DATA_EMPTY_VALUE = '-';

/** Most values one multi-select filter carries; the API rejects more, and it keeps shared links short. */
export const PROJECT_DATA_MAX_FILTER_VALUES = 100;

export const DEFAULT_PROJECT_DATA_QUERY: ProjectDataQuery = {
  search: '',
  subjectCode: '',
  semester: '',
  year: '',
  group: [],
  startupIndustry: [],
  major: [],
  lecturerId: [],
  mentorId: [],
  achievement: [],
  competition: [],
  competitionStage: [],
  competitionYear: [],
  pageIndex: 1,
  pageSize: 20,
  sortBy: 'classCode',
  isDescending: false,
};

type ScalarFilterKey = 'subjectCode' | 'semester' | 'year';
type MultiFilterKey =
  | 'group' | 'startupIndustry' | 'major' | 'lecturerId' | 'mentorId' | 'achievement'
  | 'competition' | 'competitionStage' | 'competitionYear';
export type FilterKey = ScalarFilterKey | MultiFilterKey;

// One value each: the top-row dropdowns. Several values each: the "More filters" panel.
const SCALAR_FILTER_KEYS: readonly ScalarFilterKey[] = ['subjectCode', 'semester', 'year'];
const MULTI_FILTER_KEYS: readonly MultiFilterKey[] = [
  'group', 'startupIndustry', 'major', 'lecturerId', 'mentorId', 'achievement', 'competition', 'competitionStage', 'competitionYear',
];
const FILTER_KEYS: readonly FilterKey[] = [...SCALAR_FILTER_KEYS, ...MULTI_FILTER_KEYS];

export const FILTER_LABELS: Record<FilterKey, string> = {
  subjectCode: 'Subject',
  semester: 'Semester',
  year: 'Year',
  group: 'Group',
  startupIndustry: 'Startup industry',
  major: 'Major',
  lecturerId: 'Lecturer',
  mentorId: 'Mentor',
  achievement: 'Achievement',
  competition: 'Competition',
  competitionStage: 'Competition stage',
  competitionYear: 'Competition year',
};

const toPositiveInteger = (value: string | null): number | null => {
  if (!value || !/^\d+$/.test(value)) return null;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= 1 ? parsed : null;
};

const isOneOf = <T extends string | number>(allowed: readonly T[], value: T): boolean => allowed.includes(value);

const isCompetitionYear = (value: string): boolean =>
  /^\d{4}$/.test(value) && Number(value) >= COMPETITION_YEAR_MIN && Number(value) <= COMPETITION_YEAR_MAX;

/** Reads the screen state from the URL; anything invalid falls back to the default instead of failing the request. */
export function parseProjectDataQuery(params: URLSearchParams): ProjectDataQuery {
  const text = (key: string, max = 100) => (params.get(key) ?? '').trim().slice(0, max);
  // A repeated key (?group=A&group=B) is a multi-select filter; invalid, blank and duplicate values are dropped.
  const list = (key: string, accept: (value: string) => string | null): string[] => {
    const values: string[] = [];
    for (const raw of params.getAll(key)) {
      const value = accept(raw.trim());
      if (value && !hasFilterValue(values, value)) values.push(value);
      if (values.length >= PROJECT_DATA_MAX_FILTER_VALUES) break;
    }
    return values;
  };
  const freeText = (max = 100) => (value: string) => value.slice(0, max) || null;
  const pageSize = toPositiveInteger(params.get('pageSize'));
  const sortBy = params.get('sortBy') ?? '';
  const semester = (params.get('semester') ?? '').trim().toUpperCase();
  const year = (params.get('year') ?? '').trim();

  return {
    search: text('search', PROJECT_DATA_SEARCH_MAX_LENGTH),
    subjectCode: text('subjectCode', 50),
    semester: isOneOf(PROJECT_DATA_SEMESTER_TERMS, semester as ProjectDataSemesterTerm) ? (semester as ProjectDataSemesterTerm) : '',
    year: /^\d{4}$/.test(year) ? year : '',
    group: list('group', freeText()),
    startupIndustry: list('startupIndustry', freeText()),
    major: list('major', value => (/^[A-Za-z_]{1,20}$/.test(value) ? value.toUpperCase() : null)),
    lecturerId: list('lecturerId', freeText(64)),
    mentorId: list('mentorId', freeText(64)),
    competition: list('competition', value => PROJECT_COMPETITIONS.find(name => name.toLowerCase() === value.toLowerCase()) ?? null),
    competitionStage: list(
      'competitionStage',
      value => PROJECT_COMPETITION_STAGES.find(stage => stage.toLowerCase() === value.toLowerCase()) ?? null,
    ) as ProjectCompetitionStage[],
    competitionYear: list('competitionYear', value => (isCompetitionYear(value) ? value : null)),
    achievement: orderAchievements(list('achievement', value => (isOneOf(PROJECT_ACHIEVEMENTS, value as ProjectAchievement) ? value : null)) as ProjectAchievement[]),
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

/** The open tab. It is screen state, not a filter, so it stays out of the query and the request keys. */
export function parseProjectDataTab(params: URLSearchParams): ProjectDataTab {
  const value = params.get('tab') ?? '';
  return PROJECT_DATA_TABS.includes(value as ProjectDataTab) ? (value as ProjectDataTab) : DEFAULT_PROJECT_DATA_TAB;
}

/** Serialises the screen state to the URL, leaving out defaults so shared links stay short. */
export function toProjectDataSearchParams(
  query: ProjectDataQuery,
  tab: ProjectDataTab = DEFAULT_PROJECT_DATA_TAB,
): URLSearchParams {
  const params = new URLSearchParams();
  if (tab !== DEFAULT_PROJECT_DATA_TAB) params.set('tab', tab);
  if (query.search) params.set('search', query.search);
  for (const key of SCALAR_FILTER_KEYS) {
    if (query[key]) params.set(key, query[key]);
  }
  for (const key of MULTI_FILTER_KEYS) {
    for (const value of query[key]) params.append(key, value);
  }
  if (query.pageIndex !== DEFAULT_PROJECT_DATA_QUERY.pageIndex) params.set('page', String(query.pageIndex));
  if (query.pageSize !== DEFAULT_PROJECT_DATA_QUERY.pageSize) params.set('pageSize', String(query.pageSize));
  if (query.sortBy !== DEFAULT_PROJECT_DATA_QUERY.sortBy) params.set('sortBy', query.sortBy);
  if (query.isDescending) params.set('desc', '1');
  return params;
}

/** Axios params for GET /project-data. "All" filters are simply not sent; a multi-select filter sends an array. */
export function toProjectDataRequestParams(query: ProjectDataQuery): Record<string, string | number | boolean | string[]> {
  return {
    pageIndex: query.pageIndex,
    pageSize: query.pageSize,
    sortBy: query.sortBy,
    isDescending: query.isDescending,
    ...toProjectDataSummaryParams(query),
  };
}

/**
 * The request params that decide which rows match: search and filters, without paging or sorting. A multi-select
 * filter is an array; the API client serialises it as a repeated key (group=A&group=B), see `projectDataApi`.
 */
export function toProjectDataSummaryParams(query: ProjectDataQuery): Record<string, string | string[]> {
  const params: Record<string, string | string[]> = {};
  if (query.search) params.search = query.search;
  for (const key of SCALAR_FILTER_KEYS) {
    if (query[key]) params[key] = query[key];
  }
  for (const key of MULTI_FILTER_KEYS) {
    if (query[key].length > 0) params[key] = [...query[key]];
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
  return FILTER_KEYS.filter(key => {
    const value = query[key];
    return Array.isArray(value) ? value.length > 0 : Boolean(value);
  });
}

/** One entry per selected value, so a multi-select filter with three values yields three entries. */
export function activeFilterValues(query: ProjectDataQuery): Array<{ key: FilterKey; value: string }> {
  return activeFilterKeys(query).flatMap(key => {
    const value = query[key];
    return Array.isArray(value) ? value.map(item => ({ key, value: item })) : [{ key, value }];
  });
}

/** The patch that removes one value from its filter (clears a single-value filter). */
export function removeFilterValue(query: ProjectDataQuery, key: FilterKey, value: string): Partial<ProjectDataQuery> {
  const current = query[key];
  return { [key]: Array.isArray(current) ? current.filter(item => item !== value) : '' } as Partial<ProjectDataQuery>;
}

export function hasFilterValue(values: readonly string[], value: string): boolean {
  const wanted = value.toLowerCase();
  return values.some(item => item.toLowerCase() === wanted);
}

/** Adds the value, or removes it when already selected (ignoring case). Stops adding at the per-filter limit. */
export function toggleFilterValue(values: readonly string[], value: string): string[] {
  if (hasFilterValue(values, value)) {
    const unwanted = value.toLowerCase();
    return values.filter(item => item.toLowerCase() !== unwanted);
  }
  return values.length >= PROJECT_DATA_MAX_FILTER_VALUES ? [...values] : [...values, value];
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

/** Paging and sorting are left out so turning a page never refetches the summary. */
export function projectDataSummaryKey(userId: string | undefined, query: ProjectDataQuery) {
  return ['project-data', userId ?? 'anonymous', 'summary', toProjectDataSummaryParams(query)] as const;
}

/** Same rows as the summary, so it shares the summary's params and ignores paging and sorting. */
export function projectDataDistributionKey(userId: string | undefined, query: ProjectDataQuery) {
  return ['project-data', userId ?? 'anonymous', 'distribution', toProjectDataSummaryParams(query)] as const;
}

/** A trend spans semesters, so the semester and year filters are left out. */
export function toProjectDataTrendParams(query: ProjectDataQuery): Record<string, string | string[]> {
  const { semester: _semester, year: _year, ...params } = toProjectDataSummaryParams(query);
  return params;
}

export function projectDataTrendsKey(userId: string | undefined, query: ProjectDataQuery) {
  return ['project-data', userId ?? 'anonymous', 'trends', toProjectDataTrendParams(query)] as const;
}

/** Includes the row version so a saved change makes an open history list refetch. */
export function projectDataHistoryKey(userId: string | undefined, projectId: string, rowVersion: string) {
  return ['project-data', userId ?? 'anonymous', 'history', projectId, rowVersion] as const;
}

/** "4 Oct 2026, 14:22": month spelled out so day/month order is never ambiguous, no seconds. */
export function formatHistoryDate(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return PROJECT_DATA_EMPTY_VALUE;
  return date.toLocaleString('en-GB', {
    day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
  });
}

export interface HistoryEntryView {
  isCarriedOver: boolean;
  /** Labels were added or removed (kept labels alone are not a change). */
  labelsChanged: boolean;
  /** Short tag for entries that only touched the note, or removed it. */
  noteTag: 'Note updated' | 'Note removed' | null;
  /** False when the entry could not be read; the summary sentence is shown instead. */
  hasStructure: boolean;
}

export function describeHistoryEntry(entry: ProjectAchievementHistoryItem): HistoryEntryView {
  const labelsChanged = entry.added.length > 0 || entry.removed.length > 0;
  const hasStructure = labelsChanged || entry.kept.length > 0 || entry.noteChanged;
  const noteTag = !entry.noteChanged
    ? null
    : entry.note === null
      ? 'Note removed'
      : labelsChanged ? null : 'Note updated';
  return { isCarriedOver: entry.action === 'ACHIEVEMENTS_CARRIED_OVER', labelsChanged, noteTag, hasStructure };
}

export function historyActionLabel(action: string): string {
  return action === 'ACHIEVEMENTS_CARRIED_OVER' ? 'Carried over' : 'Changed';
}

/** Says so when the list shows only the latest entries of a longer history. */
export function historyTruncationNotice(history: { totalCount: number; items: readonly unknown[] }): string | null {
  return history.totalCount > history.items.length
    ? `Showing the latest ${history.items.length} of ${history.totalCount} updates.`
    : null;
}

/** Includes the row version so a saved change makes an open industry history list refetch. */
export function projectStartupIndustryHistoryKey(userId: string | undefined, projectId: string, rowVersion: string) {
  return ['project-data', userId ?? 'anonymous', 'industry-history', projectId, rowVersion] as const;
}

export interface IndustryDraftView {
  /** What the picker shows: the user's pick, or the project's saved industries until they pick. */
  selectedIds: string[];
  /** Saved names that match no active industry anymore; saving replaces them. */
  retired: string[];
  /** True when saving would change the project's industries. */
  dirty: boolean;
  /** Why the pick cannot be saved; empty while it is valid or untouched. */
  validationMessage: string;
}

/**
 * `draftIds` is null until the user touches the picker, so an untouched dialog never offers a save. Until the active
 * industries have loaded nothing can be matched, so nothing is reported as retired or changed.
 */
export function describeIndustryDraft(
  savedNames: readonly string[],
  industries: ReadonlyArray<{ id: string; name: string }>,
  draftIds: readonly string[] | null,
  optionsLoaded: boolean,
): IndustryDraftView {
  const options = [...industries];
  const savedIds = resolveIndustryIds([...savedNames], options);
  const retired = optionsLoaded
    ? savedNames.filter(name => !options.some(option => haveSameIndustrySelection([name], [option.name])))
    : [];
  if (draftIds === null || !optionsLoaded) {
    return { selectedIds: draftIds ? [...draftIds] : savedIds, retired, dirty: false, validationMessage: '' };
  }

  const selectedIds = [...draftIds];
  const validationMessage = validateIndustrySelection(selectedIds);
  const dirty = !haveSameIndustrySelection(selectedIds, savedIds) || retired.length > 0;
  return { selectedIds, retired, dirty, validationMessage };
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

/** Trims the note; a blank note means no note (the API stores null). */
export function normalizeNote(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

export function sameNote(a: string | null | undefined, b: string | null | undefined): boolean {
  return normalizeNote(a) === normalizeNote(b);
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

/** The Startup Workspace of exactly this team (the route also used by the workspace hub). */
export function projectWorkspacePath(teamId: string): string {
  return `/workspace/teams/${encodeURIComponent(teamId)}`;
}

export function findItem(items: readonly ProjectDataItem[], projectId: string | null): ProjectDataItem | null {
  return projectId ? items.find(item => item.projectId === projectId) ?? null : null;
}

/** "FShark 2026 · Final round": what a competition badge says. */
export function competitionSummary(entry: Pick<ProjectCompetition, 'name' | 'year' | 'stage'>): string {
  return `${entry.name} ${entry.year} · ${COMPETITION_STAGE_LABELS[entry.stage]}`;
}

/** Collapses inner whitespace so "  Tech   Cup " and "Tech Cup" are the same name. */
export function cleanCompetitionName(value: string): string {
  return value.replace(/\s+/g, ' ').trim();
}

/** One editable row of a project's competitions. `key` only identifies the row while it is being edited. */
export interface CompetitionDraftEntry {
  key: string;
  /** A name from PROJECT_COMPETITIONS, or empty for a row the user has not filled in yet. */
  competition: string;
  otherName: string;
  /** Kept as typed so a half-typed year does not jump around; checked when the draft is described. */
  year: string;
  stage: ProjectCompetitionStage;
}

let competitionDraftCounter = 0;
const nextCompetitionDraftKey = () => `competition-draft-${++competitionDraftCounter}`;

export function toCompetitionDrafts(saved: readonly ProjectCompetition[]): CompetitionDraftEntry[] {
  return saved.map(entry => ({
    key: nextCompetitionDraftKey(),
    competition: entry.competition,
    otherName: entry.otherName ?? '',
    year: String(entry.year),
    stage: entry.stage,
  }));
}

/** A blank row: the competition is chosen by the user, the year defaults to the current one. */
export function newCompetitionDraft(year: number = new Date().getFullYear()): CompetitionDraftEntry {
  return { key: nextCompetitionDraftKey(), competition: '', otherName: '', year: String(year), stage: 'Registered' };
}

const competitionIdentity = (entry: { competition: string; otherName: string | null; year: string | number }): string => {
  const name = entry.competition === COMPETITION_OTHERS ? cleanCompetitionName(entry.otherName ?? '').toLowerCase() : '';
  return `${entry.competition}|${name}|${entry.year}`;
};

export interface CompetitionDraftView {
  /** What to send when saving; only meaningful while `blocked` is false. */
  payload: ProjectCompetitionEntryPayload[];
  /** Problem of each row that has one, by draft key. */
  errors: Record<string, string>;
  /** A problem that belongs to the list as a whole (too many rows). */
  listError: string;
  /** True when saving would be refused, so the Save button has to stay off. */
  blocked: boolean;
  /** True when the draft differs from what is saved. */
  dirty: boolean;
}

/**
 * Checks the rows the way the API will (so the user sees the problem before saving) and says whether the list
 * differs from the saved one. Two rows are the same entry when competition, year and (for Others) name match.
 */
export function describeCompetitionDraft(
  saved: readonly ProjectCompetition[],
  draft: readonly CompetitionDraftEntry[],
): CompetitionDraftView {
  const errors: Record<string, string> = {};
  const seen = new Set<string>();

  for (const row of draft) {
    const year = row.year.trim();
    const name = cleanCompetitionName(row.otherName);
    if (!row.competition) errors[row.key] = 'Choose a competition.';
    else if (row.competition === COMPETITION_OTHERS && !name) errors[row.key] = 'Enter the competition name.';
    else if (name.length > COMPETITION_OTHER_NAME_MAX_LENGTH) {
      errors[row.key] = `The name can have at most ${COMPETITION_OTHER_NAME_MAX_LENGTH} characters.`;
    } else if (!/^\d{4}$/.test(year) || Number(year) < COMPETITION_YEAR_MIN || Number(year) > COMPETITION_YEAR_MAX) {
      errors[row.key] = `Enter a year between ${COMPETITION_YEAR_MIN} and ${COMPETITION_YEAR_MAX}.`;
    } else {
      const identity = competitionIdentity({ competition: row.competition, otherName: name, year });
      if (seen.has(identity)) errors[row.key] = 'This competition is already listed for that year.';
      seen.add(identity);
    }
  }

  const listError = draft.length > COMPETITION_MAX_ENTRIES
    ? `A project can have at most ${COMPETITION_MAX_ENTRIES} competitions.`
    : '';
  const signature = (items: Array<{ identity: string; stage: string }>) =>
    items.map(item => `${item.identity}:${item.stage}`).sort().join('\n');
  const dirty = signature(draft.map(row => ({
    identity: competitionIdentity({ competition: row.competition, otherName: row.otherName, year: row.year.trim() }),
    stage: row.stage,
  }))) !== signature(saved.map(entry => ({
    identity: competitionIdentity(entry),
    stage: entry.stage,
  })));

  return {
    payload: draft.map(row => ({
      competition: row.competition,
      otherName: row.competition === COMPETITION_OTHERS ? cleanCompetitionName(row.otherName) : null,
      year: Number(row.year.trim()),
      stage: row.stage,
    })),
    errors,
    listError,
    blocked: Object.keys(errors).length > 0 || listError !== '',
    dirty,
  };
}
