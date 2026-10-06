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
  projectDataSummaryKey,
  projectWorkspacePath,
  projectDataHistoryKey,
  historyActionLabel,
  describeHistoryEntry,
  formatHistoryDate,
  historyTruncationNotice,
  normalizeNote,
  sameNote,
  toProjectDataSummaryParams,
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
    subjectCode: 'EXE201',
    semester: 'FA' as const,
    year: '2026',
    group: 'FA26-G1',
    startupIndustry: 'Health Tech',
    lecturerId: 'lecturer-1',
    mentorId: 'mentor-1',
    achievement: 'Funded' as const,
    pageIndex: 3,
    pageSize: 50 as const,
    sortBy: 'group' as const,
    isDescending: true,
  };
  assert.deepEqual(parseProjectDataQuery(toProjectDataSearchParams(query)), query);
});

test('invalid URL values fall back to safe defaults instead of reaching the API', () => {
  const parsed = parse('page=0&pageSize=7&sortBy=password&achievement=Famous&desc=yes&search=%20%20');
  assert.equal(parsed.pageIndex, 1);
  assert.equal(parsed.pageSize, 20);
  assert.equal(parsed.sortBy, 'classCode');
  assert.equal(parse('sortBy=subject').sortBy, 'classCode');
  assert.equal(parse('sortBy=projectName').sortBy, 'projectName');
  assert.equal(parsed.achievement, '');
  assert.equal(parsed.isDescending, false);
  assert.equal(parsed.search, '');
  assert.equal(parse('page=-4').pageIndex, 1);
  assert.equal(parse('page=abc').pageIndex, 1);
  assert.equal(parse(`search=${'x'.repeat(250)}`).search.length, 100);
  assert.equal(parse('pageSize=100').pageSize, 100);
  assert.equal(parse('semester=fa&year=2026').semester, 'FA');
  assert.equal(parse('semester=Fall&year=26').semester, '');
  assert.equal(parse('semester=FA&year=26').year, '');
  assert.equal(parse('year=20266').year, '');
});

test('request params omit empty filters ("All") and always carry paging and sorting', () => {
  assert.deepEqual(toProjectDataRequestParams(DEFAULT_PROJECT_DATA_QUERY), {
    pageIndex: 1, pageSize: 20, sortBy: 'classCode', isDescending: false,
  });
  const params = toProjectDataRequestParams({ ...DEFAULT_PROJECT_DATA_QUERY, subjectCode: 'EXE201', semester: 'SU', year: '2026', achievement: 'Potential', search: 'ai' });
  assert.equal(params.subjectCode, 'EXE201');
  assert.equal(params.semester, 'SU');
  assert.equal(params.year, '2026');
  assert.equal(params.achievement, 'Potential');
  assert.equal(params.search, 'ai');
  assert.equal('group' in params, false);
  assert.equal('mentorId' in params, false);
  assert.equal('semester' in toProjectDataRequestParams(DEFAULT_PROJECT_DATA_QUERY), false);
  assert.equal('year' in toProjectDataRequestParams(DEFAULT_PROJECT_DATA_QUERY), false);
});

test('changing search, filters, page size or sort returns to page one while paging keeps the page', () => {
  const onPageThree = { ...DEFAULT_PROJECT_DATA_QUERY, pageIndex: 3 };
  assert.equal(applyProjectDataChange(onPageThree, { search: 'x' }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { subjectCode: 'EXE201' }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { semester: 'FA' }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { year: '2026' }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { pageSize: 50 }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { sortBy: 'classCode', isDescending: false }).pageIndex, 1);
  assert.equal(applyProjectDataChange(onPageThree, { pageIndex: 4 }).pageIndex, 4);
  assert.equal(applyProjectDataChange(onPageThree, { achievement: '' }).pageIndex, 1);
});

test('clearing filters keeps sorting and page size but drops search, filters and page', () => {
  const dirty = {
    ...DEFAULT_PROJECT_DATA_QUERY, search: 'x', semester: 'SP' as const, year: '2026', group: 'G1', achievement: 'Awarded' as const,
    pageIndex: 4, pageSize: 50 as const, sortBy: 'group' as const, isDescending: true,
  };
  assert.deepEqual(activeFilterKeys(dirty), ['semester', 'year', 'group', 'achievement']);
  assert.deepEqual(clearProjectDataFilters(dirty), {
    ...DEFAULT_PROJECT_DATA_QUERY, pageSize: 50, sortBy: 'group', isDescending: true,
  });
});

test('clicking a sort header toggles direction on the same field and starts ascending on a new one', () => {
  assert.deepEqual(nextProjectDataSort(DEFAULT_PROJECT_DATA_QUERY, 'classCode'), { sortBy: 'classCode', isDescending: true });
  assert.deepEqual(nextProjectDataSort(DEFAULT_PROJECT_DATA_QUERY, 'group'), { sortBy: 'group', isDescending: false });
  assert.deepEqual(nextProjectDataSort({ ...DEFAULT_PROJECT_DATA_QUERY, isDescending: true }, 'semester'), { sortBy: 'semester', isDescending: false });
});

