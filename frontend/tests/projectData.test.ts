import assert from 'node:assert/strict';
import test from 'node:test';
import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../src/api/axiosClient.ts';
import { projectDataApi } from '../src/api/projectDataApi.ts';
import type { ProjectDataItem } from '../src/types/projectData.ts';
import {
  DEFAULT_PROJECT_DATA_QUERY,
  activeFilterKeys,
  applyProjectDataChange,
  clearProjectDataFilters,
  displayList,
  displayText,
  hasActiveSearchOrFilters,
  mentorName,
  nextProjectDataSort,
  pageSummary,
  parseProjectDataQuery,
  projectDataOptionsKey,
  projectDataQueryKey,
  projectDataScopeKey,
  removedAchievements,
  sameAchievements,
  summarizeList,
  toProjectDataRequestParams,
  toProjectDataSearchParams,
  toggleAchievement,
} from '../src/utils/projectData.ts';

const parse = (search: string) => parseProjectDataQuery(new URLSearchParams(search));

test('an empty URL yields the default query and defaults are left out of the URL', () => {
  assert.deepEqual(parse(''), DEFAULT_PROJECT_DATA_QUERY);
  assert.equal(toProjectDataSearchParams(DEFAULT_PROJECT_DATA_QUERY).toString(), '');
  assert.equal(hasActiveSearchOrFilters(DEFAULT_PROJECT_DATA_QUERY), false);
});

test('query state round-trips through URL search params', () => {
  const query = {
    ...DEFAULT_PROJECT_DATA_QUERY,
    search: 'health',
    semesterId: 'semester-1',
    subjectCode: 'EXE201',
    group: 'FA26-G1',
    startupIndustry: 'Health Tech',
    lecturerId: 'lecturer-1',
    mentorId: 'mentor-1',
    achievement: 'Funded' as const,
    pageIndex: 3,
    pageSize: 50 as const,
    sortBy: 'semester' as const,
    isDescending: true,
  };
  assert.deepEqual(parseProjectDataQuery(toProjectDataSearchParams(query)), query);
});

test('invalid URL values fall back to safe defaults instead of reaching the API', () => {
  const parsed = parse('page=0&pageSize=7&sortBy=password&achievement=Famous&desc=yes&search=%20%20');
  assert.equal(parsed.pageIndex, 1);
  assert.equal(parsed.pageSize, 20);
  assert.equal(parsed.sortBy, 'projectName');
  assert.equal(parsed.achievement, '');
  assert.equal(parsed.isDescending, false);
  assert.equal(parsed.search, '');
  assert.equal(parse('page=-4').pageIndex, 1);
  assert.equal(parse('page=abc').pageIndex, 1);
  assert.equal(parse(`search=${'x'.repeat(250)}`).search.length, 100);
  assert.equal(parse('pageSize=100').pageSize, 100);
});

test('request params omit empty filters ("All") and always carry paging and sorting', () => {
  assert.deepEqual(toProjectDataRequestParams(DEFAULT_PROJECT_DATA_QUERY), {
    pageIndex: 1, pageSize: 20, sortBy: 'projectName', isDescending: false,
  });
  const params = toProjectDataRequestParams({ ...DEFAULT_PROJECT_DATA_QUERY, subjectCode: 'EXE201', achievement: 'Potential', search: 'ai' });
  assert.equal(params.subjectCode, 'EXE201');
  assert.equal(params.achievement, 'Potential');
  assert.equal(params.search, 'ai');
  assert.equal('group' in params, false);
  assert.equal('mentorId' in params, false);
});

test('changing search, filters, page size or sort returns to page one while paging keeps the page', () => {
  const onPageThree = { ...DEFAULT_PROJECT_DATA_QUERY, pageIndex: 3 };
  assert.equal(applyProjectDataChange(onPageThree, { search: 'x' }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { subjectCode: 'EXE201' }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { pageSize: 50 }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { sortBy: 'subject', isDescending: false }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { pageIndex: 4 }).pageIndex, 4);
  assert.equal(applyProjectDataChange(onPageThree, { achievement: '' }).pageIndex, 1);
});

test('clearing filters keeps sorting and page size but drops search, filters and page', () => {
  const dirty = {
    ...DEFAULT_PROJECT_DATA_QUERY, search: 'x', group: 'G1', achievement: 'Awarded' as const,
    pageIndex: 4, pageSize: 50 as const, sortBy: 'subject' as const, isDescending: true,
  };
  assert.deepEqual(activeFilterKeys(dirty), ['group', 'achievement']);
  assert.deepEqual(clearProjectDataFilters(dirty), {
    ...DEFAULT_PROJECT_DATA_QUERY, pageSize: 50, sortBy: 'subject', isDescending: true,
  });
});

test('clicking a sort header toggles direction on the same field and starts ascending on a new one', () => {
  assert.deepEqual(nextProjectDataSort(DEFAULT_PROJECT_DATA_QUERY, 'projectName'), { sortBy: 'projectName', isDescending: true });
  assert.deepEqual(nextProjectDataSort({ ...DEFAULT_PROJECT_DATA_QUERY, isDescending: true }, 'semester'), { sortBy: 'semester', isDescending: false });
});

