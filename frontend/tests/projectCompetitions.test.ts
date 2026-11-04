import assert from 'node:assert/strict';
import test from 'node:test';
import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../src/api/axiosClient.ts';
import { projectDataApi } from '../src/api/projectDataApi.ts';
import {
  COMPETITION_MAX_ENTRIES,
  PROJECT_COMPETITIONS,
  PROJECT_COMPETITION_STAGES,
} from '../src/types/projectData.ts';
import type { ProjectCompetition } from '../src/types/projectData.ts';
import {
  DEFAULT_PROJECT_DATA_QUERY,
  activeFilterValues,
  cleanCompetitionName,
  competitionSummary,
  describeCompetitionDraft,
  newCompetitionDraft,
  parseProjectDataQuery,
  removeFilterValue,
  toCompetitionDrafts,
  toProjectDataSearchParams,
  toProjectDataSummaryParams,
  toProjectDataTrendParams,
} from '../src/utils/projectData.ts';
import { fileDateStamp } from '../src/utils/downloadBlob.ts';

const parse = (search: string) => parseProjectDataQuery(new URLSearchParams(search));

const saved = (patch: Partial<ProjectCompetition> = {}): ProjectCompetition => ({
  competition: 'FShark', otherName: null, name: 'FShark', year: 2026, stage: 'Final', ...patch,
});

test('the fixed list has the nine competitions and Others, and three stages in order', () => {
  assert.deepEqual([...PROJECT_COMPETITIONS], [
    'SV.STARTUP', 'Sinh viên Thế Hệ Mới VTV', 'VietFuture Awards', 'FTalent', 'DBC', 'FShark', 'Startup Wheel', 'DAVAS', 'SURF', 'Others',
  ]);
  assert.deepEqual([...PROJECT_COMPETITION_STAGES], ['Registered', 'Qualifier', 'Final']);
});

test('competition filters are read from repeated URL keys, matched ignoring case and written back unchanged', () => {
  const query = parse('competition=fshark&competition=Others&competition=Nope&competitionStage=final&competitionStage=Winner&competitionYear=2026&competitionYear=26&competitionYear=2025&competitionYear=2026');
  assert.deepEqual(query.competition, ['FShark', 'Others']);
  assert.deepEqual(query.competitionStage, ['Final']);
  assert.deepEqual(query.competitionYear, ['2026', '2025']);
  assert.deepEqual(parseProjectDataQuery(toProjectDataSearchParams(query)), query);
  assert.equal(
    toProjectDataSearchParams(query).toString(),
    'competition=FShark&competition=Others&competitionStage=Final&competitionYear=2026&competitionYear=2025',
  );
  assert.deepEqual(parse('competitionYear=1999&competitionYear=2101').competitionYear, []);
  assert.deepEqual(parse('').competition, []);
});

test('competition filters are sent with the other filters, also to the trends, and show as one chip per value', () => {
  const query = { ...DEFAULT_PROJECT_DATA_QUERY, competition: ['FShark', 'DBC'], competitionStage: ['Final' as const], competitionYear: ['2026'] };
  assert.deepEqual(toProjectDataSummaryParams(query), { competition: ['FShark', 'DBC'], competitionStage: ['Final'], competitionYear: ['2026'] });
  assert.deepEqual(toProjectDataTrendParams(query), { competition: ['FShark', 'DBC'], competitionStage: ['Final'], competitionYear: ['2026'] });
  assert.deepEqual(activeFilterValues(query).map(item => `${item.key}:${item.value}`), [
    'competition:FShark', 'competition:DBC', 'competitionStage:Final', 'competitionYear:2026',
  ]);
  assert.deepEqual(removeFilterValue(query, 'competition', 'DBC'), { competition: ['FShark'] });
});