test('summary params carry only search and filters, so paging and sorting never change the counters', () => {
  assert.deepEqual(toProjectDataSummaryParams(DEFAULT_PROJECT_DATA_QUERY), {});
  const filtered = { ...DEFAULT_PROJECT_DATA_QUERY, search: 'ai', semester: 'SU' as const, year: '2026', achievement: 'Funded' as const };
  assert.deepEqual(toProjectDataSummaryParams(filtered), { search: 'ai', semester: 'SU', year: '2026', achievement: 'Funded' });
  const paged = { ...filtered, pageIndex: 5, pageSize: 100 as const, sortBy: 'group' as const, isDescending: true };
  assert.deepEqual(projectDataSummaryKey('user-a', paged), projectDataSummaryKey('user-a', filtered));
  assert.notDeepEqual(projectDataSummaryKey('user-a', filtered), projectDataSummaryKey('user-a', { ...filtered, year: '2025' }));
  assert.notDeepEqual(projectDataSummaryKey('user-a', filtered), projectDataSummaryKey('user-b', filtered));
  assert.deepEqual(projectDataSummaryKey('user-a', filtered).slice(0, 2), projectDataScopeKey('user-a'));
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

test('notes are trimmed, blank means no note and comparison ignores surrounding whitespace', () => {
  assert.equal(normalizeNote('  Strong pilot  '), 'Strong pilot');
  assert.equal(normalizeNote('   '), null);
  assert.equal(normalizeNote(null), null);
  assert.equal(sameNote(' same ', 'same'), true);
  assert.equal(sameNote('', null), true);
  assert.equal(sameNote('a', 'b'), false);
  assert.equal(sameNote('a', null), false);
});

test('the history list says when it shows only the latest entries and labels carried-over ones', () => {
  assert.equal(historyTruncationNotice({ totalCount: 3, items: [1, 2, 3] }), null);
  assert.equal(historyTruncationNotice({ totalCount: 0, items: [] }), null);
  assert.equal(historyTruncationNotice({ totalCount: 63, items: new Array(50).fill(0) }), 'Showing the latest 50 of 63 updates.');
  assert.equal(historyActionLabel('ACHIEVEMENTS_CARRIED_OVER'), 'Carried over');
  assert.equal(historyActionLabel('PROJECT_ACHIEVEMENTS_CHANGED'), 'Changed');
});

const historyEntry = (patch: Record<string, unknown> = {}) => ({
  id: 'e1', action: 'PROJECT_ACHIEVEMENTS_CHANGED', summary: 'Achievements changed.', actorName: 'EHUB',
  occurredAtUtc: '2026-10-04T07:22:00Z', added: [], removed: [], kept: [], noteChanged: false, note: null, ...patch,
}) as Parameters<typeof describeHistoryEntry>[0];

test('history entries are described by what changed, not by the summary sentence', () => {
  const added = describeHistoryEntry(historyEntry({ added: ['Awarded'], kept: ['Potential'], noteChanged: true, note: 'Won' }));
  assert.deepEqual(added, { isCarriedOver: false, labelsChanged: true, noteTag: null, hasStructure: true });

  assert.equal(describeHistoryEntry(historyEntry({ removed: ['Potential'], noteChanged: true, note: null })).noteTag, 'Note removed');
  assert.equal(describeHistoryEntry(historyEntry({ kept: ['Potential'], noteChanged: true, note: 'Revised' })).noteTag, 'Note updated');
  assert.equal(describeHistoryEntry(historyEntry({ kept: ['Potential'], noteChanged: true, note: 'Revised' })).labelsChanged, false);

  const carried = describeHistoryEntry(historyEntry({ action: 'ACHIEVEMENTS_CARRIED_OVER', added: ['Potential'] }));
  assert.equal(carried.isCarriedOver, true);
  assert.equal(carried.labelsChanged, true);

  // Nothing readable: the summary sentence is shown instead.
  assert.equal(describeHistoryEntry(historyEntry()).hasStructure, false);
});

test('history dates spell out the month, drop seconds and survive bad input', () => {
  assert.match(formatHistoryDate('2026-10-04T07:22:32Z'), /^\d{1,2} [A-Z][a-z]{2} 2026, \d{2}:\d{2}$/);
  assert.doesNotMatch(formatHistoryDate('2026-10-04T07:22:32Z'), /:\d{2}:\d{2}/);
  assert.equal(formatHistoryDate('not a date'), '-');
});

test('history cache keys follow the user, the project and its row version', () => {
  const key = projectDataHistoryKey('user-a', 'project-1', '7');
  assert.deepEqual(key.slice(0, 2), projectDataScopeKey('user-a'));
  assert.notDeepEqual(key, projectDataHistoryKey('user-a', 'project-1', '8'));
  assert.notDeepEqual(key, projectDataHistoryKey('user-a', 'project-2', '7'));
  assert.notDeepEqual(key, projectDataHistoryKey('user-b', 'project-1', '7'));
});

test('the history request targets the project and maps the entries', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    mock.onGet('/project-data/project-1/achievements/history').reply(200, {
      success: true, message: 'ok',
      data: { totalCount: 2, items: [
        { id: 'a', action: 'PROJECT_ACHIEVEMENTS_CHANGED', summary: 'Achievements changed from none to Potential.', actorName: 'EHUB', occurredAtUtc: '2026-10-04T01:00:00Z', added: ['Potential'], removed: [], kept: [], noteChanged: false, note: null },
        { id: 'b', action: 'ACHIEVEMENTS_CARRIED_OVER', summary: 'Carried over achievements (Potential).', actorName: null, occurredAtUtc: '2026-09-01T01:00:00Z', added: ['Potential'], removed: [], kept: [], noteChanged: false, note: null },
      ] },
    });
    const response = await projectDataApi.getAchievementHistory('project-1');
    assert.equal(response.data.totalCount, 2);
    assert.equal(response.data.items[0].actorName, 'EHUB');
    assert.equal(response.data.items[1].actorName, null);
    assert.deepEqual(response.data.items[0].added, ['Potential']);
  } finally {
    mock.restore();
  }
});