test('query keys are scoped to the signed-in user so another account never reuses cached rows', () => {
  const a = projectDataQueryKey('user-a', DEFAULT_PROJECT_DATA_QUERY);
  const b = projectDataQueryKey('user-b', DEFAULT_PROJECT_DATA_QUERY);
  assert.notDeepEqual(a, b);
  assert.deepEqual(a.slice(0, 2), projectDataScopeKey('user-a'));
  assert.deepEqual(projectDataOptionsKey('user-a').slice(0, 2), projectDataScopeKey('user-a'));
  assert.notDeepEqual(a, projectDataQueryKey('user-a', { ...DEFAULT_PROJECT_DATA_QUERY, search: 'x' }));
});

test('achievement labels are independent, ordered and diffed against the saved set', () => {
  assert.deepEqual(toggleAchievement([], 'Awarded'), ['Awarded']);
  assert.deepEqual(toggleAchievement(['Awarded'], 'Potential'), ['Potential', 'Awarded']);
  assert.deepEqual(toggleAchievement(['Potential', 'Funded', 'Awarded'], 'Funded'), ['Potential', 'Awarded']);
  assert.equal(sameAchievements(['Funded', 'Potential'], ['Potential', 'Funded']), true);
  assert.equal(sameAchievements(['Potential'], ['Potential', 'Funded']), false);
  assert.deepEqual(removedAchievements(['Potential', 'Funded'], ['Funded']), ['Potential']);
  assert.deepEqual(removedAchievements(['Potential'], ['Potential', 'Awarded']), []);
});

test('missing values and collections render as a dash while real values are trimmed', () => {
  assert.equal(displayText(null), '-');
  assert.equal(displayText('  '), '-');
  assert.equal(displayText(' FA2026 '), 'FA2026');
  assert.equal(displayList([]), '-');
  assert.equal(displayList(undefined), '-');
  assert.equal(displayList([' G1 ', '', 'G2']), 'G1, G2');
  assert.equal(mentorName(null), '-');
  assert.deepEqual(summarizeList(['a', 'b', 'c', 'd']), { shown: ['a', 'b'], hidden: 2 });
  assert.deepEqual(summarizeList(null), { shown: [], hidden: 0 });
});

test('page summary describes the visible range and handles empty results', () => {
  assert.equal(pageSummary({ pageIndex: 1, pageSize: 20, totalItems: 0 }), 'No projects');
  assert.equal(pageSummary({ pageIndex: 2, pageSize: 20, totalItems: 35 }), '21–35 of 35 projects');
  assert.equal(pageSummary({ pageIndex: 9, pageSize: 20, totalItems: 35 }), '35 projects');
});

const item: ProjectDataItem = {
  projectId: 'project-1', teamId: 'team-1', classId: 'class-1', semesterId: 'semester-1', semesterCode: 'FA2026',
  subjectId: 'subject-1', subjectCode: 'EXE201', groups: [], projectName: 'Health app', description: null,
  startupIndustries: [], lecturer: null, mentor: null, academicMentor: null, achievements: [], rowVersion: '7',
};

test('the API client sends server-side query params, honours abort signals and maps the envelope', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    let seenParams: Record<string, unknown> = {};
    mock.onGet('/project-data').reply(config => {
      seenParams = config.params;
      return [200, { success: true, message: 'ok', data: { items: [item], pageIndex: 1, pageSize: 20, totalItems: 1, totalPages: 1 } }];
    });
    const response = await projectDataApi.list({ ...DEFAULT_PROJECT_DATA_QUERY, subjectCode: 'EXE201', achievement: 'Potential' });
    assert.equal(response.data.items[0].projectId, 'project-1');
    assert.equal(seenParams.subjectCode, 'EXE201');
    assert.equal(seenParams.achievement, 'Potential');
    assert.equal(seenParams.pageSize, 20);
    assert.equal('search' in seenParams, false);

    mock.onGet('/project-data/filter-options').reply(200, { success: true, message: 'ok', data: { semesters: [], subjects: [], groups: [], startupIndustries: [], lecturers: [], mentors: [], achievements: ['Potential', 'Funded', 'Awarded'] } });
    assert.deepEqual((await projectDataApi.getFilterOptions()).data.achievements, ['Potential', 'Funded', 'Awarded']);

    const controller = new AbortController();
    controller.abort();
    await assert.rejects(projectDataApi.list(DEFAULT_PROJECT_DATA_QUERY, controller.signal));
  } finally {
    mock.restore();
  }
});

test('updating achievements PUTs the full label set with the row version and surfaces conflicts', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    let body: unknown;
    mock.onPut('/project-data/project-1/achievements').replyOnce(config => {
      body = JSON.parse(config.data);
      return [200, { success: true, message: 'ok', data: { projectId: 'project-1', achievements: ['Potential', 'Funded'], rowVersion: '8' } }];
    });
    const result = await projectDataApi.updateAchievements('project-1', { achievements: ['Potential', 'Funded'], rowVersion: '7' });
    assert.deepEqual(body, { achievements: ['Potential', 'Funded'], rowVersion: '7' });
    assert.equal(result.data.rowVersion, '8');

    mock.onPut('/project-data/project-1/achievements').replyOnce(409, { success: false, message: 'changed', code: 'PROJECT_DATA_CONCURRENCY_CONFLICT' });
    await assert.rejects(
      projectDataApi.updateAchievements('project-1', { achievements: [], rowVersion: '7' }),
      (error: { response?: { status?: number; data?: { code?: string } } }) =>
        error.response?.status === 409 && error.response.data?.code === 'PROJECT_DATA_CONCURRENCY_CONFLICT',
    );
  } finally {
    mock.restore();
  }
});