test('a badge says the competition, the year and how far the project got', () => {
  assert.equal(competitionSummary(saved()), 'FShark 2026 · Final round');
  assert.equal(competitionSummary(saved({ competition: 'Others', otherName: 'Tech Cup', name: 'Tech Cup', year: 2025, stage: 'Registered' })), 'Tech Cup 2025 · Registered');
  assert.equal(competitionSummary(saved({ stage: 'Qualifier' })), 'FShark 2026 · Qualifier round');
});

test('an untouched draft equals the saved list; editing a stage, year, name or a row makes it differ', () => {
  const list = [saved(), saved({ competition: 'Others', otherName: 'Tech Cup', name: 'Tech Cup', year: 2025, stage: 'Qualifier' })];
  const draft = toCompetitionDrafts(list);
  const untouched = describeCompetitionDraft(list, draft);
  assert.equal(untouched.dirty, false);
  assert.equal(untouched.blocked, false);
  assert.deepEqual(untouched.payload, [
    { competition: 'FShark', otherName: null, year: 2026, stage: 'Final' },
    { competition: 'Others', otherName: 'Tech Cup', year: 2025, stage: 'Qualifier' },
  ]);

  assert.equal(describeCompetitionDraft(list, draft.map((row, index) => (index === 0 ? { ...row, stage: 'Qualifier' as const } : row))).dirty, true);
  assert.equal(describeCompetitionDraft(list, draft.map((row, index) => (index === 0 ? { ...row, year: '2027' } : row))).dirty, true);
  assert.equal(describeCompetitionDraft(list, draft.slice(1)).dirty, true);
  assert.equal(describeCompetitionDraft(list, [...draft, { ...newCompetitionDraft(2026), competition: 'DBC' }]).dirty, true);
  // The same name with other spacing and case is no change; a different order is no change either.
  assert.equal(describeCompetitionDraft(list, [
    draft[1] && { ...draft[1], otherName: '  tech   CUP ' },
    draft[0],
  ]).dirty, false);
  assert.equal(describeCompetitionDraft([], []).dirty, false);
});

test('a draft is checked the way the API will: choice, name for Others, year range, duplicates and the limit', () => {
  const blank = newCompetitionDraft(2026);
  assert.equal(blank.competition, '');
  assert.equal(blank.year, '2026');
  assert.equal(blank.stage, 'Registered');
  const blankView = describeCompetitionDraft([], [blank]);
  assert.equal(blankView.blocked, true);
  assert.match(blankView.errors[blank.key], /Choose a competition/);

  const others = { ...newCompetitionDraft(2026), competition: 'Others', otherName: '   ' };
  assert.match(describeCompetitionDraft([], [others]).errors[others.key], /Enter the competition name/);
  assert.equal(describeCompetitionDraft([], [{ ...others, otherName: 'Tech Cup' }]).blocked, false);
  assert.match(describeCompetitionDraft([], [{ ...others, otherName: 'x'.repeat(101) }]).errors[others.key], /at most 100/);

  const dbc = { ...newCompetitionDraft(2026), competition: 'DBC' };
  for (const year of ['', '26', '1999', '2101', 'abcd']) {
    assert.match(describeCompetitionDraft([], [{ ...dbc, year }]).errors[dbc.key], /year between 2000 and 2100/, year);
  }
  assert.equal(describeCompetitionDraft([], [{ ...dbc, year: '2000' }]).blocked, false);
  assert.equal(describeCompetitionDraft([], [{ ...dbc, year: '2100' }]).blocked, false);

  // The second of two equal rows is the one flagged; another year or another Others name is a different entry.
  const twin = { ...dbc, key: 'twin' };
  const duplicate = describeCompetitionDraft([], [dbc, twin]);
  assert.deepEqual(Object.keys(duplicate.errors), ['twin']);
  assert.match(duplicate.errors.twin, /already listed/);
  assert.equal(describeCompetitionDraft([], [dbc, { ...twin, year: '2025' }]).blocked, false);
  const techCup = { ...others, otherName: 'Tech Cup' };
  assert.equal(describeCompetitionDraft([], [techCup, { ...techCup, key: 'other', otherName: 'Other Cup' }]).blocked, false);
  assert.equal(describeCompetitionDraft([], [techCup, { ...techCup, key: 'other', otherName: ' tech   cup ' }]).blocked, true);

  const many = Array.from({ length: COMPETITION_MAX_ENTRIES + 1 }, (_, index) => ({ ...dbc, key: `row-${index}`, year: String(2000 + index) }));
  const tooMany = describeCompetitionDraft([], many);
  assert.equal(tooMany.blocked, true);
  assert.match(tooMany.listError, /at most 30/);
  assert.equal(describeCompetitionDraft([], many.slice(0, COMPETITION_MAX_ENTRIES)).blocked, false);
});