test('the workspace link targets exactly the team of the selected project', () => {
  assert.equal(projectWorkspacePath('0b8f6d3e-1111-4222-8333-444455556666'), '/workspace/teams/0b8f6d3e-1111-4222-8333-444455556666');
  assert.equal(projectWorkspacePath('a/b?c'), '/workspace/teams/a%2Fb%3Fc');
});

test('page summary describes the visible range and handles empty results', () => {
  assert.equal(pageSummary({ pageIndex: 1, pageSize: 20, totalItems: 0 }), 'No projects');
  assert.equal(pageSummary({ pageIndex: 2, pageSize: 20, totalItems: 35 }), '21–35 of 35 projects');
  assert.equal(pageSummary({ pageIndex: 9, pageSize: 20, totalItems: 35 }), '35 projects');
});

const item: ProjectDataItem = {
  projectId: 'project-1', teamId: 'team-1', classId: 'class-1', semesterId: 'semester-1', semesterCode: 'FA2026',
  subjectId: 'subject-1', subjectCode: 'EXE201', classCode: 'EXE201_8', groups: [], projectName: 'Health app', description: null,
  startupIndustries: [], lecturer: null, mentor: null, academicMentor: null, achievements: [], achievementNote: null,
  achievementsUpdatedAtUtc: null, achievementsUpdatedBy: null, rowVersion: '7',
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

test('the summary request sends search and filters only and maps the counters', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    let seenParams: Record<string, unknown> = {};
    mock.onGet('/project-data/summary').reply(config => {
      seenParams = config.params;
      return [200, { success: true, message: 'ok', data: { totalGroups: 8, potentialGroups: 3, fundedGroups: 2, awardedGroups: 1 } }];
    });
    const response = await projectDataApi.getSummary({
      ...DEFAULT_PROJECT_DATA_QUERY, subjectCode: 'EXE101', pageIndex: 4, pageSize: 50, sortBy: 'group', isDescending: true,
    });
    assert.deepEqual(response.data, { totalGroups: 8, potentialGroups: 3, fundedGroups: 2, awardedGroups: 1 });
    assert.deepEqual(seenParams, { subjectCode: 'EXE101' });
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
      return [200, { success: true, message: 'ok', data: { projectId: 'project-1', achievements: ['Potential', 'Funded'], rowVersion: '8', note: 'Strong pilot', updatedAtUtc: '2026-10-04T00:00:00Z', updatedBy: { userId: 'u1', fullName: 'EHUB' } } }];
    });
    const result = await projectDataApi.updateAchievements('project-1', { achievements: ['Potential', 'Funded'], note: 'Strong pilot', rowVersion: '7' });
    assert.deepEqual(body, { achievements: ['Potential', 'Funded'], note: 'Strong pilot', rowVersion: '7' });
    assert.equal(result.data.rowVersion, '8');
    assert.equal(result.data.note, 'Strong pilot');
    assert.equal(result.data.updatedBy?.fullName, 'EHUB');

    mock.onPut('/project-data/project-1/achievements').replyOnce(409, { success: false, message: 'changed', code: 'PROJECT_DATA_CONCURRENCY_CONFLICT' });
    await assert.rejects(
      projectDataApi.updateAchievements('project-1', { achievements: [], note: null, rowVersion: '7' }),
      (error: { response?: { status?: number; data?: { code?: string } } }) =>
        error.response?.status === 409 && error.response.data?.code === 'PROJECT_DATA_CONCURRENCY_CONFLICT',
    );
  } finally {
    mock.restore();
  }
});