test('a name typed for a listed competition is not sent, and spaces in an Others name are collapsed', () => {
  const rows = [
    { ...newCompetitionDraft(2026), competition: 'DBC', otherName: 'leftover' },
    { ...newCompetitionDraft(2026), competition: 'Others', otherName: '  Tech    Cup ', year: ' 2025 ' },
  ];
  assert.deepEqual(describeCompetitionDraft([], rows).payload, [
    { competition: 'DBC', otherName: null, year: 2026, stage: 'Registered' },
    { competition: 'Others', otherName: 'Tech Cup', year: 2025, stage: 'Registered' },
  ]);
  assert.equal(cleanCompetitionName('  a   b  '), 'a b');
});

test('saving PUTs the whole list with the row version and surfaces conflicts', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    let body: unknown;
    mock.onPut('/project-data/project-1/competitions').replyOnce(config => {
      body = JSON.parse(config.data);
      return [200, { success: true, message: 'ok', data: { projectId: 'project-1', competitions: [saved()], rowVersion: '8' } }];
    });
    const payload = { entries: [{ competition: 'FShark', otherName: null, year: 2026, stage: 'Final' as const }], rowVersion: '7' };
    const result = await projectDataApi.updateCompetitions('project-1', payload);
    assert.deepEqual(body, payload);
    assert.equal(result.data.rowVersion, '8');
    assert.equal(result.data.competitions[0].stage, 'Final');

    mock.onPut('/project-data/project-1/competitions').replyOnce(409, { success: false, message: 'changed', code: 'PROJECT_DATA_CONCURRENCY_CONFLICT' });
    await assert.rejects(
      projectDataApi.updateCompetitions('project-1', { entries: [], rowVersion: '7' }),
      (error: { response?: { status?: number } }) => error.response?.status === 409,
    );
  } finally {
    mock.restore();
  }
});

test('the project data report request carries the current filters as repeated keys and asks for a file', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    let url = '';
    let responseType: unknown;
    mock.onGet('/project-data/export').reply(config => {
      url = axiosClient.getUri(config);
      responseType = config.responseType;
      return [200, new Blob(['xlsx'])];
    });
    await projectDataApi.exportProjectData({
      ...DEFAULT_PROJECT_DATA_QUERY, search: 'farm', competition: ['FShark', 'DBC'], competitionYear: ['2026'], pageIndex: 4, pageSize: 50,
    });
    const params = new URL(url, 'http://localhost').searchParams;
    assert.equal(responseType, 'blob');
    assert.deepEqual(params.getAll('competition'), ['FShark', 'DBC']);
    assert.deepEqual(params.getAll('competitionYear'), ['2026']);
    assert.equal(params.get('search'), 'farm');
    // Paging and sorting are not part of a report.
    assert.equal(params.has('pageIndex'), false);
    assert.equal(params.has('pageSize'), false);
    assert.doesNotMatch(url, /%5B%5D|\[\]/);
  } finally {
    mock.restore();
  }
});

test('the report file name carries the local date', () => {
  assert.equal(fileDateStamp(new Date(2026, 10, 4, 23, 59)), '20261104');
  assert.equal(fileDateStamp(new Date(2027, 0, 9)), '20270109');
});
